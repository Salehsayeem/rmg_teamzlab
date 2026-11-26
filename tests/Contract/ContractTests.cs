using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Rmg.ContractTests;

// Task 15: the committed OpenAPI snapshot (contracts/openapi/openapi.json) must equal the document
// the live API serves at GET /openapi/v1.json. The document is regenerated in-memory through the
// same WebApplicationFactory harness the integration suite uses (the pinned net11.0 stack carries
// no Microsoft.AspNetCore.OpenApi package, so the export is served by the Api's own generator).
public sealed class OpenApiContractTests
{
    // Every path the pilot surface (Tasks 8-14) must expose: auth login (Task 4), org facilities
    // (W1), iam users/roles (Task 9), ord buyers (Task 10), pdm materials/uoms/styles/boms (11-13),
    // ord customer-orders lifecycle (Task 14).
    private static readonly string[] RequiredPaths =
    {
        "/api/v1/auth/login",
        "/api/v1/org/facilities",
        "/api/v1/org/facilities/{id}",
        "/api/v1/iam/users",
        "/api/v1/iam/users/{id}/roles",
        "/api/v1/iam/roles",
        "/api/v1/ord/buyers",
        "/api/v1/ord/buyers/{id}",
        "/api/v1/pdm/uoms",
        "/api/v1/pdm/materials",
        "/api/v1/pdm/styles",
        "/api/v1/pdm/styles/{id}",
        "/api/v1/pdm/styles/{id}/approve",
        "/api/v1/pdm/boms",
        "/api/v1/pdm/boms/{id}",
        "/api/v1/pdm/boms/{id}/lines",
        "/api/v1/pdm/boms/{id}/approve",
        "/api/v1/ord/customer-orders",
        "/api/v1/ord/customer-orders/{id}",
        "/api/v1/ord/customer-orders/{id}/confirm",
        "/api/v1/ord/customer-orders/{id}/cancel",
    };

    [Fact]
    public async Task OpenAPI_MatchesCommittedFile()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b => b.UseEnvironment("Development"));
        var client = factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        var served = await response.Content.ReadAsStringAsync();

        var doc = JsonNode.Parse(served)!.AsObject();
        var paths = doc["paths"]!.AsObject();

        foreach (var path in RequiredPaths)
            Assert.True(paths.ContainsKey(path), $"served document is missing path {path}");

        foreach (var (path, item) in paths)
        {
            foreach (var (_, operation) in item!.AsObject())
            {
                var op = operation!.AsObject();
                Assert.False(string.IsNullOrWhiteSpace(op["operationId"]?.ToString()),
                    $"operation on {path} lacks an operationId");

                var documentsProblemJson = op["responses"]!.AsObject()
                    .Any(r => r.Value?["content"]?.AsObject().ContainsKey("application/problem+json") == true);
                Assert.True(documentsProblemJson, $"operation on {path} does not document application/problem+json");
            }
        }

        // Canonical comparison: key order and indentation are not part of the contract.
        var committed = await File.ReadAllTextAsync(CommittedSnapshotPath());
        Assert.Equal(Canonical(committed), Canonical(served));
    }

    private static string CommittedSnapshotPath()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "contracts", "openapi", "openapi.json");
            if (File.Exists(candidate)) return candidate;
        }

        throw new InvalidOperationException(
            "contracts/openapi/openapi.json not found — run scripts/export-openapi.ps1 to produce it.");
    }

    private static string Canonical(string json)
    {
        var node = Canonicalize(JsonNode.Parse(json));
        return node?.ToJsonString(new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
        }) ?? "null";
    }

    // Recursively rebuilds the tree with object keys sorted (arrays keep their order), so the
    // comparison is insensitive to how either side happened to serialize the document.
    private static JsonNode? Canonicalize(JsonNode? node) => node switch
    {
        null => null,
        JsonObject o => new JsonObject(o.OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => KeyValuePair.Create(p.Key, Canonicalize(p.Value)))),
        JsonArray a => new JsonArray(a.Select(Canonicalize).ToArray()),
        _ => node.DeepClone(), // leaves cannot be re-parented into the rebuilt tree
    };
}
