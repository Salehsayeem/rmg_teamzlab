using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Rmg.BuildingBlocks.Common;
using Rmg.BuildingBlocks.Idempotency;
using Rmg.Modules.Organization.Domain;

namespace Rmg.IntegrationTests;

// Task 7: the [Idempotent] filter — an identical Idempotency-Key + body replays the stored response
// without re-executing the write; the same key with a different body is 422 IDEMPOTENCY_KEY_REUSED.
public class IdempotencyTests : IntegrationTestBase
{
    [Fact]
    public async Task Idempotent_Replay_SameResponse_NoSecondWrite()
    {
        var fx = await GuardTestSeed.SeedAsync(Db);
        Client.DefaultRequestHeaders.Authorization = new("Bearer", await GuardTestSeed.LoginAsync(Client, fx.AdminAEmail));
        Client.DefaultRequestHeaders.Add("Idempotency-Key", "idem-replay-1");

        var body = FacilityBody(fx, "FAC-IDEM");
        var first = await Client.PostAsJsonAsync("/guardz/probe/facility", body);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var firstJson = await first.Content.ReadAsStringAsync();
        Assert.False(first.Headers.Contains("Idempotent-Replay"));
        Assert.Equal(3, await GuardTestSeed.CountFacilitiesAsync(Db, fx.TenantA)); // FAC-01 + FAC-A2 + FAC-IDEM

        var replay = await Client.PostAsJsonAsync("/guardz/probe/facility", body);

        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        Assert.Equal("true", replay.Headers.GetValues("Idempotent-Replay").Single());
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(firstJson), JsonNode.Parse(await replay.Content.ReadAsStringAsync())));
        Assert.Equal(3, await GuardTestSeed.CountFacilitiesAsync(Db, fx.TenantA)); // no second write
    }

    [Fact]
    public async Task Idempotent_KeyReuse_DifferentBody_422()
    {
        var fx = await GuardTestSeed.SeedAsync(Db);
        Client.DefaultRequestHeaders.Authorization = new("Bearer", await GuardTestSeed.LoginAsync(Client, fx.AdminAEmail));
        Client.DefaultRequestHeaders.Add("Idempotency-Key", "idem-reuse-1");

        var first = await Client.PostAsJsonAsync("/guardz/probe/facility", FacilityBody(fx, "FAC-REUSE"));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(3, await GuardTestSeed.CountFacilitiesAsync(Db, fx.TenantA));

        var reuse = await Client.PostAsJsonAsync("/guardz/probe/facility", FacilityBody(fx, "FAC-OTHER"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, reuse.StatusCode);
        Assert.Equal("application/problem+json", reuse.Content.Headers.ContentType?.MediaType);
        var problem = await reuse.Content.ReadFromJsonAsync<ProblemBody>();
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", problem!.Code);
        Assert.Equal(3, await GuardTestSeed.CountFacilitiesAsync(Db, fx.TenantA)); // the reuse wrote nothing
    }

    [Fact]
    public async Task Idempotent_ConcurrentSameKey_OneWrite_OtherConflicts()
    {
        var fx = await GuardTestSeed.SeedAsync(Db);
        Client.DefaultRequestHeaders.Authorization = new("Bearer", await GuardTestSeed.LoginAsync(Client, fx.AdminAEmail));
        Client.DefaultRequestHeaders.Add("Idempotency-Key", "idem-race-1");

        var body = FacilityBody(fx, "FAC-RACE");
        var responses = await Task.WhenAll(
            Client.PostAsJsonAsync("/guardz/probe/facility", body),
            Client.PostAsJsonAsync("/guardz/probe/facility", body));

        // Exactly one write effect and no 500-after-commit: the racing duplicate is refused in flight
        // (409) or — if the server happened to serialize them — replays the winner's response (201+flag).
        Assert.All(responses, r => Assert.True(
            (int)r.StatusCode is 201 or 409, $"unexpected status {(int)r.StatusCode}"));
        Assert.True(responses.Any(r => (int)r.StatusCode == 201));
        Assert.Equal(3, await GuardTestSeed.CountFacilitiesAsync(Db, fx.TenantA)); // FAC-01 + FAC-A2 + one FAC-RACE
        if (responses.Count(r => (int)r.StatusCode == 201) == 2)
            Assert.Single(responses.Where(r => r.Headers.Contains("Idempotent-Replay")));
    }

    [Fact]
    public async Task Idempotent_PendingClaim_409_InFlight()
    {
        var fx = await GuardTestSeed.SeedAsync(Db);
        Client.DefaultRequestHeaders.Authorization = new("Bearer", await GuardTestSeed.LoginAsync(Client, fx.AdminAEmail));
        Client.DefaultRequestHeaders.Add("Idempotency-Key", "idem-pending-1");

        var first = await Client.PostAsJsonAsync("/guardz/probe/facility", FacilityBody(fx, "FAC-PENDING"));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        // Rewind the stored claim to Pending, as if its owner were still executing.
        var claim = await Db.Set<IdempotencyRecord>().SingleAsync();
        claim.ResponseStatus = 0;
        claim.ResponseJson = "null";
        await Db.SaveChangesAsync();

        var second = await Client.PostAsJsonAsync("/guardz/probe/facility", FacilityBody(fx, "FAC-PENDING"));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("application/problem+json", second.Content.Headers.ContentType?.MediaType);
        var problem = await second.Content.ReadFromJsonAsync<ProblemBody>();
        Assert.Equal("IDEMPOTENCY_KEY_IN_FLIGHT", problem!.Code);
        Assert.Equal(3, await GuardTestSeed.CountFacilitiesAsync(Db, fx.TenantA)); // nothing executed
    }

    [Fact]
    public async Task Idempotent_EmptyBody200_Stores_And_Replays()
    {
        var fx = await GuardTestSeed.SeedAsync(Db);
        Client.DefaultRequestHeaders.Authorization = new("Bearer", await GuardTestSeed.LoginAsync(Client, fx.AdminAEmail));
        Client.DefaultRequestHeaders.Add("Idempotency-Key", "idem-empty-1");

        var first = await Client.PostAsJsonAsync($"/guardz/probe/facility/{fx.FacilityA1}/touch", new { revision = 1 });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(string.Empty, await first.Content.ReadAsStringAsync());
        Assert.False(first.Headers.Contains("Idempotent-Replay"));
        Assert.Equal(2, await RevisionOfAsync(fx.FacilityA1)); // the write effect happened

        var replay = await Client.PostAsJsonAsync($"/guardz/probe/facility/{fx.FacilityA1}/touch", new { revision = 1 });

        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.True(replay.Headers.Contains("Idempotent-Replay"));
        Assert.Equal(string.Empty, await replay.Content.ReadAsStringAsync());
        Assert.Equal(2, await RevisionOfAsync(fx.FacilityA1)); // replayed, not re-executed
    }

    [Fact]
    public async Task Idempotent_NonIResult_Passthrough_Uncaptured()
    {
        var fx = await GuardTestSeed.SeedAsync(Db);
        Client.DefaultRequestHeaders.Authorization = new("Bearer", await GuardTestSeed.LoginAsync(Client, fx.AdminAEmail));
        Client.DefaultRequestHeaders.Add("Idempotency-Key", "idem-poco-1");

        // A bare POCO return passes through uncaptured: both calls really execute and return the full
        // body — no bodyless false replay, and no claim is stored.
        var first = await Client.PostAsJsonAsync("/guardz/probe/poco", new { });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstBody = await first.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrWhiteSpace(firstBody));
        Assert.False(first.Headers.Contains("Idempotent-Replay"));

        var second = await Client.PostAsJsonAsync("/guardz/probe/poco", new { });
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.False(second.Headers.Contains("Idempotent-Replay"));

        Assert.Equal(firstBody, await second.Content.ReadAsStringAsync());
        Assert.False(await Db.Set<IdempotencyRecord>().AnyAsync()); // no bogus stored claim
    }

    private async Task<int> RevisionOfAsync(Guid facilityId) =>
        await Db.Set<Facility>().IgnoreQueryFilters().Where(f => f.Id == facilityId).Select(f => f.Revision).SingleAsync();

    private static JsonObject FacilityBody(GuardTestSeed.GuardFixture fx, string code) => new()
    {
        ["legalEntityId"] = fx.LegalEntityA.ToString(),
        ["code"] = code,
        ["name"] = $"Facility {code}",
    };

    private sealed record ProblemBody(string Code);
}
