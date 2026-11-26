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
using Rmg.Modules.ProductEngineering.Domain;

namespace Rmg.IntegrationTests;

// Task 13 (PDM): BOM authoring — one BOM per style version; lines only while Draft (uom + material
// validated → 422 BOM_LINE_INVALID_UOM / MATERIAL_INVALID, qty > 0 → QTY_POSITIVE); release needs
// ≥1 line and pdm.bom.approve. Every command emits one outbox + one audit row in its own transaction.
// Direct row asserts use IgnoreQueryFilters(): the entities' fail-closed filter only re-roots on
// TenantScopedDbContext, while the harness reads on the unfiltered-for-them base RmgDbContext.
public class BomEndpointTests : IntegrationTestBase
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

    private async Task<Guid> SeedBuyerAsync(Guid tenantId)
    {
        var buyer = new Buyer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = "BUY-BOM",
            Name = "BOM Test Buyer",
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

    private async Task<Guid> SeedMaterialAsync(Guid tenantId, string code = "RM-BOM1")
    {
        var material = new Material
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = code,
            Name = "Cotton Fabric",
            BaseUomCode = "KG", // one of the six UOMs seeded by 003_PdmMaster
            Revision = 1,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "test",
        };
        Db.Add(material);
        await Db.SaveChangesAsync();
        return material.Id;
    }

    // Style + draft version 1 via the API (the BOM's FK target; the style FK needs a buyer row).
    private async Task<(Guid StyleId, Guid VersionId)> CreateStyleVersionAsync(string token, Guid buyerId, string code)
    {
        Client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var response = await Client.PostAsJsonAsync("/api/v1/pdm/styles", new
        {
            code,
            name = "Polo Shirt",
            buyerId,
            variants = new[] { new { colorCode = "NAVY", sizeCode = "L" } },
        });
        Assert.True(response.StatusCode == HttpStatusCode.Created,
            $"style create failed: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        var data = (await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync()))
            .RootElement.GetProperty("data");
        return (data.GetProperty("id").GetGuid(), data.GetProperty("versions")[0].GetProperty("id").GetGuid());
    }

    private async Task<Guid> CreateDraftBomAsync(string token, Guid styleVersionId)
    {
        Client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var response = await Client.PostAsJsonAsync("/api/v1/pdm/boms", new { styleVersionId });
        Assert.True(response.StatusCode == HttpStatusCode.Created,
            $"bom create failed: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return (await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync()))
            .RootElement.GetProperty("data").GetProperty("id").GetGuid();
    }

    private async Task<HttpResponseMessage> AddLineAsync(string token, Guid bomId, object payload)
    {
        Client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return await Client.PostAsJsonAsync($"/api/v1/pdm/boms/{bomId}/lines", payload);
    }

    private static async Task<JsonElement> ProblemAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    [Fact]
    public async Task BomLifecycle_AddLine_Release()
    {
        var token = await AdminTokenAsync();
        var tenantId = await Db.Set<Tenant>().Select(t => t.Id).SingleAsync();
        var buyerId = await SeedBuyerAsync(tenantId);
        var (_, versionId) = await CreateStyleVersionAsync(token, buyerId, "STY-BOM");
        var materialId = await SeedMaterialAsync(tenantId);

        var created = await Client.PostAsJsonAsync("/api/v1/pdm/boms", new { styleVersionId = versionId });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var bomData = (await JsonDocument.ParseAsync(await created.Content.ReadAsStreamAsync()))
            .RootElement.GetProperty("data");
        var bomId = bomData.GetProperty("id").GetGuid();
        Assert.Equal("Draft", bomData.GetProperty("status").GetString());
        Assert.Equal(versionId, bomData.GetProperty("styleVersionId").GetGuid());

        var line = await AddLineAsync(token, bomId, new { materialId, qtyPerUnit = 1.5m, uomCode = "KG", wastePct = 5m });
        Assert.Equal(HttpStatusCode.Created, line.StatusCode);
        var lineData = (await JsonDocument.ParseAsync(await line.Content.ReadAsStreamAsync())).RootElement.GetProperty("data");
        Assert.Equal(1.5m, lineData.GetProperty("qtyPerUnit").GetDecimal());
        Assert.Equal("KG", lineData.GetProperty("uomCode").GetString());
        Assert.Equal(5m, lineData.GetProperty("wastePct").GetDecimal());
        Assert.Equal(materialId, lineData.GetProperty("materialId").GetGuid());

        Client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var released = await Client.PostAsync($"/api/v1/pdm/boms/{bomId}/approve", null);

        Assert.Equal(HttpStatusCode.OK, released.StatusCode);
        var releasedData = (await JsonDocument.ParseAsync(await released.Content.ReadAsStreamAsync()))
            .RootElement.GetProperty("data");
        Assert.Equal("Released", releasedData.GetProperty("status").GetString());
        Assert.Equal(1, releasedData.GetProperty("lines").GetArrayLength());

        var bomRow = await Db.Set<Bom>().IgnoreQueryFilters().SingleAsync(b => b.Id == bomId);
        Assert.Equal("Released", bomRow.Status);
        Assert.Equal(1, await Db.Set<BomLine>().IgnoreQueryFilters().CountAsync(l => l.BomId == bomId));

        // Three commands → three outbox rows + three audit rows, all BOM events on the BOM aggregate.
        var outboxRows = await Db.Set<OutboxMessage>().Where(o => o.AggregateId == bomId).ToListAsync();
        Assert.Equal(3, outboxRows.Count);
        Assert.All(outboxRows, o => Assert.Equal("pdm.bill.of.materials.authoring.changed.v1", o.EventType));
        Assert.All(outboxRows, o => Assert.Equal("Pending", o.Status));
        var auditRows = await Db.Set<AuditEvent>().Where(a => a.EntityId == bomId.ToString()).OrderBy(a => a.OccurredAtUtc).ToListAsync();
        Assert.Equal(["pdm.bom.create", "pdm.bom.add_line", "pdm.bom.release"], auditRows.Select(a => a.Command).ToArray());
        Assert.All(auditRows, a => Assert.Equal("pdm.bom", a.EntityType));
    }

    [Fact]
    public async Task BomLine_ZeroQty_422_QTY_POSITIVE()
    {
        var token = await AdminTokenAsync();
        var tenantId = await Db.Set<Tenant>().Select(t => t.Id).SingleAsync();
        var buyerId = await SeedBuyerAsync(tenantId);
        var (_, versionId) = await CreateStyleVersionAsync(token, buyerId, "STY-QTY");
        var materialId = await SeedMaterialAsync(tenantId, "RM-QTY");
        var bomId = await CreateDraftBomAsync(token, versionId);

        var response = await AddLineAsync(token, bomId, new { materialId, qtyPerUnit = 0m, uomCode = "KG" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await ProblemAsync(response);
        Assert.Equal("QTY_POSITIVE", problem.GetProperty("code").GetString());
        Assert.Equal(422, problem.GetProperty("status").GetInt32());
        Assert.Equal(0, await Db.Set<BomLine>().IgnoreQueryFilters().CountAsync(l => l.BomId == bomId)); // scoped: the demo dataset seeds its own BOM lines
    }

    [Fact]
    public async Task BomLine_BadUomOrMaterial_422()
    {
        var token = await AdminTokenAsync();
        var tenantId = await Db.Set<Tenant>().Select(t => t.Id).SingleAsync();
        var buyerId = await SeedBuyerAsync(tenantId);
        var (_, versionId) = await CreateStyleVersionAsync(token, buyerId, "STY-UOM");
        var bomId = await CreateDraftBomAsync(token, versionId);

        var badUom = await AddLineAsync(token, bomId, new { materialId = Guid.NewGuid(), qtyPerUnit = 1m, uomCode = "XX" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, badUom.StatusCode);
        Assert.Equal("BOM_LINE_INVALID_UOM", (await ProblemAsync(badUom)).GetProperty("code").GetString());

        var badMaterial = await AddLineAsync(token, bomId, new { materialId = Guid.NewGuid(), qtyPerUnit = 1m, uomCode = "KG" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, badMaterial.StatusCode);
        Assert.Equal("MATERIAL_INVALID", (await ProblemAsync(badMaterial)).GetProperty("code").GetString());

        Assert.Equal(0, await Db.Set<BomLine>().IgnoreQueryFilters().CountAsync(l => l.BomId == bomId)); // scoped: the demo dataset seeds its own BOM lines
    }

    [Fact]
    public async Task BomLine_AfterRelease_422_INVALID_STATE()
    {
        var token = await AdminTokenAsync();
        var tenantId = await Db.Set<Tenant>().Select(t => t.Id).SingleAsync();
        var buyerId = await SeedBuyerAsync(tenantId);
        var (_, versionId) = await CreateStyleVersionAsync(token, buyerId, "STY-FROZEN");
        var materialId = await SeedMaterialAsync(tenantId, "RM-FRZ");
        var bomId = await CreateDraftBomAsync(token, versionId);
        Assert.Equal(HttpStatusCode.Created,
            (await AddLineAsync(token, bomId, new { materialId, qtyPerUnit = 2m, uomCode = "KG" })).StatusCode);
        Client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        Assert.Equal(HttpStatusCode.OK, (await Client.PostAsync($"/api/v1/pdm/boms/{bomId}/approve", null)).StatusCode);

        var late = await AddLineAsync(token, bomId, new { materialId, qtyPerUnit = 3m, uomCode = "KG" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, late.StatusCode);
        Assert.Equal("INVALID_STATE", (await ProblemAsync(late)).GetProperty("code").GetString());
        Assert.Equal(1, await Db.Set<BomLine>().IgnoreQueryFilters().CountAsync(l => l.BomId == bomId)); // scoped: the demo dataset seeds its own BOM lines
    }

    [Fact]
    public async Task Bom_Release_WithoutLines_422_NO_LINES()
    {
        var token = await AdminTokenAsync();
        var tenantId = await Db.Set<Tenant>().Select(t => t.Id).SingleAsync();
        var buyerId = await SeedBuyerAsync(tenantId);
        var (_, versionId) = await CreateStyleVersionAsync(token, buyerId, "STY-EMPTY");
        var bomId = await CreateDraftBomAsync(token, versionId);

        Client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var response = await Client.PostAsync($"/api/v1/pdm/boms/{bomId}/approve", null);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("NO_LINES", (await ProblemAsync(response)).GetProperty("code").GetString());
        Assert.Equal("Draft", (await Db.Set<Bom>().IgnoreQueryFilters().SingleAsync(b => b.Id == bomId)).Status);
    }

    [Fact]
    public async Task Bom_SecondForSameVersion_422()
    {
        var token = await AdminTokenAsync();
        var tenantId = await Db.Set<Tenant>().Select(t => t.Id).SingleAsync();
        var buyerId = await SeedBuyerAsync(tenantId);
        var (_, versionId) = await CreateStyleVersionAsync(token, buyerId, "STY-ONEBOM");
        Assert.Equal(HttpStatusCode.Created, (await Client.PostAsJsonAsync("/api/v1/pdm/boms", new { styleVersionId = versionId })).StatusCode);

        Client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var second = await Client.PostAsJsonAsync("/api/v1/pdm/boms", new { styleVersionId = versionId });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, second.StatusCode);
        Assert.Equal("BOM_EXISTS", (await ProblemAsync(second)).GetProperty("code").GetString());
        Assert.Equal(1, await Db.Set<Bom>().IgnoreQueryFilters().CountAsync(b => b.StyleVersionId == versionId)); // scoped: the demo dataset seeds its own BOM
    }

    [Fact]
    public async Task BomRelease_PermRequired_403()
    {
        var token = await AdminTokenAsync();
        var tenantId = await Db.Set<Tenant>().Select(t => t.Id).SingleAsync();
        var buyerId = await SeedBuyerAsync(tenantId);
        var (_, versionId) = await CreateStyleVersionAsync(token, buyerId, "STY-PERM");
        var materialId = await SeedMaterialAsync(tenantId, "RM-PRM");
        var bomId = await CreateDraftBomAsync(token, versionId);
        Assert.Equal(HttpStatusCode.Created,
            (await AddLineAsync(token, bomId, new { materialId, qtyPerUnit = 1m, uomCode = "KG" })).StatusCode);

        // A BOM editor: read/create/edit but NOT approve — release must be forbidden.
        var hasher = new PasswordHasher<AppUser>();
        var editor = new AppUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = "bom-editor@tzl.demo",
            DisplayName = "BOM Editor",
            IsActive = true,
            Revision = 1,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "test",
        };
        editor.PasswordHash = hasher.HashPassword(editor, AdminPassword);
        var role = new Role
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = "BomEditor",
            Revision = 1,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "test",
        };
        Db.AddRange(editor, role,
            new RolePermission { RoleId = role.Id, PermissionCode = "pdm.bom.read" },
            new RolePermission { RoleId = role.Id, PermissionCode = "pdm.bom.create" },
            new RolePermission { RoleId = role.Id, PermissionCode = "pdm.bom.edit" },
            new UserRole { TenantId = tenantId, UserId = editor.Id, RoleId = role.Id });
        await Db.SaveChangesAsync();

        var login = await Client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = editor.Email, password = AdminPassword });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var editorToken = (await JsonDocument.ParseAsync(await login.Content.ReadAsStreamAsync()))
            .RootElement.GetProperty("data").GetProperty("accessToken").GetString();

        Client.DefaultRequestHeaders.Authorization = new("Bearer", editorToken);
        var response = await Client.PostAsync($"/api/v1/pdm/boms/{bomId}/approve", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("Draft", (await Db.Set<Bom>().IgnoreQueryFilters().SingleAsync(b => b.Id == bomId)).Status);
    }
}
