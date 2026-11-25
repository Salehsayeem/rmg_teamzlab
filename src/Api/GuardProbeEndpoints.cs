// Task 7 guard probes: tenant isolation (TenantEntity query filter), xmin revision concurrency, and
// idempotent writes. Throwaway companion to DevProbe/AuthProbe endpoints — deleted by a later task,
// do not build features on these.
using Microsoft.EntityFrameworkCore;
using Rmg.BuildingBlocks.Auth;
using Rmg.BuildingBlocks.Common;
using Rmg.BuildingBlocks.Idempotency;
using Rmg.BuildingBlocks.Tenancy;
using Rmg.Database;
using Rmg.Modules.Organization.Domain;

namespace Rmg.Api;

public static class GuardProbeEndpoints
{
    public static void MapGuardProbeEndpoints(this WebApplication app)
    {
        app.MapGet("/guardz/probe/facility/{id}", FacilityById);
        app.MapGet("/guardz/probe/facility", FacilityList);
        app.MapPost("/guardz/probe/facility", FacilityCreate).AddEndpointFilter<IdempotencyFilter>();
        app.MapPost("/guardz/probe/facility/{id}/rename", FacilityRename);

        [RequirePerm("org.read")]
        static async Task<IResult> FacilityById(Guid id, TenantScopedDbContext db)
        {
            // The query filter narrows this to the caller's tenant: another tenant's row → 404, never a leak.
            var facility = await db.Set<Facility>().SingleOrDefaultAsync(f => f.Id == id)
                ?? throw new NotFoundProblem();
            return Results.Json(Envelope<object>.Of(Dto(facility)));
        }

        [RequirePerm("org.read")]
        static async Task<IResult> FacilityList(TenantScopedDbContext db)
        {
            var facilities = await db.Set<Facility>().OrderBy(f => f.Code).ToListAsync();
            return Results.Json(Envelope<object>.Of(facilities.Select(Dto).ToList()));
        }

        [Idempotent]
        [RequirePerm("org.facility.write")]
        static async Task<IResult> FacilityCreate(CreateFacility body, TenantScopedDbContext db, ITenantContext tenant)
        {
            var facility = new Facility
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.TenantId!.Value,
                LegalEntityId = body.LegalEntityId,
                Code = body.Code,
                Name = body.Name,
                Timezone = "Asia/Dhaka",
                Revision = 1,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = "guard-probe",
            };
            db.Add(facility);
            await db.SaveChangesAsync();
            return Results.Json(Envelope<object>.Of(Dto(facility)), statusCode: StatusCodes.Status201Created);
        }

        [RequirePerm("org.facility.write")]
        static async Task<IResult> FacilityRename(Guid id, RenameFacility body, TenantScopedDbContext db)
        {
            var facility = await db.Set<Facility>().SingleOrDefaultAsync(f => f.Id == id)
                ?? throw new NotFoundProblem();
            facility.Name = body.Name;

            try
            {
                if (facility.Revision != body.Revision)
                {
                    // The caller's expected revision is stale — stand in the concurrent writer whose
                    // commit made it stale, so our save genuinely misses the xmin match below.
                    await using var rival = new TenantScopedDbContext(new DbContextOptionsBuilder<RmgDbContext>()
                        .UseNpgsql(db.Database.GetConnectionString()!).Options);
                    var concurrent = await rival.Set<Facility>().SingleAsync(f => f.Id == id);
                    concurrent.Name = body.Name + " (rival)";
                    await rival.SaveChangesAsync();
                }

                facility.Revision++;
                await db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                var current = await db.Set<Facility>().IgnoreQueryFilters().AsNoTracking()
                    .Where(f => f.Id == id).Select(f => f.Revision).SingleAsync();
                throw new RevisionConflictProblem("FACILITY_REVISION_CONFLICT",
                    $"expected revision {body.Revision}, current {current}");
            }

            return Results.Json(Envelope<object>.Of(Dto(facility)));
        }

        static object Dto(Facility f) => new { f.Id, f.TenantId, f.Code, f.Name, f.Revision };
    }

    private sealed record CreateFacility(Guid LegalEntityId, string Code, string Name);
    private sealed record RenameFacility(string Name, int Revision);
}
