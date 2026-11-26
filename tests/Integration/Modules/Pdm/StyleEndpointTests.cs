using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Rmg.BuildingBlocks.Audit;
using Rmg.BuildingBlocks.Outbox;
using Rmg.Database;
using Rmg.Modules.Merchandising.Domain;
using Rmg.Modules.Organization.Domain;
using Rmg.Modules.ProductEngineering.Domain;

namespace Rmg.IntegrationTests;

// Task 12 (PDM): style master + versions + variants. POST creates Draft style + version 1 Draft +
// variant rows in one SaveChanges (with outbox + audit); approve moves style AND current version to
// Approved (guard: ≥1 variant → 422 NO_VARIANTS); duplicate codes are 422 STYLE_CODE_TAKEN.
// Direct row asserts use IgnoreQueryFilters(): the entities' fail-closed filter only re-roots on
// TenantScopedDbContext, while the harness reads on the unfiltered-for-them base RmgDbContext.
public class StyleEndpointTests : IntegrationTestBase
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

    // pdm.style.buyer_id has a composite FK to ord.buyer (ruling W2-b) — the tests seed a buyer row
    // directly; there is no service-level buyer check, the FK enforces.
    private async Task<Guid> SeedBuyerAsync(Guid tenantId, string code = "BUY-STY")
    {
        var buyer = new Buyer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = code,
            Name = "Style Test Buyer",
            Country = "BD",
            Currency = "USD",
            Status = "Active",
            Revision = 1,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "test",
        };
        Db.Add(buyer);
        await Db.SaveChangesAsync();
        return buyer.Id;
    }

    private async Task<HttpResponseMessage> PostStyleAsync(string token, Guid buyerId, string code, object[]? variants = null)
    {
        Client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return await Client.PostAsJsonAsync("/api/v1/pdm/styles", new
        {
            code,
            name = "Classic Tee",
            season = "SS26",
            category = "Tops",
            buyerId,
            variants = variants ?? new object[] { new { colorCode = "RED", sizeCode = "M" } },
        });
    }

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return body.RootElement.GetProperty("data");
    }

    private async Task<Guid> CreateStyleAsync(string token, Guid buyerId, string code = "STY-001", object[]? variants = null)
    {
        var response = await PostStyleAsync(token, buyerId, code, variants);
        Assert.True(response.StatusCode == HttpStatusCode.Created,
            $"style create failed: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return (await DataAsync(response)).GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task StyleLifecycle_Draft_Approve_VersionsApproved()
    {
        var token = await AdminTokenAsync();
        var tenantId = await Db.Set<Tenant>().Select(t => t.Id).SingleAsync();
        var buyerId = await SeedBuyerAsync(tenantId);

        var created = await PostStyleAsync(token, buyerId, "STY-LIFE",
            [new { colorCode = "RED", sizeCode = "S" }, new { colorCode = "RED", sizeCode = "M" }]);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var data = await DataAsync(created);
        var styleId = data.GetProperty("id").GetGuid();
        Assert.Equal("Draft", data.GetProperty("status").GetString());
        Assert.Equal(1, data.GetProperty("revision").GetInt32());
        var version = data.GetProperty("versions")[0];
        Assert.Equal(1, version.GetProperty("version").GetInt32());
        Assert.Equal("Draft", version.GetProperty("status").GetString());
        Assert.Equal(2, version.GetProperty("variants").GetArrayLength());
        Assert.Equal("RED", version.GetProperty("variants")[0].GetProperty("colorCode").GetString());
        Assert.Equal("M", version.GetProperty("variants")[1].GetProperty("sizeCode").GetString());

        Client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var approved = await Client.PostAsync($"/api/v1/pdm/styles/{styleId}/approve", null);

        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        var approvedData = await DataAsync(approved);
        Assert.Equal("Approved", approvedData.GetProperty("status").GetString());
        Assert.Equal("Approved", approvedData.GetProperty("versions")[0].GetProperty("status").GetString());
        Assert.NotNull(approvedData.GetProperty("versions")[0].GetProperty("approvedAtUtc"));

        // Persisted: style + version rows moved together.
        var styleRow = await Db.Set<Style>().IgnoreQueryFilters().SingleAsync(s => s.Id == styleId);
        Assert.Equal("Approved", styleRow.Status);
        var versionRow = await Db.Set<StyleVersion>().IgnoreQueryFilters().SingleAsync(v => v.StyleId == styleId);
        Assert.Equal("Approved", versionRow.Status);
        Assert.NotNull(versionRow.ApprovedAtUtc);
        Assert.Equal(2, await Db.Set<StyleVariant>().IgnoreQueryFilters().CountAsync(v => v.StyleVersionId == versionRow.Id));

        // Detail endpoint reads it back Approved.
        Client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var detail = await Client.GetAsync($"/api/v1/pdm/styles/{styleId}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        Assert.Equal("Approved", (await DataAsync(detail)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task StyleVariants_DuplicateColorSize_422()
    {
        var token = await AdminTokenAsync();
        var tenantId = await Db.Set<Tenant>().Select(t => t.Id).SingleAsync();
        var buyerId = await SeedBuyerAsync(tenantId, "BUY-DUPV");

        var response = await PostStyleAsync(token, buyerId, "STY-DUPV",
            [new { colorCode = "RED", sizeCode = "S" }, new { colorCode = "RED", sizeCode = "S" }]);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        var problem = JsonDocument.Parse(raw);
        Assert.Equal(422, problem.RootElement.GetProperty("status").GetInt32());
        Assert.True(problem.RootElement.TryGetProperty("errors", out var errors)
            && errors.TryGetProperty("variants", out _), raw);
        Assert.Equal(0, await Db.Set<Style>().IgnoreQueryFilters().CountAsync(s => s.Code == "STY-DUPV")); // scoped: the demo dataset seeds STY-001
    }

    [Fact]
    public async Task ApproveStyle_NoVariants_422_NO_VARIANTS()
    {
        var token = await AdminTokenAsync();
        var tenantId = await Db.Set<Tenant>().Select(t => t.Id).SingleAsync();
        var buyerId = await SeedBuyerAsync(tenantId, "BUY-NV");
        var styleId = await CreateStyleAsync(token, buyerId, "STY-NV", variants: []);

        Client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var response = await Client.PostAsync($"/api/v1/pdm/styles/{styleId}/approve", null);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("NO_VARIANTS", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal(422, problem.RootElement.GetProperty("status").GetInt32());
        // Still Draft — the rejected write moved nothing.
        Assert.Equal("Draft", (await Db.Set<Style>().IgnoreQueryFilters().SingleAsync(s => s.Id == styleId)).Status);
    }

    [Fact]
    public async Task Style_Code_Duplicate_422()
    {
        var token = await AdminTokenAsync();
        var tenantId = await Db.Set<Tenant>().Select(t => t.Id).SingleAsync();
        var buyerId = await SeedBuyerAsync(tenantId, "BUY-DUPC");
        Assert.Equal(HttpStatusCode.Created, (await PostStyleAsync(token, buyerId, "STY-DUP")).StatusCode);

        var dupe = await PostStyleAsync(token, buyerId, "STY-DUP");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, dupe.StatusCode);
        var problem = JsonDocument.Parse(await dupe.Content.ReadAsStringAsync());
        Assert.Equal("STYLE_CODE_TAKEN", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal(422, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(1, await Db.Set<Style>().IgnoreQueryFilters().CountAsync(s => s.Code == "STY-DUP")); // scoped: the demo dataset seeds STY-001
    }

    [Fact]
    public async Task Approve_Twice_422_INVALID_STATE()
    {
        var token = await AdminTokenAsync();
        var tenantId = await Db.Set<Tenant>().Select(t => t.Id).SingleAsync();
        var buyerId = await SeedBuyerAsync(tenantId, "BUY-2X");
        var styleId = await CreateStyleAsync(token, buyerId, "STY-2X");

        Client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        Assert.Equal(HttpStatusCode.OK, (await Client.PostAsync($"/api/v1/pdm/styles/{styleId}/approve", null)).StatusCode);

        var second = await Client.PostAsync($"/api/v1/pdm/styles/{styleId}/approve", null);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, second.StatusCode);
        var problem = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        Assert.Equal("INVALID_STATE", problem.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Approve_MakerCheckerOn_RejectsCreator()
    {
        var token = await AdminTokenAsync();
        var tenant = await Db.Set<Tenant>().SingleAsync();
        tenant.RequireMakerChecker = true; // tenant policy: the creator cannot approve their own style
        await Db.SaveChangesAsync();
        var buyerId = await SeedBuyerAsync(tenant.Id, "BUY-MC");
        var styleId = await CreateStyleAsync(token, buyerId, "STY-MC");

        Client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var rejected = await Client.PostAsync($"/api/v1/pdm/styles/{styleId}/approve", null);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, rejected.StatusCode);
        var problem = JsonDocument.Parse(await rejected.Content.ReadAsStringAsync());
        Assert.Equal("MAKER_CHECKER_VIOLATION", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal("Draft", (await Db.Set<Style>().IgnoreQueryFilters().SingleAsync(s => s.Id == styleId)).Status);

        // Another maker's style approves fine even with the flag on.
        var otherStyleId = await CreateStyleAsync(token, buyerId, "STY-MC2");
        Db.Set<Style>().IgnoreQueryFilters().Single(s => s.Id == otherStyleId).CreatedBy = "maker-1";
        await Db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.OK, (await Client.PostAsync($"/api/v1/pdm/styles/{otherStyleId}/approve", null)).StatusCode);
    }

    [Fact]
    public async Task CrossTenant_StyleId_404()
    {
        var token = await AdminTokenAsync();

        // Another tenant's style, inserted directly — indistinguishable from a missing row. The
        // rival tenant needs its own buyer row: the raw-SQL composite FK (ruling W2-b) enforces it.
        var rivalTenantId = Guid.NewGuid();
        var rivalBuyerId = Guid.NewGuid();
        var otherStyle = new Style
        {
            Id = Guid.NewGuid(),
            TenantId = rivalTenantId,
            Code = "STY-OTH",
            Name = "Rival Style",
            BuyerId = rivalBuyerId,
            Status = "Draft",
            Revision = 1,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "test",
        };
        Db.AddRange(new Tenant
        {
            Id = rivalTenantId,
            Name = "Rival Ltd.",
            Code = "RIV-S",
            DefaultLocale = "en",
            RequireMakerChecker = false,
            Revision = 1,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "test",
        }, new Buyer
        {
            Id = rivalBuyerId,
            TenantId = rivalTenantId,
            Code = "BUY-RIV",
            Name = "Rival Buyer",
            Country = "BD",
            Currency = "USD",
            Status = "Active",
            Revision = 1,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "test",
        }, otherStyle);
        await Db.SaveChangesAsync();

        Client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var detail = await Client.GetAsync($"/api/v1/pdm/styles/{otherStyle.Id}");
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);
        Assert.Equal("application/problem+json", detail.Content.Headers.ContentType?.MediaType);
        Assert.Equal("NOT_FOUND", JsonDocument.Parse(await detail.Content.ReadAsStringAsync()).RootElement.GetProperty("code").GetString());

        var approve = await Client.PostAsync($"/api/v1/pdm/styles/{otherStyle.Id}/approve", null);
        Assert.Equal(HttpStatusCode.NotFound, approve.StatusCode);
    }

    [Fact]
    public async Task Outbox_Audit_PerCommand()
    {
        var token = await AdminTokenAsync();
        var tenantId = await Db.Set<Tenant>().Select(t => t.Id).SingleAsync();
        var buyerId = await SeedBuyerAsync(tenantId, "BUY-EVT");
        var styleId = await CreateStyleAsync(token, buyerId, "STY-EVT");

        // Command 1: create — exactly one outbox + one audit row, same transaction.
        var outbox = Assert.Single(await Db.Set<OutboxMessage>().ToListAsync(), o => o.AggregateId == styleId);
        Assert.Equal("pdm.style.master.creation.changed.v1", outbox.EventType);
        Assert.Equal("Pending", outbox.Status);
        Assert.Equal(tenantId, outbox.TenantId);
        var audit = Assert.Single(await Db.Set<AuditEvent>().ToListAsync(), a => a.EntityId == styleId.ToString());
        Assert.Equal("pdm.style.create", audit.Command);
        Assert.Equal("pdm.style", audit.EntityType);
        Assert.Equal(tenantId, audit.TenantId);

        Client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        Assert.Equal(HttpStatusCode.OK, (await Client.PostAsync($"/api/v1/pdm/styles/{styleId}/approve", null)).StatusCode);

        // Command 2: approve — one more of each, now 2 per aggregate, same event type.
        Assert.Equal(2, await Db.Set<OutboxMessage>().CountAsync(o => o.AggregateId == styleId));
        Assert.Equal(2, await Db.Set<OutboxMessage>().CountAsync(o => o.EventType == "pdm.style.master.creation.changed.v1"));
        Assert.Equal(2, await Db.Set<AuditEvent>().CountAsync(a => a.EntityId == styleId.ToString()));
        Assert.Equal(1, await Db.Set<AuditEvent>().CountAsync(a => a.Command == "pdm.style.approve" && a.EntityId == styleId.ToString()));
    }
}
