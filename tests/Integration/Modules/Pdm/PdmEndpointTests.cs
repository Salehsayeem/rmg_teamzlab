using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Rmg.BuildingBlocks.Common;
using Rmg.Database;
using Rmg.Modules.ProductEngineering.Contracts;

namespace Rmg.IntegrationTests;

// Task 11 (PDM): material master + UOM reference data. Materials are tenant-scoped (context-rooted
// query filter) and validated on create — unknown baseUomCode is 422 UOM_INVALID, a taken code is
// 422 DUPLICATE_CODE. The six UOMs come from the 003_PdmMaster migration seed (scratch databases
// are migrated but NOT seeded), so the uoms list proves the migration data too.
public class PdmEndpointTests : IntegrationTestBase
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

    private async Task<HttpResponseMessage> PostMaterialAsync(string token, object body)
    {
        Client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return await Client.PostAsJsonAsync("/api/v1/pdm/materials", body);
    }

    [Fact]
    public async Task CreateMaterial_Happy()
    {
        var token = await AdminTokenAsync();

        var response = await PostMaterialAsync(token,
            new MaterialCreateRequest("RM-001", "Cotton Fabric", "Fabric", "KG"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<Envelope<MaterialDto>>();
        Assert.NotNull(created);
        Assert.Equal("RM-001", created!.Data.Code);
        Assert.Equal("Cotton Fabric", created.Data.Name);
        Assert.Equal("Fabric", created.Data.Category);
        Assert.Equal("KG", created.Data.BaseUomCode);

        // Readable again through the tenant-scoped list endpoint.
        Client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var list = await Client.GetAsync("/api/v1/pdm/materials");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var page = await list.Content.ReadFromJsonAsync<Envelope<MaterialDto[]>>();
        var row = Assert.Single(page!.Data, m => m.Code == "RM-001");
        Assert.Equal(created.Data.Id, row.Id);
        Assert.Equal("KG", row.BaseUomCode);
    }

    [Fact]
    public async Task CreateMaterial_BadUom_422_UOM_INVALID()
    {
        var token = await AdminTokenAsync();

        var response = await PostMaterialAsync(token,
            new MaterialCreateRequest("RM-BAD", "Mystery Fabric", "Fabric", "XX"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal("UOM_INVALID", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal(422, problem.RootElement.GetProperty("status").GetInt32());
        var errors = problem.RootElement.GetProperty("errors").GetProperty("baseUomCode");
        Assert.Equal(["XX"], errors.EnumerateArray().Select(e => e.GetString()).ToArray());

        // Nothing persisted.
        Client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var list = await Client.GetAsync("/api/v1/pdm/materials");
        var page = await list.Content.ReadFromJsonAsync<Envelope<MaterialDto[]>>();
        Assert.Empty(page!.Data);
    }

    [Fact]
    public async Task DuplicateCode_422()
    {
        var token = await AdminTokenAsync();

        var first = await PostMaterialAsync(token,
            new MaterialCreateRequest("RM-DUP", "First", null, "PC"));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await PostMaterialAsync(token,
            new MaterialCreateRequest("RM-DUP", "Second", null, "PC"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, second.StatusCode);
        var problem = await JsonDocument.ParseAsync(await second.Content.ReadAsStreamAsync());
        Assert.Equal("DUPLICATE_CODE", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal(422, problem.RootElement.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Uoms_List_Seeded6()
    {
        // Scratch databases are migrated but never seeded — the six rows here come from the
        // 003_PdmMaster migration's InsertData, not Seeder.cs. Login needs the pilot seed.
        var token = await AdminTokenAsync();
        Client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var response = await Client.GetAsync("/api/v1/pdm/uoms");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Envelope<UomDto[]>>();
        Assert.NotNull(body);
        Assert.Equal(6, body!.Data.Length);
        Assert.Equal(
            new HashSet<string> { "PC", "KG", "M", "YD", "M2", "DOZ" },
            body.Data.Select(u => u.Code).ToHashSet());
        Assert.All(body.Data, u => Assert.False(string.IsNullOrWhiteSpace(u.Name)));
    }
}
