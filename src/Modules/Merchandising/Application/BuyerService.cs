using Rmg.BuildingBlocks.Audit;
using Rmg.BuildingBlocks.Common;
using Rmg.BuildingBlocks.Outbox;
using Rmg.Modules.Merchandising.Contracts;
using Rmg.Modules.Merchandising.Domain;

namespace Rmg.Modules.Merchandising.Application;

// Persistence seam for buyers — implemented in module Infrastructure against the ambient scoped
// DbContext. AddAsync stages a change set only; SaveAsync commits it — the service interleaves the
// audit + outbox writers between the two, so one SaveChanges covers buyer + contacts + audit + outbox.
public interface IBuyerRepository
{
    Task<Buyer[]> ListPageAsync(Guid tenantId, int take, string? afterCode, CancellationToken ct);
    Task<Buyer?> FindAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<bool> CodeExistsAsync(Guid tenantId, string code, CancellationToken ct);
    Task<Dictionary<Guid, BuyerContact[]>> ContactsForAsync(Guid tenantId, IReadOnlyList<Guid> buyerIds, CancellationToken ct);
    Task AddAsync(Buyer buyer, IReadOnlyList<BuyerContact> contacts, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
}

// Buyer profile creation: required-field + ISO code-length validation first, duplicate codes are a
// 422 (never a SQL error), and the profile-changed event + audit row ride the create's own
// transaction (one SaveChanges — the outbox guarantee).
public sealed class BuyerService(IBuyerRepository buyers, IAuditWriter audit, IOutboxWriter outbox) : IMerchandisingApi
{
    public const string ProfileChangedEvent = "ord.buyer.and.customer.profile.changed.v1";

    public async Task<BuyerDto> CreateBuyerAsync(Guid tenantId, string actor, BuyerCreateRequest request, CancellationToken ct = default)
    {
        var errors = new Dictionary<string, string[]>();
        var code = request.Code?.Trim();
        var name = request.Name?.Trim();
        var country = request.Country?.Trim();
        var currency = request.Currency?.Trim();

        if (string.IsNullOrEmpty(code)) errors["code"] = ["Code is required."];
        if (string.IsNullOrEmpty(name)) errors["name"] = ["Name is required."];
        if (string.IsNullOrEmpty(country)) errors["country"] = ["Country is required."];
        else if (country.Length != 2) errors["country"] = ["Country must be a 2-letter ISO 3166 code."];
        if (string.IsNullOrEmpty(currency)) errors["currency"] = ["Currency is required."];
        else if (currency.Length != 3) errors["currency"] = ["Currency must be a 3-letter ISO 4217 code."];

        var contacts = (request.Contacts ?? []).Select(c => new BuyerContact
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = c.Name?.Trim() ?? "",
            Email = string.IsNullOrWhiteSpace(c.Email) ? null : c.Email.Trim(),
            Phone = string.IsNullOrWhiteSpace(c.Phone) ? null : c.Phone.Trim(),
        }).ToArray();

        if (contacts.Any(c => c.Name.Length == 0))
            errors["contacts"] = ["Every contact needs a name."];
        var duplicateEmails = contacts.Where(c => c.Email is not null)
            .GroupBy(c => c.Email!, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1).Select(g => g.Key!).ToArray();
        if (duplicateEmails.Length > 0)
            errors["contacts.email"] = [$"Duplicate contact email(s): {string.Join(", ", duplicateEmails)}"];

        if (errors.Count > 0) throw new ValidationProblem(errors);

        if (await buyers.CodeExistsAsync(tenantId, code!, ct))
            throw new BusinessRuleProblem("BUYER_CODE_TAKEN", $"Buyer code '{code}' is already in use.");

        var now = DateTime.UtcNow;
        var buyer = new Buyer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = code!,
            Name = name!,
            Country = country!,
            Currency = currency!,
            Status = "Active", // command semantics: creates in status Active at revision 1
            Revision = 1,
            CreatedAtUtc = now,
            CreatedBy = actor,
        };

        // No navigations between the entities — the FK columns must be set by hand (buyer_id points at
        // the new buyer; created_at/by satisfy the baseline NOT NULLs).
        foreach (var c in contacts) { c.BuyerId = buyer.Id; c.CreatedAtUtc = now; c.CreatedBy = actor; }

        await buyers.AddAsync(buyer, contacts, ct);
        await audit.WriteAsync("ord.buyer.create", "ord.buyer", buyer.Id, null,
            new { buyer.Code, buyer.Name, buyer.Country, buyer.Currency, buyer.Status, contacts = contacts.Length });
        await outbox.EnqueueAsync(ProfileChangedEvent,
            new
            {
                buyerId = buyer.Id,
                code = buyer.Code,
                name = buyer.Name,
                country = buyer.Country,
                currency = buyer.Currency,
                status = buyer.Status,
                contactCount = contacts.Length,
            },
            buyer.Id);
        await buyers.SaveAsync(ct);

        return ToDto(buyer, contacts);
    }

    public async Task<(BuyerDto[] Buyers, PageMeta Meta)> ListBuyersAsync(Guid tenantId, int pageSize, string? cursor, CancellationToken ct = default)
    {
        // Keyset pagination on the tenant-unique code: the cursor is the last page's last code.
        var (size, afterCode) = Page.Normalize(pageSize, cursor);
        var rows = await buyers.ListPageAsync(tenantId, size + 1, afterCode, ct);
        var page = rows.Take(size).ToArray();
        string? nextCursor = rows.Length > size ? page[^1].Code : null;

        var contacts = await buyers.ContactsForAsync(tenantId, page.Select(b => b.Id).ToArray(), ct);
        var dtos = page.Select(b => ToDto(b, contacts.GetValueOrDefault(b.Id, []))).ToArray();
        return (dtos, new PageMeta(size, nextCursor, null));
    }

    public async Task<BuyerDto?> FindBuyerAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var buyer = await buyers.FindAsync(tenantId, id, ct);
        if (buyer is null) return null; // unknown or other tenant's id — indistinguishable by design
        var contacts = await buyers.ContactsForAsync(tenantId, [buyer.Id], ct);
        return ToDto(buyer, contacts.GetValueOrDefault(buyer.Id, []));
    }

    private static BuyerDto ToDto(Buyer buyer, IReadOnlyList<BuyerContact> contacts) =>
        new(buyer.Id, buyer.Code, buyer.Name, buyer.Country, buyer.Currency, buyer.Status, buyer.Revision,
            contacts.Select(c => new BuyerContactDto(c.Name, c.Email, c.Phone)).ToArray());
}
