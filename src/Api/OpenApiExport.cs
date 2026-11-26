using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.ApiExplorer;

namespace Rmg.Api;

// Task 15: OpenAPI export. The pinned net11.0 stack carries no Microsoft.AspNetCore.OpenApi package
// (not in the shared framework, not referenced), so the document is generated here from ApiExplorer
// (IApiDescriptionGroupCollectionProvider — covers the MVC controllers and the minimal-API login).
// Deterministic by construction: paths, operation keys, parameters and schema properties are all
// sorted; operationIds follow a mechanical convention (verb + PascalCased path, {param} → By{Param});
// every operation documents the R-1 error envelope as a default application/problem+json response.
// Served at /openapi/v1.json (Development only); scripts/export-openapi.ps1 snapshots it into
// contracts/openapi/openapi.json, which tests/Contract compares against this live document.
public static class OpenApiExport
{
    public static IEndpointConventionBuilder MapOpenApiExport(this WebApplication app) =>
        app.MapGet("/openapi/v1.json", (IApiDescriptionGroupCollectionProvider api) =>
            Results.Text(BuildDocument(api.ApiDescriptionGroups), "application/vnd.oai.openapi+json"));

    internal static string BuildDocument(ApiDescriptionGroupCollection groups)
    {
        var schemas = new SortedDictionary<string, JsonObject>(StringComparer.Ordinal);
        var paths = new SortedDictionary<string, JsonObject>(StringComparer.Ordinal);

        foreach (var group in groups.Items)
            foreach (var desc in group?.Items ?? [])
            {
                if (desc.RelativePath is not { } relative || !relative.StartsWith("api/v1/", StringComparison.Ordinal))
                    continue; // pilot contract only: skip "/" and the devz/authz/guardz probe endpoints
                var path = "/" + relative; // ApiExplorer paths are relative; OpenAPI paths must start with "/"
                var method = desc.HttpMethod?.ToUpperInvariant() ?? "GET";
                (paths.TryGetValue(path, out var item) ? item : paths[path] = new JsonObject())[method.ToLowerInvariant()] =
                    Operation(desc, method, relative, schemas);
            }

        // R-1 error envelope component, referenced as the default response of every operation.
        schemas.TryAdd("ProblemDetails", new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["type"] = new JsonObject { ["type"] = "string", ["format"] = "uri" },
                ["title"] = new JsonObject { ["type"] = "string" },
                ["status"] = new JsonObject { ["type"] = "integer", ["format"] = "int32", ["nullable"] = true },
                ["detail"] = new JsonObject { ["type"] = "string", ["nullable"] = true },
                ["instance"] = new JsonObject { ["type"] = "string", ["format"] = "uri-reference", ["nullable"] = true },
                ["code"] = new JsonObject { ["type"] = "string", ["description"] = "stable machine-readable error code" },
                ["errors"] = new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = new JsonObject
                    {
                        ["type"] = "array",
                        ["items"] = new JsonObject { ["type"] = "string" },
                    },
                    ["description"] = "per-field validation messages, when the problem is a validation failure",
                },
                ["correlationId"] = new JsonObject { ["type"] = "string", ["description"] = "correlation id echoed by CorrelationMiddleware" },
            },
        });

        var document = new JsonObject
        {
            ["openapi"] = "3.0.1",
            ["info"] = new JsonObject
            {
                ["title"] = "Rmg.Api",
                ["version"] = "v1",
                ["description"] = "RMG platform pilot API — {data, meta} envelope on success, RFC 9457 problem+json on errors.",
            },
            ["paths"] = new JsonObject(paths.Select(p => KeyValuePair.Create(p.Key, (JsonNode?)p.Value))),
            ["components"] = new JsonObject
            {
                ["schemas"] = new JsonObject(schemas.Select(p => KeyValuePair.Create(p.Key, (JsonNode?)p.Value))),
            },
        };
        return document.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }

    private static JsonObject Operation(ApiDescription desc, string method, string relative,
        IDictionary<string, JsonObject> schemas)
    {
        var operation = new JsonObject
        {
            ["operationId"] = OperationId(method, relative),
            ["responses"] = Responses(desc, schemas),
        };

        var parameters = new JsonArray();
        var hasBody = false;
        foreach (var parameter in desc.ParameterDescriptions)
        {
            if (parameter.Type == typeof(CancellationToken)) continue;
            if (parameter.Source?.Id == "Body")
            {
                hasBody = true;
                continue;
            }
            if (parameter.Source?.Id is not ("Path" or "Query")) continue;
            parameters.Add(new JsonObject
            {
                ["name"] = parameter.Name,
                ["in"] = parameter.Source!.Id == "Path" ? "path" : "query",
                ["required"] = parameter.Source.Id == "Path",
                ["schema"] = Schema(parameter.Type, schemas),
            });
        }
        if (parameters.Count > 0) operation["parameters"] = parameters;
        if (hasBody)
            operation["requestBody"] = new JsonObject
            {
                ["required"] = true,
                ["content"] = new JsonObject
                {
                    ["application/json"] = new JsonObject
                    {
                        ["schema"] = Schema(desc.ParameterDescriptions
                            .First(p => p.Source?.Id == "Body").Type, schemas),
                    },
                },
            };
        return operation;
    }

    private static string OperationId(string method, string path)
    {
        var id = method switch
        {
            "GET" => "Get",
            "POST" => "Post",
            "PUT" => "Put",
            "PATCH" => "Patch",
            "DELETE" => "Delete",
            _ => method,
        };
        foreach (var segment in path.Split('/')[2..]) // drop "api/v1"
            id += segment.StartsWith('{')
                ? "By" + Pascal(segment.Trim('{', '}'))
                : Pascal(segment);
        return id;
    }

    private static string Pascal(string segment) => string.Concat(segment.Split('-',
        StringSplitOptions.RemoveEmptyEntries).Select(w => char.ToUpperInvariant(w[0]) + w[1..]));

    private static JsonObject Responses(ApiDescription desc, IDictionary<string, JsonObject> schemas)
    {
        var responses = new JsonObject();
        foreach (var responseType in desc.SupportedResponseTypes
                     .Where(t => t.Type is not null && t.Type != typeof(void))
                     .OrderBy(t => t.StatusCode))
        {
            var mediaType = responseType.ApiResponseFormats.FirstOrDefault()?.MediaType ?? "application/json";
            responses[responseType.StatusCode.ToString()] = new JsonObject
            {
                ["description"] = responseType.StatusCode is >= 200 and < 300 ? "Success" : "Error",
                ["content"] = new JsonObject
                {
                    [mediaType] = new JsonObject { ["schema"] = Schema(responseType.Type!, schemas) },
                },
            };
        }

        // R-1: every error is RFC 9457 problem+json (code, errors[], correlationId) — the default
        // response documents it uniformly, since controllers carry no per-status declarations.
        responses["default"] = new JsonObject
        {
            ["description"] = "Error — RFC 9457 problem+json (code, errors[], correlationId)",
            ["content"] = new JsonObject
            {
                ["application/problem+json"] = new JsonObject
                {
                    ["schema"] = new JsonObject { ["$ref"] = "#/components/schemas/ProblemDetails" },
                },
            },
        };
        return responses;
    }

    // Primitive and well-known shapes inline; complex types become #/components/schemas entries.
    private static JsonNode Schema(Type? type, IDictionary<string, JsonObject> schemas)
    {
        if (type is null) return new JsonObject();
        if (Nullable.GetUnderlyingType(type) is { } underlying) return Schema(underlying, schemas);
        if (type == typeof(string)) return new JsonObject { ["type"] = "string" };
        if (type == typeof(Guid)) return new JsonObject { ["type"] = "string", ["format"] = "uuid" };
        if (type == typeof(bool)) return new JsonObject { ["type"] = "boolean" };
        if (type == typeof(DateTime) || type == typeof(DateTimeOffset))
            return new JsonObject { ["type"] = "string", ["format"] = "date-time" };
        if (type == typeof(DateOnly)) return new JsonObject { ["type"] = "string", ["format"] = "date" };
        if (type == typeof(int)) return new JsonObject { ["type"] = "integer", ["format"] = "int32" };
        if (type == typeof(long)) return new JsonObject { ["type"] = "integer", ["format"] = "int64" };
        if (type == typeof(decimal) || type == typeof(double))
            return new JsonObject { ["type"] = "number", ["format"] = "double" };
        if (type == typeof(float)) return new JsonObject { ["type"] = "number", ["format"] = "float" };
        if (type == typeof(object)) return new JsonObject();
        if (type.IsArray) return new JsonObject { ["type"] = "array", ["items"] = Schema(type.GetElementType(), schemas) };
        if (type.GetInterfaces().Append(type).FirstOrDefault(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(Dictionary<,>)) is { } dict)
            return new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = Schema(dict.GetGenericArguments()[1], schemas),
            };
        if (type.GetInterfaces().Append(type).FirstOrDefault(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>)) is { } enumerable)
            return new JsonObject
            {
                ["type"] = "array",
                ["items"] = Schema(enumerable.GetGenericArguments()[0], schemas),
            };

        var name = ComponentName(type);
        if (!schemas.ContainsKey(name))
        {
            var schema = new JsonObject { ["type"] = "object" };
            schemas[name] = schema; // register before walking properties so recursion terminates

            var nullability = new NullabilityInfoContext();
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.GetIndexParameters().Length == 0 && p.GetMethod!.IsPublic)
                .OrderBy(p => p.Name, StringComparer.Ordinal)
                .ToList();

            var required = new JsonArray();
            var propertySchemas = new JsonObject();
            foreach (var property in properties)
            {
                var info = nullability.Create(property);
                var isNullable = info.WriteState == NullabilityState.Nullable;
                if (!isNullable) required.Add(property.Name);
                var propertySchema = Schema(property.PropertyType, schemas);
                if (isNullable && propertySchema is JsonObject obj) obj["nullable"] = true;
                propertySchemas[property.Name] = propertySchema;
            }
            if (required.Count > 0) schema["required"] = required;
            schema["properties"] = propertySchemas;
        }
        return new JsonObject { ["$ref"] = $"#/components/schemas/{name}" };
    }

    private static string ComponentName(Type type) => type.IsGenericType
        ? type.Name.Split('`')[0] + "Of" + string.Join("And",
            type.GetGenericArguments().Select(a => ComponentName(a)))
        : type.Name;
}
