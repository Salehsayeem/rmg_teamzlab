using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Rmg.BuildingBlocks.Audit;
using Rmg.BuildingBlocks.Outbox;
using Rmg.Database;
using Rmg.Modules.Identity.Domain;
using Rmg.Modules.Merchandising.Domain;
using Rmg.Modules.Organization.Domain;

namespace Rmg.IntegrationTests;

// Task 10 (ORD/M002): buyer profile + contacts. POST creates the buyer Active at revision 1 and
// commits buyer + contact + outbox + audit rows in one SaveChanges; listing is cursor-paginated;
// contact emails/phones are property-level authz — only ord.buyer.manage holders see them; duplicate
// codes are 422 BUYER_CODE_TAKEN and another tenant's id is 404 (never a leak).
public class BuyerEndpointTests : IntegrationTestBase
{
    private static string AdminPassword =>
        Environment.GetEnvironmentVariable("Seed__AdminPassword") ?? Seeder.DefaultAdminPassword;

    private async Task<string> AdminTokenAsync()
    {
        await Seeder.SeedAsync(Db);
        var login = await Client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "admin@tzl.demo", password = AdminPassword });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await JsonDocument.ParseAsync(await login.Content.ReadAsStreamAsync());
        return body.RootElement.GetProperty("data").GetProperty("accessToken").GetString()!;
    }

    private async Task<HttpResponseMessage> PostBuyerAsync(string token, string code, string? email = null, string? phone = null)
    {
        Client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var payload = new Dictionary<string, object?>
        {
            ["code"] = code,
            ["name"] = "Acme Apparel Ltd.",
            ["country"] = "BD",
            ["currency"] = "USD",
        };
        if (email is not null)
            payload["contacts"] = new[] { new { name = "Nadia Islam", email, phone } };
        return await Client.PostAsJsonAsync("/api/v1/ord/buyers", payload);
    }

    private static Buyer BuyerRow(Guid tenantId, string code) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        Code = code,
        Name = $"Buyer {code}",
        Country = "BD",
        Currency = "USD",
        Status = "Active",
        Revision = 1,
        CreatedAtUtc = DateTime.UtcNow,
        CreatedBy = "test",
    };

    private async Task<string> ReaderTokenAsync()
    {
        // A reader: ord.buyer.read only — no ord.buyer.manage, so contact details must be stripped.
        var tenantId = await Db.Set<Tenant>().Select(t => t.Id).SingleAsync();
        var hasher = new PasswordHasher<AppUser>();
        var reader = new AppUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = "buyer-reader@tzl.demo",
            DisplayName = "Buyer Reader",
            IsActive = true,
            Revision = 1,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "test",
        };
        reader.PasswordHash = hasher.HashPassword(reader, AdminPassword);
        var role = new Role { Id = Guid.NewGuid(), TenantId = tenantId, Code = "BuyerReader", Revision = 1, CreatedAtUtc = DateTime.UtcNow, CreatedBy = "test" };
        Db.AddRange(reader, role,
            new RolePermission { RoleId = role.Id, PermissionCode = "ord.buyer.read" },
            new UserRole { TenantId = tenantId, UserId = reader.Id, RoleId = role.Id });
        await Db.SaveChangesAsync();

        var login = await Client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = reader.Email, password = AdminPassword });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await JsonDocument.ParseAsync(await login.Content.ReadAsStreamAsync());
        return body.RootElement.GetProperty("data").GetProperty("accessToken").GetString()!;
    }

    [Fact]
    public async Task CreateBuyer_201_AndOutboxAuditAtomic()
    {
        var token = await AdminTokenAsync();

        var response = await PostBuyerAsync(token, "BUY-NEW", email: "nadia@acme.demo", phone: "+8801700000000");

        Assert.True(response.StatusCode == HttpStatusCode.Created,
            $"create failed: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var data = body.RootElement.GetProperty("data");
        var buyerId = data.GetProperty("id").GetGuid();
        Assert.Equal("BUY-NEW", data.GetProperty("code").GetString());
        Assert.Equal("Acme Apparel Ltd.", data.GetProperty("name").GetString());
        Assert.Equal("BD", data.GetProperty("country").GetString());
        Assert.Equal("USD", data.GetProperty("currency").GetString());
        Assert.Equal("Active", data.GetProperty("status").GetString());
        Assert.Equal(1, data.GetProperty("revision").GetInt32());
        Assert.Equal("Nadia Islam", data.GetProperty("contacts")[0].GetProperty("name").GetString());
        Assert.Equal("nadia@acme.demo", data.GetProperty("contacts")[0].GetProperty("email").GetString());

        // One SaveChanges: buyer + contact + exactly 1 outbox row + 1 audit row, all visible together.
        var persisted = await Db.Set<Buyer>().SingleAsync(b => b.Id == buyerId);
        Assert.Equal("Active", persisted.Status);
        Assert.Equal(1, persisted.Revision);
        Assert.Single(await Db.Set<BuyerContact>().Where(c => c.BuyerId == buyerId).ToListAsync());

        var outbox = Assert.Single(await Db.Set<OutboxMessage>().ToListAsync());
        Assert.Equal("ord.buyer.and.customer.profile.changed.v1", outbox.EventType);
        Assert.Equal(buyerId, outbox.AggregateId);
        Assert.Equal(persisted.TenantId, outbox.TenantId);
        Assert.Equal("Pending", outbox.Status);

        var audit = Assert.Single(await Db.Set<AuditEvent>().ToListAsync());
        Assert.Equal("ord.buyer.create", audit.Command);
        Assert.Equal("ord.buyer", audit.EntityType);
        Assert.Equal(buyerId.ToString(), audit.EntityId);
        Assert.Equal(persisted.TenantId, audit.TenantId);
    }

    [Fact]
    public async Task List_Buyers_CursorPagination()
    {
        var token = await AdminTokenAsync();
        var tenantId = await Db.Set<Tenant>().Select(t => t.Id).SingleAsync();
        Db.AddRange(BuyerRow(tenantId, "BUY-A"), BuyerRow(tenantId, "BUY-B"), BuyerRow(tenantId, "BUY-C"));
        await Db.SaveChangesAsync();
        Client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var page1 = await Client.GetAsync("/api/v1/ord/buyers?pageSize=2");

        Assert.Equal(HttpStatusCode.OK, page1.StatusCode);
        var body1 = await JsonDocument.ParseAsync(await page1.Content.ReadAsStreamAsync());
        var codes1 = body1.RootElement.GetProperty("data").EnumerateArray()
            .Select(b => b.GetProperty("code").GetString()).ToArray();
        // The boot-seeded demo buyer BUY-001 sorts first on the code keyset.
        Assert.Equal(["BUY-001", "BUY-A"], codes1);
        var meta = body1.RootElement.GetProperty("meta").GetProperty("meta");
        Assert.Equal(2, meta.GetProperty("pageSize").GetInt32());
        var nextCursor = meta.GetProperty("nextCursor").GetString();
        Assert.False(string.IsNullOrEmpty(nextCursor));

        var page2 = await Client.GetAsync($"/api/v1/ord/buyers?pageSize=2&cursor={nextCursor}");

        Assert.Equal(HttpStatusCode.OK, page2.StatusCode);
        var body2 = await JsonDocument.ParseAsync(await page2.Content.ReadAsStreamAsync());
        var codes2 = body2.RootElement.GetProperty("data").EnumerateArray()
            .Select(b => b.GetProperty("code").GetString()).ToArray();
        Assert.Equal(["BUY-B", "BUY-C"], codes2);
        Assert.Null(body2.RootElement.GetProperty("meta").GetProperty("meta").GetProperty("nextCursor").GetString());
    }

    [Fact]
    public async Task ContactFields_WithoutManagePerm_Stripped()
    {
        var token = await AdminTokenAsync();
        var created = await PostBuyerAsync(token, "BUY-PRV", email: "nadia@acme.demo", phone: "+8801700000000");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var buyerId = (await JsonDocument.ParseAsync(await created.Content.ReadAsStreamAsync()))
            .RootElement.GetProperty("data").GetProperty("id").GetGuid();

        // Control: the manage holder sees the contact details.
        Client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var adminView = await Client.GetAsync($"/api/v1/ord/buyers/{buyerId}");
        Assert.Equal(HttpStatusCode.OK, adminView.StatusCode);
        var adminRaw = await adminView.Content.ReadAsStringAsync();
        Assert.Contains("nadia@acme.demo", adminRaw);

        // The reader (ord.buyer.read only) gets contacts: [{name}] — emails/phones stripped server-side.
        Client.DefaultRequestHeaders.Authorization = new("Bearer", await ReaderTokenAsync());
        foreach (var url in new[] { $"/api/v1/ord/buyers/{buyerId}", "/api/v1/ord/buyers?pageSize=100" })
        {
            var view = await Client.GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, view.StatusCode);
            var raw = await view.Content.ReadAsStringAsync();
            Assert.Contains("Nadia Islam", raw);
            Assert.DoesNotContain("nadia@acme.demo", raw);
            Assert.DoesNotContain("+8801700000000", raw);
        }
    }

    [Fact]
    public async Task DuplicateBuyerCode_422_BUYER_CODE_TAKEN()
    {
        var token = await AdminTokenAsync();
        Assert.Equal(HttpStatusCode.Created, (await PostBuyerAsync(token, "BUY-DUP")).StatusCode);

        var dupe = await PostBuyerAsync(token, "BUY-DUP");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, dupe.StatusCode);
        var problem = await JsonDocument.ParseAsync(await dupe.Content.ReadAsStreamAsync());
        Assert.Equal("BUYER_CODE_TAKEN", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal(422, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(1, await Db.Set<Buyer>().CountAsync(b => b.Code == "BUY-DUP")); // scoped: the demo dataset owns BUY-001
    }

    [Fact]
    public async Task CrossTenant_BuyerId_404()
    {
        var token = await AdminTokenAsync();

        // Another tenant's buyer, inserted directly — must be indistinguishable from a missing row.
        var otherBuyer = BuyerRow(Guid.NewGuid(), "BUY-OTH");
        Db.AddRange(new Tenant
        {
            Id = otherBuyer.TenantId,
            Name = "Rival Ltd.",
            Code = "RIV",
            DefaultLocale = "en",
            RequireMakerChecker = false,
            Revision = 1,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "test",
        }, otherBuyer);
        await Db.SaveChangesAsync();

        Client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var response = await Client.GetAsync($"/api/v1/ord/buyers/{otherBuyer.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal("NOT_FOUND", problem.RootElement.GetProperty("code").GetString());
    }
}
