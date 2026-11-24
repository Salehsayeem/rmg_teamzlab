using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Rmg.IntegrationTests.Common;

// R-1 platform response/error contract: success envelope {data, meta}, errors RFC 9457 problem+json.
public class EnvelopeTests
{
    private static HttpClient NewClient()
    {
        var factory = new WebApplicationFactory<Program>();
        return factory.CreateClient();
    }

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response)
    {
        var raw = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(raw).RootElement;
    }

    [Fact]
    public async Task BusinessRule_ProducesProblemShape()
    {
        var client = NewClient();

        var response = await client.GetAsync("/devz/probe/business-rule");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        var body = await BodyAsync(response);
        Assert.Equal("STYLE_CODE_TAKEN", body.GetProperty("code").GetString());
        Assert.True(body.TryGetProperty("correlationId", out var correlationId)
            && correlationId.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(correlationId.GetString()));
        Assert.Equal(422, body.GetProperty("status").GetInt32());
        Assert.True(body.TryGetProperty("title", out _));
    }

    [Fact]
    public async Task ValidationProblem_CarriesFieldErrors()
    {
        var client = NewClient();

        var response = await client.GetAsync("/devz/probe/validation");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        var body = await BodyAsync(response);
        Assert.Equal("VALIDATION_FAILED", body.GetProperty("code").GetString());
        var errors = body.GetProperty("errors");
        Assert.Equal(JsonValueKind.Object, errors.ValueKind);
        Assert.True(errors.TryGetProperty("name", out var nameErrors));
        Assert.NotEmpty(nameErrors.EnumerateArray());
    }

    [Fact]
    public async Task CorrelationId_Echoed_WhenProvided()
    {
        var client = NewClient();
        client.DefaultRequestHeaders.Add("X-Correlation-Id", "test-corr-123");

        var response = await client.GetAsync("/devz/probe/correlation");

        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal("test-corr-123",
            string.Join(",", response.Headers.GetValues("X-Correlation-Id")));
    }

    [Fact]
    public async Task Unhandled_Crash_Is500InternalProblem_WithoutStackTrace()
    {
        var client = NewClient();

        var response = await client.GetAsync("/devz/probe/unhandled");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        var raw = await response.Content.ReadAsStringAsync();
        var body = JsonDocument.Parse(raw).RootElement;
        Assert.Equal("INTERNAL_ERROR", body.GetProperty("code").GetString());
        Assert.Equal(500, body.GetProperty("status").GetInt32());
        Assert.DoesNotContain("stackTrace", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("at Rmg", raw);
    }
}
