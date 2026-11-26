using System.Net;
using System.Net.Http.Json;
using Rmg.BuildingBlocks.Common;
using Rmg.Modules.Organization.Contracts;
using Rmg.Database;

namespace Rmg.IntegrationTests;

// Task 8 (ORG): facility read endpoints — the listing is scoped to the caller's tenant over the pilot
// seed, and a cross-tenant id is indistinguishable from a missing row (404 NOT_FOUND problem+json).
public class FacilityEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task Facilities_List_ScopedAndSeeded()
    {
        // Pilot TZL seed (FAC-01) + the two-tenant guard fixture (adds FAC-A2 beside it, tenant TNB).
        await Seeder.SeedAsync(Db);
        var fx = await GuardTestSeed.SeedAsync(Db);
        Client.DefaultRequestHeaders.Authorization = new("Bearer", await GuardTestSeed.LoginAsync(Client, fx.AdminAEmail));

        var response = await Client.GetAsync("/api/v1/org/facilities");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Envelope<FacilityDto[]>>();
        Assert.NotNull(body);
        // The seeded FAC-01 comes back with its real values.
        var fac01 = Assert.Single(body!.Data, f => f.Code == "FAC-01");
        Assert.Equal(fx.FacilityA1, fac01.Id);
        Assert.Equal("TeamzLab Demo Facility", fac01.Name);
        Assert.Equal("Asia/Dhaka", fac01.Timezone);
        Assert.Null(fac01.Address);
        // Scoped: exactly tenant A's facilities — tenant B's FAC-B1 never appears.
        Assert.Equal(new HashSet<Guid> { fx.FacilityA1, fx.FacilityA2 }, body.Data.Select(f => f.Id).ToHashSet());
    }

    [Fact]
    public async Task Facilities_GetOtherTenantId_404()
    {
        await Seeder.SeedAsync(Db);
        var fx = await GuardTestSeed.SeedAsync(Db);
        Client.DefaultRequestHeaders.Authorization = new("Bearer", await GuardTestSeed.LoginAsync(Client, fx.AdminAEmail));

        var response = await Client.GetAsync($"/api/v1/org/facilities/{fx.FacilityB1}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemBody>();
        Assert.Equal("NOT_FOUND", problem!.Code);
    }

    // Control for the 404 test: the same route serves the caller's own facility.
    [Fact]
    public async Task Facilities_GetById_ReturnsOwnFacility()
    {
        await Seeder.SeedAsync(Db);
        var fx = await GuardTestSeed.SeedAsync(Db);
        Client.DefaultRequestHeaders.Authorization = new("Bearer", await GuardTestSeed.LoginAsync(Client, fx.AdminAEmail));

        var response = await Client.GetAsync($"/api/v1/org/facilities/{fx.FacilityA1}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Envelope<FacilityDto>>();
        Assert.NotNull(body);
        Assert.Equal(fx.FacilityA1, body!.Data.Id);
        Assert.Equal("FAC-01", body.Data.Code);
    }

    private sealed record ProblemBody(string Code);
}
