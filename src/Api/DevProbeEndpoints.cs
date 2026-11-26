#if DEBUG
// THROWAWAY dev probes exercising the R-1 envelope/problem/correlation contract. Deleted by a later task — do not build features on these.
using Rmg.BuildingBlocks.Common;

namespace Rmg.Api;

public static class DevProbeEndpoints
{
    public static void MapDevProbeEndpoints(this WebApplication app)
    {
        app.MapGet("/devz/probe/business-rule", () => Throw(new BusinessRuleProblem("STYLE_CODE_TAKEN", "dup")));
        app.MapGet("/devz/probe/validation",
            () => Throw(new ValidationProblem(new Dictionary<string, string[]> { ["name"] = ["is required"] })));
        app.MapGet("/devz/probe/correlation", () => Envelope<string>.Of("pong"));
        app.MapGet("/devz/probe/unhandled", () => Crash());

        static object Throw(RmgProblemException problem) => throw problem;
        static object Crash() => throw new InvalidOperationException("boom");
    }
}
#endif
