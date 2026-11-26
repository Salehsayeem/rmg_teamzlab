using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
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

// Task 14 (ORD/M005): customer orders + confirm lifecycle. Capture validates the style version is
// Approved (STYLE_VERSION_NOT_APPROVED) and every ordered (color,size) cell against that version's
// variant matrix (ORDER_VARIANT_MISMATCH, per-field errors), stores line_qty = Σ sizes and 422s a
// duplicate (tenant, buyer, buyer_po). Confirm is idempotent ([Idempotent] + Idempotency-Key),
// maker-checker/revision-guarded; cancel moves Draft/Confirmed → Cancelled; confirm-after-cancel is
// INVALID_STATE. Every command emits one outbox + one audit row in the same transaction. Another
// tenant's id is 404, never a leak. Direct row asserts use IgnoreQueryFilters() (fail-closed filter
// on the harness's base context).
public class OrderEndpointTests : IntegrationTestBase
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

    private async Task<Guid> SeedBuyerAsync(Guid tenantId, string code = "BUY-ORD")
    {
        var buyer = new Buyer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = code,
            Name = $"Buyer {code}",
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

    // An Approved style + version via the API — the capture validations run against this version's
    // variant matrix, so the tests author it through pdm.style.create + approve.
    private async Task<(Guid StyleId, Guid VersionId)> CreateApprovedVersionAsync(
        string token, Guid buyerId, string code, (string Color, string Size)[] variants)
    {
        Client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var create = await Client.PostAsJsonAsync("/api/v1/pdm/styles", new
        {
            code,
            name = $"Style {code}",
            buyerId,
            variants = variants.Select(v => new { colorCode = v.Color, sizeCode = v.Size }),
        });
        Assert.True(create.StatusCode == HttpStatusCode.Created,
            $"style create failed: {(int)create.StatusCode} {await create.Content.ReadAsStringAsync()}");
        var styleId = (await JsonDocument.ParseAsync(await create.Content.ReadAsStreamAsync()))
            .RootElement.GetProperty("data").GetProperty("id").GetGuid();
        var approved = await Client.PostAsync($"/api/v1/pdm/styles/{styleId}/approve", null);
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        var data = (await JsonDocument.ParseAsync(await approved.Content.ReadAsStreamAsync()))
            .RootElement.GetProperty("data");
        return (styleId, data.GetProperty("versions")[0].GetProperty("id").GetGuid());
    }

    private Task<HttpResponseMessage> CaptureAsync(string token, object body)
    {
        Client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return Client.PostAsJsonAsync("/api/v1/ord/customer-orders", body);
    }

    private static object CaptureBody(Guid buyerId, Guid versionId, string code = "ORD-101", string buyerPo = "PO-101",
        object[]? sizes = null) => new
    {
        code,
        buyerId,
        buyerPo,
        currency = "USD",
        orderDate = "2026-01-15",
        lines = new[]
        {
            new
            {
                styleVersionId = versionId,
                price = 4.25m,
                sizes = sizes ?? new object[]
                {
                    new { colorCode = "NAVY", sizeCode = "L", qty = 600m },
                    new { colorCode = "NAVY", sizeCode = "M", qty = 400m },
                },
                schedules = new object[]
                {
                    new { shipDate = "2026-02-15", qty = 600m },
                    new { shipDate = "2026-03-15", qty = 400m },
                },
            },
        },
    };

    private Task<HttpResponseMessage> ConfirmAsync(string token, Guid id, int expectedRevision, string? idempotencyKey = null)
    {
        Client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        Client.DefaultRequestHeaders.Remove("Idempotency-Key");
        if (idempotencyKey is not null)
            Client.DefaultRequestHeaders.Add("Idempotency-Key", idempotencyKey);
        return Client.PostAsJsonAsync($"/api/v1/ord/customer-orders/{id}/confirm", new { expectedRevision });
    }

    private async Task<Guid> CaptureSimpleAsync(string token, Guid buyerId, Guid versionId, string code = "ORD-101", string buyerPo = "PO-101")
    {
        var created = await CaptureAsync(token, CaptureBody(buyerId, versionId, code, buyerPo));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return (await JsonDocument.ParseAsync(await created.Content.ReadAsStreamAsync()))
            .RootElement.GetProperty("data").GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> ProblemAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    private async Task<string> ReaderTokenAsync()
    {
        // A reader: ord.order.read only — no ord.order.confirm.
        var tenantId = await Db.Set<Tenant>().Select(t => t.Id).SingleAsync();
        var hasher = new PasswordHasher<AppUser>();
        var reader = new AppUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = "order-reader@tzl.demo",
            DisplayName = "Order Reader",
            IsActive = true,
            Revision = 1,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "test",
        };
        reader.PasswordHash = hasher.HashPassword(reader, AdminPassword);
        var role = new Role { Id = Guid.NewGuid(), TenantId = tenantId, Code = "OrderReader", Revision = 1, CreatedAtUtc = DateTime.UtcNow, CreatedBy = "test" };
        Db.AddRange(reader, role,
            new RolePermission { RoleId = role.Id, PermissionCode = "ord.order.read" },
            new UserRole { TenantId = tenantId, UserId = reader.Id, RoleId = role.Id });
        await Db.SaveChangesAsync();

        var login = await Client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = reader.Email, password = AdminPassword });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await JsonDocument.ParseAsync(await login.Content.ReadAsStreamAsync());
        return body.RootElement.GetProperty("data").GetProperty("accessToken").GetString()!;
    }

    [Fact]
    public async Task CaptureOrder_Happy_Draft()
    {
        var token = await AdminTokenAsync();
        var tenantId = await Db.Set<Tenant>().Select(t => t.Id).SingleAsync();
        var buyerId = await SeedBuyerAsync(tenantId);
        var (_, versionId) = await CreateApprovedVersionAsync(token, buyerId, "STY-ORD",
            [("NAVY", "L"), ("NAVY", "M")]);

        var response = await CaptureAsync(token, CaptureBody(buyerId, versionId));

        Assert.True(response.StatusCode == HttpStatusCode.Created,
            $"capture failed: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        var data = (await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync()))
            .RootElement.GetProperty("data");
        var orderId = data.GetProperty("id").GetGuid();
        Assert.Equal("ORD-101", data.GetProperty("code").GetString());
        Assert.Equal("PO-101", data.GetProperty("buyerPo").GetString());
        Assert.Equal("USD", data.GetProperty("currency").GetString());
        Assert.Equal("2026-01-15", data.GetProperty("orderDate").GetString());
        Assert.Equal("Draft", data.GetProperty("status").GetString());
        Assert.Equal(1, data.GetProperty("revision").GetInt32());
        Assert.Equal(buyerId, data.GetProperty("buyerId").GetGuid());
        var line = data.GetProperty("lines")[0];
        Assert.Equal(1000, line.GetProperty("lineQty").GetInt32()); // derived = Σ sizes
        Assert.Equal(4.25m, line.GetProperty("price").GetDecimal());
        Assert.Equal(2, line.GetProperty("sizes").GetArrayLength());
        Assert.Equal(2, line.GetProperty("schedules").GetArrayLength());

        // GET by id mirrors the capture response (browser-ready read model).
        Client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var fetched = await Client.GetAsync($"/api/v1/ord/customer-orders/{orderId}");
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        var fetchedData = (await JsonDocument.ParseAsync(await fetched.Content.ReadAsStreamAsync()))
            .RootElement.GetProperty("data");
        Assert.Equal("Draft", fetchedData.GetProperty("status").GetString());
        Assert.Equal(1000, fetchedData.GetProperty("lines")[0].GetProperty("lineQty").GetInt32());

        var orderRow = await Db.Set<CustomerOrder>().IgnoreQueryFilters().SingleAsync(o => o.Id == orderId);
        Assert.Equal("Draft", orderRow.Status);
        Assert.Equal(buyerId, orderRow.BuyerId);
        var lineRow = await Db.Set<OrderLine>().IgnoreQueryFilters().SingleAsync(l => l.OrderId == orderId);
        Assert.Equal(1000, lineRow.LineQty); // stored derived value
        Assert.Equal(2, await Db.Set<SizeQuantity>().IgnoreQueryFilters().CountAsync(s => s.OrderLineId == lineRow.Id));
        Assert.Equal(2, await Db.Set<DeliverySchedule>().IgnoreQueryFilters().CountAsync(d => d.OrderLineId == lineRow.Id));

        // One command → one outbox + one audit row, same transaction (scoped to the order's own
        // aggregate: the scratch DB also carries boot-seeded demo + the style's pdm events).
        var outbox = Assert.Single(await Db.Set<OutboxMessage>().ToListAsync(), o => o.AggregateId == orderId);
        Assert.Equal("ord.customer.order.capture.changed.v1", outbox.EventType);
        Assert.Equal(orderId, outbox.AggregateId);
        Assert.Equal("Pending", outbox.Status);
        var audit = Assert.Single(await Db.Set<AuditEvent>().ToListAsync(), a => a.EntityId == orderId.ToString());
        Assert.Equal("ord.order.create", audit.Command);
        Assert.Equal("ord.customer_order", audit.EntityType);
        Assert.Equal(orderId.ToString(), audit.EntityId);
    }

    [Fact]
    public async Task Capture_NonApprovedVersion_422()
    {
        var token = await AdminTokenAsync();
        var tenantId = await Db.Set<Tenant>().Select(t => t.Id).SingleAsync();
        var buyerId = await SeedBuyerAsync(tenantId, "BUY-DRAFT");
        Client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var style = await Client.PostAsJsonAsync("/api/v1/pdm/styles", new
        {
            code = "STY-DRAFT",
            name = "Draft Style",
            buyerId,
            variants = new[] { new { colorCode = "NAVY", sizeCode = "L" } },
        });
        Assert.Equal(HttpStatusCode.Created, style.StatusCode);
        var versionId = (await JsonDocument.ParseAsync(await style.Content.ReadAsStreamAsync()))
            .RootElement.GetProperty("data").GetProperty("versions")[0].GetProperty("id").GetGuid();

        var response = await CaptureAsync(token, CaptureBody(buyerId, versionId, "ORD-DRAFT", "PO-DRAFT"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await ProblemAsync(response);
        Assert.Equal("STYLE_VERSION_NOT_APPROVED", problem.GetProperty("code").GetString());
        Assert.Equal(422, problem.GetProperty("status").GetInt32());
        Assert.Equal(0, await Db.Set<CustomerOrder>().IgnoreQueryFilters().CountAsync(o => o.Code == "ORD-DRAFT")); // nothing persisted
    }

    [Fact]
    public async Task Capture_VariantMismatch_422_FieldErrors()
    {
        var token = await AdminTokenAsync();
        var tenantId = await Db.Set<Tenant>().Select(t => t.Id).SingleAsync();
        var buyerId = await SeedBuyerAsync(tenantId, "BUY-VAR");
        var (_, versionId) = await CreateApprovedVersionAsync(token, buyerId, "STY-VAR", [("NAVY", "L")]);

        var response = await CaptureAsync(token, CaptureBody(buyerId, versionId, "ORD-VAR", "PO-VAR", new object[]
        {
            new { colorCode = "NAVY", sizeCode = "L", qty = 600m },   // in the matrix
            new { colorCode = "RED", sizeCode = "M", qty = 200m },    // not
            new { colorCode = "BLACK", sizeCode = "XL", qty = 100m }, // not
        }));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await ProblemAsync(response);
        Assert.Equal("ORDER_VARIANT_MISMATCH", problem.GetProperty("code").GetString());
        var errors = problem.GetProperty("errors");
        Assert.Equal(2, errors.EnumerateObject().Count()); // per-field: exactly the two bad cells
        Assert.True(errors.TryGetProperty("lines[0].sizes[1]", out var redCell));
        Assert.Contains("RED", redCell[0].GetString());
        Assert.Contains("M", redCell[0].GetString());
        Assert.True(errors.TryGetProperty("lines[0].sizes[2]", out var blackCell));
        Assert.Contains("BLACK", blackCell[0].GetString());
        Assert.Equal(0, await Db.Set<CustomerOrder>().IgnoreQueryFilters().CountAsync(o => o.Code == "ORD-VAR"));
    }

    private async Task<HttpResponseMessage> CaptureRawLineAsync(string token, Guid buyerId, Guid versionId, string code, object line) =>
        await CaptureAsync(token, new
        {
            code,
            buyerId,
            buyerPo = $"PO-{code}",
            currency = "USD",
            orderDate = "2026-01-15",
            lines = new[] { line },
        });

    // A JSON-null colorCode/sizeCode must be a controlled 400 shape problem, never a 500 NRE from
    // the variant-mismatch loop (the cells' presence is checked before any domain rule runs).
    [Fact]
    public async Task Capture_SizeCell_MissingColor_400()
    {
        var token = await AdminTokenAsync();
        var tenantId = await Db.Set<Tenant>().Select(t => t.Id).SingleAsync();
        var buyerId = await SeedBuyerAsync(tenantId, "BUY-JNK1");
        var (_, versionId) = await CreateApprovedVersionAsync(token, buyerId, "STY-JNK1", [("NAVY", "L")]);

        var response = await CaptureRawLineAsync(token, buyerId, versionId, "ORD-JNK1",
            new { styleVersionId = versionId, price = 4.25m, sizes = new[] { new { colorCode = (string?)null, sizeCode = "L", qty = 5m } } });

        Assert.True(response.StatusCode == HttpStatusCode.BadRequest,
            $"junk cell: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        var problem = await ProblemAsync(response);
        Assert.Equal("VALIDATION_FAILED", problem.GetProperty("code").GetString());
        Assert.True(problem.GetProperty("errors").TryGetProperty("lines[0].sizes[0]", out var cellErrors));
        Assert.NotEmpty(cellErrors.EnumerateArray());
        Assert.Equal(0, await Db.Set<CustomerOrder>().IgnoreQueryFilters().CountAsync(o => o.Code == "ORD-JNK1"));
    }

    [Fact]
    public async Task Capture_SizeCell_MissingSize_400()
    {
        var token = await AdminTokenAsync();
        var tenantId = await Db.Set<Tenant>().Select(t => t.Id).SingleAsync();
        var buyerId = await SeedBuyerAsync(tenantId, "BUY-JNK2");
        var (_, versionId) = await CreateApprovedVersionAsync(token, buyerId, "STY-JNK2", [("NAVY", "L")]);

        var response = await CaptureRawLineAsync(token, buyerId, versionId, "ORD-JNK2",
            new { styleVersionId = versionId, price = 4.25m, sizes = new[] { new { colorCode = "NAVY", sizeCode = (string?)null, qty = 5m } } });

        Assert.True(response.StatusCode == HttpStatusCode.BadRequest,
            $"junk cell: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        var problem = await ProblemAsync(response);
        Assert.Equal("VALIDATION_FAILED", problem.GetProperty("code").GetString());
        Assert.True(problem.GetProperty("errors").TryGetProperty("lines[0].sizes[0]", out var cellErrors));
        Assert.NotEmpty(cellErrors.EnumerateArray());
        Assert.Equal(0, await Db.Set<CustomerOrder>().IgnoreQueryFilters().CountAsync(o => o.Code == "ORD-JNK2"));
    }

    // A schedule without a ship date would otherwise stage as 0001-01-01 — rejected at shape level.
    [Fact]
    public async Task Capture_Schedule_MissingShipDate_400()
    {
        var token = await AdminTokenAsync();
        var tenantId = await Db.Set<Tenant>().Select(t => t.Id).SingleAsync();
        var buyerId = await SeedBuyerAsync(tenantId, "BUY-JNK3");
        var (_, versionId) = await CreateApprovedVersionAsync(token, buyerId, "STY-JNK3", [("NAVY", "L")]);

        var response = await CaptureRawLineAsync(token, buyerId, versionId, "ORD-JNK3", new
        {
            styleVersionId = versionId,
            price = 4.25m,
            sizes = new[] { new { colorCode = "NAVY", sizeCode = "L", qty = 600m } },
            schedules = new[] { new { qty = 600m } }, // no shipDate → default(DateOnly)
        });

        Assert.True(response.StatusCode == HttpStatusCode.BadRequest,
            $"missing ship date: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        var problem = await ProblemAsync(response);
        Assert.Equal("VALIDATION_FAILED", problem.GetProperty("code").GetString());
        Assert.True(problem.GetProperty("errors").TryGetProperty("lines[0].schedules[0].shipDate", out var shipErrors));
        Assert.NotEmpty(shipErrors.EnumerateArray());
        Assert.Equal(0, await Db.Set<CustomerOrder>().IgnoreQueryFilters().CountAsync(o => o.Code == "ORD-JNK3"));
    }

    [Fact]
    public async Task DuplicateBuyerPo_422()
    {
        var token = await AdminTokenAsync();
        var tenantId = await Db.Set<Tenant>().Select(t => t.Id).SingleAsync();
        var buyerId = await SeedBuyerAsync(tenantId, "BUY-PO");
        var (_, versionId) = await CreateApprovedVersionAsync(token, buyerId, "STY-PO", [("NAVY", "L"), ("NAVY", "M")]);
        await CaptureSimpleAsync(token, buyerId, versionId, "ORD-PO-1", "PO-DUP-1");

        var dupe = await CaptureAsync(token, CaptureBody(buyerId, versionId, "ORD-PO-2", "PO-DUP-1"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, dupe.StatusCode);
        var problem = await ProblemAsync(dupe);
        Assert.Equal("DUPLICATE_BUYER_PO", problem.GetProperty("code").GetString());
        Assert.Equal(1, await Db.Set<CustomerOrder>().IgnoreQueryFilters().CountAsync(o => o.BuyerPo == "PO-DUP-1"));
    }

    [Fact]
    public async Task Confirm_Happy_200()
    {
        var token = await AdminTokenAsync();
        var tenantId = await Db.Set<Tenant>().Select(t => t.Id).SingleAsync();
        var buyerId = await SeedBuyerAsync(tenantId, "BUY-CNF");
        var (_, versionId) = await CreateApprovedVersionAsync(token, buyerId, "STY-CNF", [("NAVY", "L"), ("NAVY", "M")]);
        var orderId = await CaptureSimpleAsync(token, buyerId, versionId, "ORD-CNF", "PO-CNF");

        var response = await ConfirmAsync(token, orderId, expectedRevision: 1);

        Assert.True(response.StatusCode == HttpStatusCode.OK,
            $"confirm failed: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        var data = (await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync()))
            .RootElement.GetProperty("data");
        Assert.Equal("Confirmed", data.GetProperty("status").GetString());
        Assert.Equal(2, data.GetProperty("revision").GetInt32());

        var orderRow = await Db.Set<CustomerOrder>().IgnoreQueryFilters().SingleAsync(o => o.Id == orderId);
        Assert.Equal("Confirmed", orderRow.Status);
        Assert.Equal(2, orderRow.Revision);

        // Capture + confirm → two outbox + two audit rows, exact event/command names, same aggregate.
        var outboxRows = await Db.Set<OutboxMessage>().Where(o => o.AggregateId == orderId).ToListAsync();
        Assert.Equal(2, outboxRows.Count);
        Assert.All(outboxRows, o => Assert.Equal("ord.customer.order.capture.changed.v1", o.EventType));
        Assert.All(outboxRows, o => Assert.Equal("Pending", o.Status));
        var auditRows = await Db.Set<AuditEvent>().Where(a => a.EntityId == orderId.ToString()).OrderBy(a => a.OccurredAtUtc).ToListAsync();
        Assert.Equal(["ord.order.create", "ord.order.confirm"], auditRows.Select(a => a.Command).ToArray());
        Assert.All(auditRows, a => Assert.Equal("ord.customer_order", a.EntityType));
    }

    [Fact]
    public async Task Confirm_Replay_SameIdempotencyKey_NoDoubleEvent()
    {
        var token = await AdminTokenAsync();
        var tenantId = await Db.Set<Tenant>().Select(t => t.Id).SingleAsync();
        var buyerId = await SeedBuyerAsync(tenantId, "BUY-RPL");
        var (_, versionId) = await CreateApprovedVersionAsync(token, buyerId, "STY-RPL", [("NAVY", "L"), ("NAVY", "M")]);
        var orderId = await CaptureSimpleAsync(token, buyerId, versionId, "ORD-RPL", "PO-RPL");

        Client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        Client.DefaultRequestHeaders.Add("Idempotency-Key", "confirm-replay-1");
        var first = await Client.PostAsJsonAsync($"/api/v1/ord/customer-orders/{orderId}/confirm", new { expectedRevision = 1 });
        Assert.True(first.StatusCode == HttpStatusCode.OK,
            $"first confirm failed: {(int)first.StatusCode} {await first.Content.ReadAsStringAsync()}");
        var firstJson = await first.Content.ReadAsStringAsync();

        var replay = await Client.PostAsJsonAsync($"/api/v1/ord/customer-orders/{orderId}/confirm", new { expectedRevision = 1 });

        Assert.True(replay.StatusCode == HttpStatusCode.OK,
            $"replay failed: {(int)replay.StatusCode} {await replay.Content.ReadAsStringAsync()}");
        Assert.Equal("true", replay.Headers.GetValues("Idempotent-Replay").Single());
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(firstJson), JsonNode.Parse(await replay.Content.ReadAsStringAsync())));

        // Replay served the stored response — no second confirm event/audit.
        Assert.Equal(2, await Db.Set<OutboxMessage>().CountAsync(o => o.AggregateId == orderId));
        Assert.Equal(2, await Db.Set<AuditEvent>().CountAsync(a => a.EntityId == orderId.ToString()));
    }

    [Fact]
    public async Task Confirm_StaleRevision_409()
    {
        var token = await AdminTokenAsync();
        var tenantId = await Db.Set<Tenant>().Select(t => t.Id).SingleAsync();
        var buyerId = await SeedBuyerAsync(tenantId, "BUY-STL");
        var (_, versionId) = await CreateApprovedVersionAsync(token, buyerId, "STY-STL", [("NAVY", "L"), ("NAVY", "M")]);
        var orderId = await CaptureSimpleAsync(token, buyerId, versionId, "ORD-STL", "PO-STL");

        var response = await ConfirmAsync(token, orderId, expectedRevision: 99);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await ProblemAsync(response);
        Assert.Equal("ORDER_REVISION_CONFLICT", problem.GetProperty("code").GetString());
        Assert.Equal("Draft", (await Db.Set<CustomerOrder>().IgnoreQueryFilters().SingleAsync(o => o.Id == orderId)).Status);
        Assert.Equal(1, await Db.Set<OutboxMessage>().CountAsync(o => o.AggregateId == orderId)); // capture only
        Assert.Equal(1, await Db.Set<AuditEvent>().CountAsync(a => a.EntityId == orderId.ToString()));
    }

    [Fact]
    public async Task Confirm_MissingPerm_403()
    {
        var token = await AdminTokenAsync();
        var tenantId = await Db.Set<Tenant>().Select(t => t.Id).SingleAsync();
        var buyerId = await SeedBuyerAsync(tenantId, "BUY-PRM");
        var (_, versionId) = await CreateApprovedVersionAsync(token, buyerId, "STY-PRM", [("NAVY", "L"), ("NAVY", "M")]);
        var orderId = await CaptureSimpleAsync(token, buyerId, versionId, "ORD-PRM", "PO-PRM");
        var readerToken = await ReaderTokenAsync();

        var response = await ConfirmAsync(readerToken, orderId, expectedRevision: 1);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await ProblemAsync(response);
        Assert.Equal("PERMISSION_DENIED", problem.GetProperty("code").GetString());
        Assert.Equal("Draft", (await Db.Set<CustomerOrder>().IgnoreQueryFilters().SingleAsync(o => o.Id == orderId)).Status);
    }

    [Fact]
    public async Task Cancel_ThenConfirm_422_INVALID_STATE()
    {
        var token = await AdminTokenAsync();
        var tenantId = await Db.Set<Tenant>().Select(t => t.Id).SingleAsync();
        var buyerId = await SeedBuyerAsync(tenantId, "BUY-CXL");
        var (_, versionId) = await CreateApprovedVersionAsync(token, buyerId, "STY-CXL", [("NAVY", "L"), ("NAVY", "M")]);
        var orderId = await CaptureSimpleAsync(token, buyerId, versionId, "ORD-CXL", "PO-CXL");

        Client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var cancel = await Client.PostAsJsonAsync($"/api/v1/ord/customer-orders/{orderId}/cancel", new { reason = "Buyer withdrew PO" });
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        Assert.Equal("Cancelled", (await JsonDocument.ParseAsync(await cancel.Content.ReadAsStreamAsync()))
            .RootElement.GetProperty("data").GetProperty("status").GetString());

        var confirm = await ConfirmAsync(token, orderId, expectedRevision: 2);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, confirm.StatusCode);
        var problem = await ProblemAsync(confirm);
        Assert.Equal("INVALID_STATE", problem.GetProperty("code").GetString());
        Assert.Equal("Cancelled", (await Db.Set<CustomerOrder>().IgnoreQueryFilters().SingleAsync(o => o.Id == orderId)).Status);

        // Create + cancel rode the same transaction each: two events, and no confirm audit.
        var auditRows = await Db.Set<AuditEvent>().Where(a => a.EntityId == orderId.ToString()).OrderBy(a => a.OccurredAtUtc).ToListAsync();
        Assert.Equal(["ord.order.create", "ord.order.cancel"], auditRows.Select(a => a.Command).ToArray());
        Assert.Equal(2, await Db.Set<OutboxMessage>().CountAsync(o => o.AggregateId == orderId));
    }

    [Fact]
    public async Task Confirm_MakerCheckerOn_RejectsCreator()
    {
        var token = await AdminTokenAsync();
        var tenant = await Db.Set<Tenant>().SingleAsync();
        tenant.RequireMakerChecker = true; // tenant policy: the creator cannot confirm their own order
        await Db.SaveChangesAsync();
        var buyerId = await SeedBuyerAsync(tenant.Id, "BUY-MKO");

        // The style must be approvable despite the flag: another maker authors it (StyleEndpointTests
        // workaround) — but the ORDER itself is captured by the admin, so confirm is the blocked move.
        Client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var style = await Client.PostAsJsonAsync("/api/v1/pdm/styles", new
        {
            code = "STY-MKO",
            name = "Maker-Checker Style",
            buyerId,
            variants = new[] { new { colorCode = "NAVY", sizeCode = "L" }, new { colorCode = "NAVY", sizeCode = "M" } },
        });
        Assert.Equal(HttpStatusCode.Created, style.StatusCode);
        var styleData = (await JsonDocument.ParseAsync(await style.Content.ReadAsStreamAsync()))
            .RootElement.GetProperty("data");
        var styleId = styleData.GetProperty("id").GetGuid();
        Db.Set<Style>().IgnoreQueryFilters().Single(s => s.Id == styleId).CreatedBy = "maker-1";
        await Db.SaveChangesAsync();
        var approved = await Client.PostAsync($"/api/v1/pdm/styles/{styleId}/approve", null);
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        var versionId = (await JsonDocument.ParseAsync(await approved.Content.ReadAsStreamAsync()))
            .RootElement.GetProperty("data").GetProperty("versions")[0].GetProperty("id").GetGuid();

        var orderId = await CaptureSimpleAsync(token, buyerId, versionId, "ORD-MKO", "PO-MKO");

        var response = await ConfirmAsync(token, orderId, expectedRevision: 1);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await ProblemAsync(response);
        Assert.Equal("MAKER_CHECKER_VIOLATION", problem.GetProperty("code").GetString());
        Assert.Equal("Draft", (await Db.Set<CustomerOrder>().IgnoreQueryFilters().SingleAsync(o => o.Id == orderId)).Status);
    }

    [Fact]
    public async Task CrossTenant_OrderId_404()
    {
        var token = await AdminTokenAsync();

        // Another tenant's stack, inserted directly — the buyer FK forces a rival buyer row too.
        var rivalTenantId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var rivalBuyer = new Buyer
        {
            Id = Guid.NewGuid(),
            TenantId = rivalTenantId,
            Code = "BUY-RIVAL",
            Name = "Rival Buyer",
            Country = "BD",
            Currency = "USD",
            Status = "Active",
            Revision = 1,
            CreatedAtUtc = now,
            CreatedBy = "test",
        };
        var rivalOrder = new CustomerOrder
        {
            Id = Guid.NewGuid(),
            TenantId = rivalTenantId,
            Code = "ORD-RIVAL",
            BuyerId = rivalBuyer.Id,
            BuyerPo = "PO-RIVAL",
            Currency = "USD",
            OrderDate = new DateOnly(2026, 1, 15),
            Status = "Draft",
            Revision = 1,
            CreatedAtUtc = now,
            CreatedBy = "test",
        };
        Db.AddRange(new Tenant
        {
            Id = rivalTenantId,
            Name = "Rival Ltd.",
            Code = "RIV-ORD",
            DefaultLocale = "en",
            RequireMakerChecker = false,
            Revision = 1,
            CreatedAtUtc = now,
            CreatedBy = "test",
        }, rivalBuyer, rivalOrder);
        await Db.SaveChangesAsync();

        Client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var fetched = await Client.GetAsync($"/api/v1/ord/customer-orders/{rivalOrder.Id}");
        Assert.Equal(HttpStatusCode.NotFound, fetched.StatusCode);
        Assert.Equal("NOT_FOUND", (await ProblemAsync(fetched)).GetProperty("code").GetString());

        var confirm = await ConfirmAsync(token, rivalOrder.Id, expectedRevision: 1);
        Assert.Equal(HttpStatusCode.NotFound, confirm.StatusCode);
        Assert.Equal("NOT_FOUND", (await ProblemAsync(confirm)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task DemoSeed_ProducesBrowserReadyDataset()
    {
        await Seeder.SeedAsync(Db);
        await Seeder.DemoSeedAsync(Db);
        await Seeder.DemoSeedAsync(Db); // idempotent — no second dataset

        var token = await AdminTokenAsync();

        Client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var orders = await Client.GetAsync("/api/v1/ord/customer-orders");
        Assert.Equal(HttpStatusCode.OK, orders.StatusCode);
        var ordersRaw = await orders.Content.ReadAsStringAsync();
        Assert.Contains("ORD-001", ordersRaw);
        Assert.Contains("Confirmed", ordersRaw);

        var styles = await Client.GetAsync("/api/v1/pdm/styles");
        Assert.Equal(HttpStatusCode.OK, styles.StatusCode);
        var stylesRaw = await styles.Content.ReadAsStringAsync();
        Assert.Contains("STY-001", stylesRaw);
        Assert.Contains("Approved", stylesRaw);

        var buyers = await Client.GetAsync("/api/v1/ord/buyers");
        Assert.Equal(HttpStatusCode.OK, buyers.StatusCode);
        Assert.Contains("BUY-001", await buyers.Content.ReadAsStringAsync());

        // Exactly one demo dataset despite the second DemoSeedAsync call.
        Assert.Equal(1, await Db.Set<CustomerOrder>().IgnoreQueryFilters().CountAsync(o => o.Code == "ORD-001"));
        Assert.Equal(1, await Db.Set<CustomerOrder>().IgnoreQueryFilters().CountAsync());
    }
}
