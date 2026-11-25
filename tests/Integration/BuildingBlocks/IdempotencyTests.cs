using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Rmg.BuildingBlocks.Common;

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

    private static JsonObject FacilityBody(GuardTestSeed.GuardFixture fx, string code) => new()
    {
        ["legalEntityId"] = fx.LegalEntityA.ToString(),
        ["code"] = code,
        ["name"] = $"Facility {code}",
    };

    private sealed record ProblemBody(string Code);
}
