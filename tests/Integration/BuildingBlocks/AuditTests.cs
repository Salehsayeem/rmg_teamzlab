using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Rmg.BuildingBlocks.Audit;
using Rmg.BuildingBlocks.Tenancy;

namespace Rmg.IntegrationTests.BuildingBlocks;

// Audit writer acceptance: the diff lands in the caller's transaction — two SaveChanges in one tx persist both rows.
public class AuditTests : IntegrationTestBase
{
    [Fact]
    public async Task WriteAsync_RecordsChangedFieldSet()
    {
        var tenant = new TenantContext();
        tenant.Set(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var writer = new AuditWriter(Db, tenant, httpAccessor: null);
        var entityId = Guid.NewGuid();

        await using var tx = await Db.Database.BeginTransactionAsync();

        await writer.WriteAsync("CreateLegalEntity", "LegalEntity", entityId, before: null,
            after: new { Name = "Acme", Country = "EG" });
        await Db.SaveChangesAsync();

        await writer.WriteAsync("UpdateLegalEntity", "LegalEntity", entityId,
            before: new { Name = "Acme", Country = "EG" }, after: new { Name = "Acme GmbH", Country = "EG" },
            reason: "rename");
        await Db.SaveChangesAsync();

        await tx.CommitAsync();

        // ADO on the same connection (EF closes it again after commit) — assert what Postgres actually stored;
        // EF tracking would just echo the change set back.
        await Db.Database.OpenConnectionAsync();
        var conn = (NpgsqlConnection)Db.Database.GetDbConnection();

        await using (var cmd = new NpgsqlCommand(
                     "SELECT count(*) FROM audit.audit_event WHERE entity_id = @id", conn))
        {
            cmd.Parameters.AddWithValue("id", entityId.ToString());
            Assert.Equal(2L, (long)(await cmd.ExecuteScalarAsync())!);
        }

        await using (var cmd = new NpgsqlCommand(
                     "SELECT actor_id, correlation_id, changed_fields::text FROM audit.audit_event WHERE entity_id = @id AND command = 'CreateLegalEntity'", conn))
        {
            cmd.Parameters.AddWithValue("id", entityId.ToString());
            await using var reader = await cmd.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());

            // No HTTP context: actor falls back to the context user, correlation id to a fresh Guid.
            Assert.Equal(tenant.UserId!.Value.ToString(), reader.GetString(0));
            Assert.False(string.IsNullOrWhiteSpace(reader.GetString(1)));

            var created = JsonDocument.Parse(reader.GetString(2)).RootElement;
            Assert.Equal(2, created.EnumerateObject().Count()); // both properties differ from "no before"
            Assert.Equal(JsonValueKind.Null, created.GetProperty("Name").GetProperty("before").ValueKind);
            Assert.Equal("Acme", created.GetProperty("Name").GetProperty("after").GetString());
            Assert.Equal("EG", created.GetProperty("Country").GetProperty("after").GetString());
        }

        await using (var cmd = new NpgsqlCommand(
                     "SELECT reason, changed_fields::text FROM audit.audit_event WHERE entity_id = @id AND command = 'UpdateLegalEntity'", conn))
        {
            cmd.Parameters.AddWithValue("id", entityId.ToString());
            await using var reader = await cmd.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal("rename", reader.GetString(0));

            // Only the field that actually changed is recorded, with its before/after pair.
            var updated = JsonDocument.Parse(reader.GetString(1)).RootElement;
            var changed = Assert.Single(updated.EnumerateObject());
            Assert.Equal("Name", changed.Name);
            Assert.Equal("Acme", changed.Value.GetProperty("before").GetString());
            Assert.Equal("Acme GmbH", changed.Value.GetProperty("after").GetString());
        }
    }
}
