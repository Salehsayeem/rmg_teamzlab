using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Rmg.IntegrationTests;
using Rmg.Modules.Organization.Domain;

namespace Rmg.SecurityTests;

// Task 7 tenant guard: the TenantEntity global query filter hides other tenants' rows — a cross-tenant
// read is a 404, never a 200/403 leak — and xmin revision concurrency surfaces as 409 with a stable code.
public class TenantGuardTests : IntegrationTestBase
{
    [Fact]
    public async Task CrossTenant_GetById_Returns404()
    {
        var fx = await GuardTestSeed.SeedAsync(Db);
        Client.DefaultRequestHeaders.Authorization = new("Bearer", await GuardTestSeed.LoginAsync(Client, fx.AdminBEmail));

        var cross = await Client.GetAsync($"/guardz/probe/facility/{fx.FacilityA1}");

        Assert.Equal(HttpStatusCode.NotFound, cross.StatusCode);
        Assert.Equal("application/problem+json", cross.Content.Headers.ContentType?.MediaType);

        // Control: the same row IS readable inside its own tenant — the guard hid it, the endpoint is not broken.
        Client.DefaultRequestHeaders.Authorization = new("Bearer", await GuardTestSeed.LoginAsync(Client, fx.AdminAEmail));
        var own = await Client.GetAsync($"/guardz/probe/facility/{fx.FacilityA1}");
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
    }

    [Fact]
    public async Task QueryFilter_ExcludesOtherTenantRows()
    {
        var fx = await GuardTestSeed.SeedAsync(Db);

        Client.DefaultRequestHeaders.Authorization = new("Bearer", await GuardTestSeed.LoginAsync(Client, fx.AdminAEmail));
        var idsA = Ids(await Client.GetAsync("/guardz/probe/facility"));
        Assert.Equal(new HashSet<string> { fx.FacilityA1.ToString(), fx.FacilityA2.ToString() }, idsA);

        Client.DefaultRequestHeaders.Authorization = new("Bearer", await GuardTestSeed.LoginAsync(Client, fx.AdminBEmail));
        var idsB = Ids(await Client.GetAsync("/guardz/probe/facility"));
        Assert.Equal(new HashSet<string> { fx.FacilityB1.ToString() }, idsB);
    }

    [Fact]
    public async Task Write_WithStaleRevision_409()
    {
        var fx = await GuardTestSeed.SeedAsync(Db);
        Client.DefaultRequestHeaders.Authorization = new("Bearer", await GuardTestSeed.LoginAsync(Client, fx.AdminAEmail));

        // A fresh expected revision renames and bumps the row.
        var fresh = await Client.PostAsJsonAsync($"/guardz/probe/facility/{fx.FacilityA1}/rename",
            new { name = "Renamed once", revision = 1 });
        Assert.Equal(HttpStatusCode.OK, fresh.StatusCode);

        // The now-stale expected revision collides with the row's current state: real xmin miss → 409,
        // and the losing write must not land (the concurrent writer's change is what persists).
        var stale = await Client.PostAsJsonAsync($"/guardz/probe/facility/{fx.FacilityA1}/rename",
            new { name = "Renamed on stale revision", revision = 1 });

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("application/problem+json", stale.Content.Headers.ContentType?.MediaType);
        var problem = await stale.Content.ReadFromJsonAsync<ProblemBody>();
        Assert.Equal("FACILITY_REVISION_CONFLICT", problem!.Code);
        Assert.Contains("expected revision 1", problem.Detail);

        var stored = await Db.Set<Facility>().IgnoreQueryFilters().SingleAsync(f => f.Id == fx.FacilityA1);
        Assert.Equal("Renamed on stale revision (rival)", stored.Name);
        Assert.Equal(2, stored.Revision);
    }

    private static HashSet<string> Ids(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(response.Content.ReadAsStringAsync().Result);
        return doc.RootElement.GetProperty("data").EnumerateArray()
            .Select(f => f.GetProperty("id").GetString()!).ToHashSet();
    }

    private sealed record ProblemBody(string Code, string? Detail);
}
