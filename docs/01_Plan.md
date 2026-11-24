# RMG Foundation + Pilot Slice — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Status:** awaiting owner review · **Pause/resume:** banner goes here when a track is frozen
**Goal:** Ship the platform foundation (tenancy, auth, envelope, outbox, audit, idempotency, migrations, seed) plus the pilot merchandising flow — buyer → style(+variants) → BOM(approve) → order → confirm — tested, browser-verifiable, in the reference design look.

**Architecture:** Modular monolith (FND-004): ASP.NET Core API owning all domain logic and writes; PostgreSQL transactional source of truth; Next.js 16 stateless client calling `/api/v1`. Four pilot modules + building blocks; outbox dispatcher worker; single `RmgDbContext` composing module configurations; scratch-DB integration harness on the owner's PostgreSQL.

**Tech Stack:** .NET 11 (`net11.0`) · EF Core 10 + Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3 · PostgreSQL @ localhost:5433 (`rmg_teamzlab`) · Next.js 16 / TypeScript / Tailwind v4 · xUnit · Playwright.

**Spec:** `docs/superpowers/specs/2026-11-25-rmg-foundation-pilot-design.md` (read together with this plan; the spec argues the why, this plan fixes the what/where/exact names).

## Global Constraints

- .NET SDK pinned `11.0.100` via `global.json`; all projects `net11.0`; EF Core **10.x** + Npgsql provider **10.0.3** (never EF 11 / provider 11; never prerelease).
- DB creds/keys only in gitignored `.env` (`ConnectionStrings__Default`, `Jwt__Key`, `Seed__AdminPassword`); committed `.env.example` holds placeholders.
- Tenant `tid`/facility `fid`/user `sub`/permissions `perm` come **only** from JWT claims; never from body/query (R-2).
- Envelope: success `{ "data": …, "meta": … }`; errors RFC 9457 problem+json with stable `code` + `errors[]` + `correlationId` (R-1). No other shapes.
- Money `numeric(18,2)` + `currency char(3)`; quantities `numeric(18,4)` + UOM code; UTC `timestamptz`; UUID PKs; `tenant_id` inside every unique key/composite FK (R-6).
- Forward-only migrations, never edited after apply; migration files carry the task-assigned numbers below (parallel tasks must use their assigned number only).
- TDD per task: failing test first, watched failing, then minimal code; suite green on the branch before commit.
- Conventional Commits + `Co-Authored-By: Claude Code <noreply@anthropic.com>`; one feature per `feat/*` branch; worktrees at `.worktrees/` (gitignored).
- Agent verification boots use API port **5101** / web port **3101** and are stopped after use; owner standards are API 5000 / web 3000.
- No new dependencies beyond: EF Core/Npgsql, `Microsoft.AspNetCore.Authentication.JwtBearer` (11.0.x), `DotNetEnv`, xUnit stack, `Microsoft.EntityFrameworkCore.Design` (Database project only). Frontend: Next/React/Tailwind only. Anything else needs a controller ruling.
- Feature branches merge into `integration`; full regression gate before `integration → main`.

## Fan-out waves

| Wave | Tasks (parallel within wave) | Why safe |
|---|---|---|
| W0 (controller inline) | 1–7 foundation serial | shared files everywhere |
| W1 | 8 (ORG), 9 (IAM) — parallel | disjoint module folders; both create NO migrations (org/iam tables already in M001); only add endpoints + services |
| W2 | 10 (ORD buyers, M002), 11 (PDM master, M003) — parallel | distinct modules + distinct assigned migration numbers; snapshot union-resolve at merge (playbook §2) |
| W3 | 12+13 (PDM styles+BOM, M004) — one implementer | same module |
| W4 | 14 (ORD orders, M005) | depends on approved-style invariant from 12 |
| W5 | 15 (OpenAPI export) | all endpoints exist |
| W6 | 16 (shell/theme/i18n) then 17 (login) ∥ 18 (dashboard) | 17/18 disjoint folders; nav registry touched only by 18 |
| W7 | 19 (buyers) ∥ 20 (styles) ∥ 21 (BOM) ∥ 22 (orders) — parallel | each screen own folder; **nobody edits sidebar nav file** — controller adds all nav entries at merge |
| W8 | 23 (E2E + smoke + regression + merge) | controller inline |

Migration register (assigned numbers — do not deviate):
`001_foundation` (T5) · `002_ord_buyer` (T10) · `003_pdm_master` (T11) · `004_pdm_style` (T12) · `005_ord_order` (T14).

---

### Task 1: Solution scaffold + build gate

**Files:** Create `global.json` (`{"sdk":{"version":"11.0.100"}}`), `Directory.Build.props` (net11.0, Nullable enable, ImplicitUsings enable), `RMG.sln`, projects: `src/Api` (web), `src/Database` (class lib: DbContext + migrations assembly), `src/Workers` (worker svc), `src/BuildingBlocks/{Common,Tenancy,Auth,Outbox,Audit,Idempotency}` (class libs), `src/Modules/{Organization,Identity,ProductEngineering,Merchandising}/{Domain,Application,Infrastructure,Contracts}` (class libs), `tests/{Unit,Integration,Contract,Security}/<Name>Tests`, `.env.example`.
**Interfaces (produces):** project names/namespaces: `Rmg.Api`, `Rmg.Database`, `Rmg.Workers`, `Rmg.BuildingBlocks.Common`, `Rmg.BuildingBlocks.Tenancy`, `Rmg.BuildingBlocks.Auth`, `Rmg.BuildingBlocks.Outbox`, `Rmg.BuildingBlocks.Audit`, `Rmg.BuildingBlocks.Idempotency`, `Rmg.Modules.<Name>.{Domain,Application,Infrastructure,Contracts}`, `Rmg.UnitTests`, `Rmg.IntegrationTests`, `Rmg.ContractTests`, `Rmg.SecurityTests`. Api references all Modules + BuildingBlocks + Database; Workers references Database + Outbox; each Module references BuildingBlocks.Common + Tenancy only.
**Steps:**
- [ ] `dotnet new sln` + all projects + references wired; package pins into the projects that need them (Database: EFDesign; Api: JwtBearer; Database+Api: Npgsql 10.0.3).
- [ ] `dotnet build` → 0 errors; commit `chore: scaffold solution skeleton (net11.0, EF10/Npgsql 10.0.3 pins)`.

### Task 2: Integration test harness (scratch DB + migrations)

**Files:** Create `tests/Integration/Rmg.IntegrationTests/{ScratchDb.cs, IntegrationTestBase.cs}`; `src/Database/{RmgDbContext.cs, DbBootstrapper.cs}`.
**Interfaces:**
- `RmgDbContext : DbContext` — constructor `RmgDbContext(DbContextOptions<RmgDbContext>)`; registers configurations from module Infrastructure assemblies (empty now, wired per-task later); Npgsql naming: `UseNpgsql(..., o => o.MapJsonB())`… default snake_case via explicit `HasSchema`/`HasColumnName` in configurations.
- `ScratchDb` — `static Task<ScratchDb> CreateAsync(string adminConnectionString)` → creates database `rmg_test_<8hexguid>`, returns `{ConnectionString, AdminConnectionString, DisposeAsync()}` (drops db, closes pool).
- `IntegrationTestBase` — xUnit `IAsyncLifetime`: builds ScratchDb, applies `DbBootstrapper.MigrateAsync(connString)`, exposes `RmgDbContext` + `HttpClient` against `Rmg.Api` via `WebApplicationFactory<Program>` with `UseSetting("ConnectionStrings:Default", scratch conn)`; `Program` gains `public static partial class Program { }` accessor.
**Steps:**
- [ ] Write failing test `Rmg.IntegrationTests.HarnessTests.DbMigrationsApply_CreateSchemas`: after bootstrap, `SELECT schema_name FROM information_schema.schemata` contains org, iam, pdm, ord, outbox, audit.
- [ ] Run → FAIL (no DbContext). Implement `DbBootstrapper.MigrateAsync` + empty `RmgDbContext`.
- [ ] Run → PASS. Commit `test+feat: integration harness on scratch databases`.

### Task 3: Envelope, problem details, correlation (R-1)

**Files:** Create `src/BuildingBlocks/Common/{Envelope.cs, RmgProblem.cs, ProblemExceptionHandler.cs, CorrelationMiddleware.cs, Page.cs}`; modify `src/Api/{Program.cs, appsettings.json}`; test `tests/Integration/Common/EnvelopeTests.cs`.
**Interfaces:**
- `Envelope<T>(T Data, Dictionary<string, object>? Meta)` + `static Envelope<T> Of(T data, object? meta = null)`.
- `RmgProblemException(string Code, string Title, int Status, Dictionary<string, string[]>? Errors = null, string? Detail = null)` + subclasses `ValidationProblem`, `ConflictProblem`, `NotFoundProblem`, `ForbiddenProblem`, `BusinessRuleProblem(code, detail)` (422).
- `Page.Normalize(int pageSize, string? cursor)` → clamps pageSize 1..100; `PageMeta(int PageSize, string? NextCursor, int? Total)`.
- `CorrelationMiddleware`: reads `X-Correlation-Id` or creates `Guid`; echoes header; stores `HttpContext.Items["correlationId"]`.
- `ProblemExceptionHandler` (`IExceptionHandler`): maps `RmgProblemException` → problem+json `{type,title,status,detail,code,errors,correlationId}`; unhandled → 500 `INTERNAL_ERROR` (no stack trace).
- `Program.cs`: `builder.Services.AddProblemDetails()` + `AddExceptionHandler<ProblemExceptionHandler>()`; `app.UseMiddleware<CorrelationMiddleware>()`.
**Steps:**
- [ ] Failing test `EnvelopeTests.Error_ProducesProblemShape`: GET a route that throws `BusinessRuleProblem("STYLE_CODE_TAKEN", "dup")` (temporary throwaway endpoint) → status 422, `code=STYLE_CODE_TAKEN`, body has `correlationId`, `Content-Type: application/problem+json`.
- [ ] Watch FAIL → implement → PASS → commit `feat: response envelope + problem-details + correlation middleware`.

### Task 4: Auth — JWT, login, permissions, tenant middleware (R-2)

**Files:** Create `src/BuildingBlocks/Auth/{JwtIssuer.cs, IJwtIssuer.cs, LoginService.cs, PermissionAttribute.cs, PermissionPolicyProvider.cs, PermissionHandler.cs, TenantResolutionMiddleware.cs}`; modify `src/Api/Program.cs`; `src/Database/RmgDbContext.cs` (register `AppUser` entity); `src/Modules/Identity/Domain/AppUser.cs`; test `tests/Integration/Auth/AuthTests.cs`.
**Interfaces:**
- `AppUser` (`iam.app_user`): `Id Guid, TenantId, Email string(255), PasswordHash string, DisplayName string(120), IsActive bool` + baseline columns; config: `UNIQUE(tenant_id,email)`, global tenant filter.
- `IJwtIssuer.IssueAsync(AppUser user, IEnumerable<string> perms) → (string token, int expiresInSeconds)`; claims: `sub`, `tid`, `fid`, `name`, `perm` (repeated); symmetric key `Jwt__Key` (≥32 chars, .env), issuer `rmg-api`, audience `rmg-web`, 8h.
- `POST /api/v1/auth/login` body `{email, password}` → `Envelope<{accessToken, expiresInSeconds, user:{id, displayName}}>`. Uses `Microsoft.AspNetCore.Identity.PasswordHasher<AppUser>` for verify. Rate limited: `AddRateLimiter` fixed-window `login` 10/min/IP → 429 problem.
- `[RequirePerm("pdm.style.create")]` attribute; `PermissionPolicyProvider` maps `perm:*` policies; `PermissionHandler.Succeed` iff claims contain code. Any missing → 403 `PERMISSION_DENIED` via `ForbiddenProblem` (never leak existence).
- `TenantResolutionMiddleware` (after UseAuthentication): claims `tid/fid/sub` → `TenantContext` (async-local via `ITenantContext` scoped service); missing → 401 `UNAUTHENTICATED`.
- `Program`: `AddAuthentication(JwtBearer).AddJwtBearer()` + `AddAuthorization()` + `app.UseAuthentication()` + tenant middleware + `UseAuthorization()`.
**Steps:**
- [ ] Failing tests `AuthTests`: `Login_WithSeededAdmin_ReturnsToken` (seed user via direct DbContext insert w/ PasswordHasher; then POST login → 200 + token), `Login_WrongPassword_401_INVALID_CREDENTIALS`, `Protected_WithoutToken_401`, `RequirePerm_MissingPerm_403_PERMISSION_DENIED` (probe endpoint with attribute), `Claims_PopulateTenantContext` (probe endpoint returning tenantId from `ITenantContext`).
- [ ] Watch FAIL → implement → all PASS → commit `feat: JWT auth, permission policies, tenant context middleware`.

### Task 5: Foundation migration + seed + reset (M001)

**Files:** `src/Database/{Migrations/001_Foundation.cs, Seeder.cs, DbReset.cs}` (EF migration generated via `dotnet ef migrations add 001_Foundation --project src/Database` then renamed ordering by `Migrationattribute "001_Foundation"`); entities: `src/Modules/Organization/Domain/{Tenant.cs, LegalEntity.cs, Facility.cs, TenantLocalePolicy.cs}`, `src/Modules/Identity/Domain/{Role.cs, Permission.cs, RolePermission.cs, UserRole.cs}`, `src/BuildingBlocks/{Outbox/OutboxMessage.cs, Audit/AuditEvent.cs, Idempotency/IdempotencyRecord.cs}`; configurations in each module's Infrastructure + BuildingBlocks.
**Interfaces (tables, exact):**
- `org.tenant`: id PK, name, code UNIQUE, default_locale text, require_maker_checker bool, + baseline (no tenant_id). `org.tenant_locale_policy`: id, tenant_id (FK), locale text, is_default, UNIQUE(tenant_id, locale).
- `org.legal_entity`: id, tenant_id, code, name, country char(2), UNIQUE(tenant_id, code), FK(tenant,id)→org.tenant.
- `org.facility`: id, tenant_id, legal_entity_id, code, name, timezone text, address text, UNIQUE(tenant_id, code), composite FK(tenant_id, legal_entity_id).
- `iam.role` UNIQUE(tenant_id, code); `iam.permission` (code PK-equivalent UNIQUE, description) **global**; `iam.role_permission` PK(role_id, permission_id); `iam.user_role` PK(user_id, role_id).
- `outbox.outbox_message`: id, event_type, schema_version int, tenant_id, aggregate_id, actor text, correlation_id, occurred_at_utc, recorded_at_utc, payload jsonb, status text CHECK in (Pending,Processed,Failed), attempts int, next_attempt_at_utc. Index `(status, next_attempt_at_utc)` partial WHERE status='Pending'.
- `audit.audit_event`: id, tenant_id, facility_id?, actor_id, actor_display, command, entity_type, entity_id, changed_fields jsonb, reason?, correlation_id, approval_ref?, occurred_at_utc. Index (tenant_id, occurred_at_utc).
- `iam.idempotency_record`: PK(tenant_id, endpoint, key_hash), request_hash, response_status int, response_json, created_at_utc.
- `Seeder.SeedAsync(db)`: tenant "TeamzLab Demo Ltd." (code `TZL`, default_locale `en`, locales {en,bn}), legal entity `TZL-HQ` (BD), facility `FAC-01` (Asia/Dhaka); roles Admin/Merchandiser/Approver; **all 21 permission codes** from spec §5 (org.read, iam.user.read, iam.user.manage, pdm.material.read, pdm.material.create, pdm.style.read/create/edit/approve, pdm.bom.read/create/edit/approve, ord.buyer.read/create/manage, ord.order.read/create/edit/confirm, audit.read); admin user `admin@tzl.demo` (password `Seed__AdminPassword`, default `Admin#12345` in Seeder param), Admin role. Idempotent (no-op if tenant exists).
- `DbReset`: `dotnet run --project src/Database -- reset` → drop pilot schemas → migrate → seed. Startup hook in Api: Development → `Seeder.SeedAsync` on boot.
**Steps:**
- [ ] Extend harness test: `M001_CreatesAllTables_AndSeedProducesAdminLogin` — after bootstrap + seed, login works and `SELECT count(*) FROM iam.permission` = 21.
- [ ] Watch FAIL → implement entities/configs/migration/seeder → PASS → commit `feat: foundation migration, permission catalogue, seed, db reset`.

### Task 6: Audit writer + outbox enqueue + dispatcher

**Files:** `src/BuildingBlocks/Audit/{IAuditWriter.cs, AuditWriter.cs}`, `src/BuildingBlocks/Outbox/{IOutboxWriter.cs, OutboxWriter.cs}`, `src/Workers/{Worker.cs, Program.cs}`, tests `tests/Integration/BuildingBlocks/{AuditTests.cs, OutboxTests.cs}`.
**Interfaces:**
- `IAuditWriter.WriteAsync(string command, string entityType, Guid entityId, object? before, object? after, string? reason = null)` — diff via reflection → `{field: {before, after}}` jsonb; **adds to current DbContext change set** (no SaveChanges — caller owns the transaction).
- `IOutboxWriter.EnqueueAsync(string eventType, object payload, Guid aggregateId)` — same pattern; `occurred_at=recorded_at=UtcNow`, status Pending; envelope payload `{eventId, type, schemaVersion:1, tenantId, aggregateId, actor, correlationId, occurredAtUtc, data}`.
- Workers: `OutboxDispatcher : BackgroundService` — every 5s batch 50 Pending due rows → `IOutboxDelivery.DeliverAsync(msg)` (pilot: structured log) → status Processed; failure → attempts++, `next_attempt = UtcNow + min(60s·2^attempts, 15min)`; attempts ≥ 10 → Failed (log `OUTBOX_DEAD_LETTER`).
**Steps:**
- [ ] Failing `AuditTests.WriteAsync_RecordsChangedFieldSet` (two SaveChanges in one tx → row with changed_fields) and `OutboxTests.Dispatcher_ProcessesPending_AndDeadLettersAfter10` (drive attempts by inserting Failed-due rows).
- [ ] Watch FAIL → implement → PASS → commit `feat: audit writer, outbox writer + dispatcher worker`.

### Task 7: Idempotency filter + revision concurrency + tenant-guard security tests

**Files:** `src/BuildingBlocks/Idempotency/{IdempotencyFilter.cs, IdempotencyAttribute.cs}`; `src/BuildingBlocks/Tenancy/{TenantEntity.cs, TenantContext.cs, ITenantContext.cs}` (move entity base here); modify Api registration; tests `tests/Security/TenantGuardTests.cs`, `tests/Integration/BuildingBlocks/IdempotencyTests.cs`.
**Interfaces:**
- `TenantEntity` base: `Id, TenantId, Revision int, CreatedAtUtc, CreatedBy, UpdatedAtUtc, UpdatedBy`; concurrency token `xmin` (`Propertybuilder…UseXminAsConcurrencyToken()` in base config).
- `[Idempotent]` filter: POST with `Idempotency-Key` → sha256(key) lookup `(tid, endpoint, key_hash)`: hit + matching `request_hash` → replay stored response (+ header `Idempotent-Replay: true`); hit + mismatch → 422 `IDEMPOTENCY_KEY_REUSED`; miss → execute, store status+body after success (2xx only).
- Concurrency: services catch `DbUpdateConcurrencyException` → `ConflictProblem("<AGG>_REVISION_CONFLICT", "expected revision …, current …")` 409.
**Steps:**
- [ ] Failing security tests (probe endpoint): `CrossTenant_GetById_Returns404` (row of tenant A fetched with tenant B token → 404, never 200/403-leaking), `QueryFilter_ExcludesOtherTenantRows` (list count), `Write_WithStaleRevision_409`, `Idempotent_Replay_SameResponse_NoSecondWrite`, `Idempotent_KeyReuse_DifferentBody_422`.
- [ ] Watch FAIL → implement → PASS → commit `feat: idempotency filter, xmin revision concurrency, tenant-guard security tests`.

### Task 8: ORG — facilities + locale policy endpoints [W1-A]

**Files:** `src/Modules/Organization/{Application/FacilityService.cs, Infrastructure/FacilityRepository.cs, Infrastructure/FacilityConfig.cs, Contracts/{FacilityDtos.cs, IOrganizationApi.cs}}`; `src/Api/Controllers/OrgController.cs`; tests `tests/Integration/Modules/Org/ FacilityEndpointTests.cs` (+ unit `tests/Unit/Organization/…` for any pure rule).
**Interfaces:** `GET /api/v1/org/facilities` → Envelope<FacilityDto[]> scoped to tid; `GET /org/facilities/{id}`. `FacilityDto(Guid Id, string Code, string Name, string Timezone, string Address)`. Perm `org.read` on both.
**Steps:**
- [ ] Failing: `Facilities_List_ScopedAndSeeded` (seeded FAC-01 returned), `Facilities_GetOtherTenantId_404`.
- [ ] FAIL → implement (repo uses `ITenantContext`) → PASS → commit `feat(org): facilities read endpoints`.

### Task 9: IAM — users & roles management endpoints [W1-B]

**Files:** `src/Modules/Identity/{Application/{UserService.cs, RoleService.cs}, Infrastructure/{UserRepository.cs, RoleRepository.cs, …Config}, Contracts/…}`; `src/Api/Controllers/IamController.cs`; tests `tests/Integration/Modules/Iam/…`.
**Interfaces:** `GET/POST /api/v1/iam/users` `{email, displayName, password, roleCodes[]}` (creates via PasswordHasher; perm `iam.user.manage`), `POST /iam/users/{id}/roles {roleCodes[]}` (replace set), `GET /api/v1/iam/roles` (perm `iam.user.read` on reads). Never return `passwordHash` (asserted in test).
**Steps:**
- [ ] Failing: `CreateUser_AssignsRoles_LoginWorks`, `CreateUser_DuplicateEmail_422_EMAIL_TAKEN`, `Users_List_NeverReturnsHash`, `CreateUser_WithoutManagePerm_403`.
- [ ] FAIL → implement → PASS → commit `feat(iam): user & role management endpoints`.

### Task 10: ORD — buyers + contacts (M002) [W2-A]

**Files:** `src/Modules/Merchandising/Domain/{Buyer.cs, BuyerContact.cs}` + Application/Infrastructure/Contracts; `src/Api/Controllers/BuyersController.cs`; migration `002_OrdBuyer` (module-owned tables only); tests.
**Interfaces (tables):** `ord.buyer`: id, tenant_id, code, name, country char(2), currency char(3), UNIQUE(tenant_id, code), baseline columns. `ord.buyer_contact`: id, tenant_id, buyer_id, name, email, phone, UNIQUE(tenant_id, buyer_id, email); composite FK(tenant_id, buyer_id).
- API: `GET/POST /api/v1/ord/buyers` (`ord.buyer.read` / `ord.buyer.create`), `GET /ord/buyers/{id}`. **Property-level authz:** contact emails/phones included only for holders of `ord.buyer.manage`; others get `contacts: [{name}]` (emails/phones stripped). Cursor pagination `pageSize/cursor`.
- Command semantics: POST creates in status Active with revision 1; response Envelope<BuyerDto>. Event `ord.buyer.and.customer.profile.changed.v1` enqueued + audit row (same tx).
**Steps:**
- [ ] Failing: `CreateBuyer_201_AndOutboxAuditAtomic` (envelope + 1 outbox row + 1 audit row), `List_Buyers_CursorPagination` (seed 3, pageSize 2 → nextCursor), `ContactFields_WithoutManagePerm_Stripped`, `DuplicateBuyerCode_422_BUYER_CODE_TAKEN`, `CrossTenant_BuyerId_404`.
- [ ] FAIL → implement → PASS → commit `feat(ord): buyer profile + contacts, M002`.

### Task 11: PDM — materials + UOM (M003) [W2-B]

**Files:** `src/Modules/ProductEngineering/Domain/{Material.cs, Uom.cs}` + layers; `MaterialsController.cs`; migration `003_PdmMaster`; tests.
**Interfaces:** `pdm.uom`: code text PK (seed: PC, KG, M, YD, M2, DOZ), name. `pdm.material`: id, tenant_id, code, name, category text?, base_uom_code FK→pdm.uom.code, UNIQUE(tenant_id, code).
- API: `GET/POST /api/v1/pdm/materials` (perms `pdm.material.read`/`pdm.material.create`); `GET /api/v1/pdm/uoms` (any authenticated). POST validates `baseUomCode` exists → 422 `UOM_INVALID` with per-field error.
**Steps:**
- [ ] Failing: `CreateMaterial_Happy`, `CreateMaterial_BadUom_422_UOM_INVALID`, `DuplicateCode_422`, `Uoms_List_Seeded6`.
- [ ] FAIL → implement → PASS → commit `feat(pdm): material master + uom, M003`.

### Task 12+13: PDM — styles, variants, BOM (M004) [W3, one implementer]

**Files:** Domain `{Style.cs, StyleVersion.cs, StyleVariant.cs, Bom.cs, BomLine.cs}` + Application/{StyleService.cs, BomService.cs} + Infrastructure configs + Contracts; `StylesController.cs`, `BomsController.cs`; migration `004_PdmStyle`; tests unit (state machines) + integration.
**Interfaces (tables):**
- `pdm.style`: id, tenant_id, code, name, season, category, buyer_id, status CHECK ('Draft','Approved'), UNIQUE(tenant_id, code), composite FK(tenant_id, buyer_id)→ord.buyer.
- `pdm.style_version`: id, tenant_id, style_id, version int, status CHECK ('Draft','Approved','Superseded'), approved_at_utc?, UNIQUE(tenant_id, style_id, version).
- `pdm.style_variant`: id, tenant_id, style_version_id, color_code, size_code, UNIQUE(tenant_id, style_version_id, color_code, size_code).
- `pdm.bom`: id, tenant_id, style_version_id, status CHECK ('Draft','Released'), UNIQUE(tenant_id, style_version_id) (one BOM per version).
- `pdm.bom_line`: id, tenant_id, bom_id, material_id, qty_per_unit numeric(18,4) CHECK > 0, uom_code FK→pdm.uom.code, waste_pct numeric(5,2) default 0, composite FKs with tenant_id.
- Commands: `POST /pdm/styles` (Draft + version 1 Draft + variants rows from `{variants:[{colorCode, sizeCode}]}`) perm `pdm.style.create`; `POST /pdm/styles/{id}/approve` perm `pdm.style.approve` → style Approved + current version Approved (guard: ≥1 variant; maker-checker flag respected); `POST /pdm/boms` `{styleVersionId}` perm `pdm.bom.create`; `POST /pdm/boms/{id}/lines {materialId, qtyPerUnit, uomCode, wastePct?}` perm `pdm.bom.edit` (only while Draft; validate uom + material → 422 `BOM_LINE_INVALID_UOM` / `MATERIAL_INVALID`); `POST /pdm/boms/{id}/approve` perm `pdm.bom.approve` → Released (guard: ≥1 line). State transitions guarded **in the write** (unit tests on `Style.Approve()` / `Bom.Release()` throw `InvalidStateProblem` outside allowed states). Events `pdm.style.master.creation.changed.v1` / `pdm.bill.of.materials.authoring.changed.v1` + audit per command.
**Steps:**
- [ ] Unit failing: `Style_Approve_Twice_Throws`, `Bom_Release_WithoutLines_Throws`, state-machine table test.
- [ ] Integration failing: `StyleLifecycle_Draft→Approve→VersionsApproved`, `StyleVariants_DuplicateColorSize_422`, `ApproveStyle_NoVariants_422_NO_VARIANTS`, `BomLifecycle_AddLine→Release`, `BomLine_ZeroQty_422_QTY_POSITIVE`, `BomRelease_PermRequired_403`, `Style_Code_Duplicate_422`, outbox/audit rows per command.
- [ ] FAIL → implement → PASS → commit `feat(pdm): styles, variants, BOM lifecycle, M004`.

### Task 14: ORD — customer orders + confirm (M005) [W4]

**Files:** Domain `{CustomerOrder.cs, OrderLine.cs, SizeQuantity.cs, DeliverySchedule.cs}` + Application/OrderService + Contracts; `OrdersController.cs`; migration `005_OrdOrder`; tests.
**Interfaces (tables):** `ord.customer_order`: id, tenant_id, facility_id, code, buyer_id, buyer_po, currency char(3), order_date date, status CHECK ('Draft','Confirmed','Cancelled'), UNIQUE(tenant_id, buyer_id, buyer_po). `ord.order_line`: id, tenant_id, order_id, line_no int, style_id, style_version_id, price numeric(18,2) ≥0, line_qty numeric(18,0) (derived = Σ sizes), composite FKs. `ord.size_quantity`: id, tenant_id, order_line_id, color_code, size_code, qty numeric(18,0) CHECK > 0, UNIQUE(order_line_id, color_code, size_code). `ord.delivery_schedule`: id, tenant_id, order_line_id, ship_date date, qty numeric(18,0) > 0.
- API: `POST /ord/customer-orders` `{code, buyerId, buyerPo, currency, orderDate, lines:[{styleVersionId, price, sizes:[{colorCode,sizeCode,qty}], schedules:[{shipDate,qty}]}]}` perm `ord.order.create` → Draft. Validations: style version **Approved** (`STYLE_VERSION_NOT_APPROVED`); every (color,size) exists in that version's variants (`ORDER_VARIANT_MISMATCH`, per-field errors); Σ line sizes == line_qty.
- `POST /ord/customer-orders/{id}/confirm` `{expectedRevision}` perm `ord.order.confirm` (+Idempotency-Key; +maker-checker when tenant flag on) → Confirmed, 200 Envelope; conflicts 409 `ORDER_REVISION_CONFLICT`. `POST /{id}/cancel {reason}` from Draft/Confirmed only. Events `ord.customer.order.capture.changed.v1` + audit.
**Steps:**
- [ ] Failing: `CaptureOrder_Happy_Draft` (sizes reconcile), `Capture_NonApprovedVersion_422`, `Capture_VariantMismatch_422_FieldErrors`, `DuplicateBuyerPo_422`, `Confirm_Happy_200` (+ outbox+audit), `Confirm_Replay_SameIdempotencyKey_NoDoubleEvent`, `Confirm_StaleRevision_409`, `Confirm_MissingPerm_403`, `Cancel_ThenConfirm_422_INVALID_STATE`, `CrossTenant_OrderId_404`.
- [ ] Extend `Seeder` with `DemoSeedAsync` (spec §8): buyer `BUY-001`, material set (fabric, thread, button…), style `STY-001` Approved with **3 colors × 4 sizes** variants, released BOM (≥2 lines), order `ORD-001` Confirmed — idempotent, runs inside `db reset` and Development boot (guard: skip if ORD-001 exists).
- [ ] Failing: `DemoSeed_ProducesBrowserReadyDataset` (login → orders list contains ORD-001 Confirmed; style list contains STY-001 Approved).
- [ ] FAIL → implement → PASS → commit `feat(ord): customer order capture + confirm lifecycle, M005`.

### Task 15: OpenAPI export + contract tests

**Files:** modify `src/Api/Program.cs` (`AddOpenApi()`); `scripts/export-openapi.ps1` (boots Api on 5199 alt port, GET `/openapi/v1.json`, writes `contracts/openapi/openapi.json`, stops); `tests/Contract/ContractTests.cs`.
**Steps:**
- [ ] Export → commit `contracts/openapi/openapi.json` (assert: every endpoint from Tasks 8–14 present, operationIds set, `application/problem+json` documented).
- [ ] Failing contract test: `OpenAPI_MatchesCommittedFile` (regenerate in-memory, compare canonical JSON) → implement export script → PASS → commit `test: openapi contract snapshot + export script`.

### Task 16: Web scaffold — tokens, i18n, shell (theme foundation)

**Files:** `apps/web` via `npx create-next-app@latest apps/web --ts --app --tailwind --eslint --src-dir --import-alias "@/*"` (pin exact version in package.json); `src/app/globals.css` (tokens from `G:\Practices\clone site as json\website-analysis\design-tokens.json`: primary #5d87ff, secondary #49beff, success #13deb9, warning #f6b51e, error #ef4444, info #8754ec + tint aliases, surfaces #ffffff↔#1c2536, borders #dfe5ef↔#333f55, muted rgba(90,106,133,.75)↔#7c8fac, radius 10px, DM Sans via next/font); `src/lib/i18n/{en.ts, bn.ts, getDict.ts}` (typed dictionaries; every UI string keyed); `src/lib/{api.ts, auth.ts}` (fetch wrapper reading httpOnly cookie `rmg_session`; `API_URL` from `NEXT_PUBLIC_API_URL`, default http://localhost:5000/api/v1); `src/components/theme/{ThemeProvider.tsx, ThemeToggle.tsx}` (html class + localStorage `rmg-theme`); shell `src/components/shell/{Sidebar.tsx, Header.tsx}` (268px sidebar, 72px sticky header, active pill, lang toggle, avatar stub); layout `src/app/[lang]/layout.tsx` + `src/app/[lang]/(full)/layout.tsx` + `not-found.tsx`; `docs/design/ui-direction.md` (synced instruction file per playbook — token table + screen checklist).
**Interfaces:** `getDict(lang): Dict` where `Dict` = nested typed object en/bn keys match (compile-time check); `apiFetch<T>(path, init?)` server-side with cookie bearer, throws `ApiError(status, code, errors)`; `Lang = "en" | "bn"`.
**Steps:**
- [ ] Scaffold + tokens + shell + both locales; `npm run build` clean.
- [ ] Playwright MCP: screenshot `/en/dashboard` placeholder + `/bn/dashboard` light & dark → review vs reference geometry (268px/72px/radius) → fix → commit `feat(web): shell, design tokens, i18n scaffold (en/bn)` + ui-direction file.

### Task 17: Login + session [W6-B, parallel with 18]

**Files:** `src/app/[lang]/login/page.tsx`, `src/app/api/auth/{login,logout}/route.ts` (proxy API login → set httpOnly cookie `rmg_session`, sameSite lax, 8h), middleware `src/middleware.ts` (redirect unauthenticated `(full)` → `/{lang}/login`; cookie present → allowed), login form component (40px controls, error banner from problem `code`/`errors[]`), bn+en strings.
**Steps:**
- [ ] Implement; manual MCP check: wrong password shows `INVALID_CREDENTIALS` message localized; login sets cookie + redirects to `/en/dashboard`.
- [ ] `npm run build` clean → commit `feat(web): login + session middleware`.

### Task 18: Dashboard [W6-C]

**Files:** `src/app/[lang]/(full)/dashboard/page.tsx` (server component; reads buyers/styles/orders counts + recent 5 orders via API), stat tiles (10–20% semantic tints), recent-orders table (tinted status badges), four states (loading skeleton via Suspense, empty, error with retry, success); registers **nav registry file** `src/components/shell/nav.ts` (controller owns merge of later entries); en/bn strings.
**Steps:**
- [ ] Implement (empty-state verified before seeding orders); screenshots light/dark × en/bn via MCP → review → commit `feat(web): operations dashboard`.

### Task 19: Buyers screens [W7-A] — no nav-file edits

**Files:** `src/app/[lang]/(full)/buyers/{page.tsx, [id]/page.tsx, new/page.tsx}` + `src/lib/actions/buyers.ts` (server actions: create buyer w/ Idempotency-Key uuid; contact fields per perm), list: cursor pagination controls + four states; detail: contacts section (manage-perm users see emails/phones), form validation errors inline (BUYER_CODE_TAKEN etc.).
**Steps:**
- [ ] Implement; MCP click-through: create buyer happy + duplicate-code error visible; screenshots light/dark → commit `feat(web): buyers screens`.

### Task 20: Styles screens [W7-B] — no nav-file edits

**Files:** `src/app/[lang]/(full)/styles/{page.tsx, [id]/page.tsx, new/page.tsx}` + `src/lib/actions/styles.ts`; **variant matrix editor** (add color codes × size codes grid, client component), approve button (guard: disabled w/o perm; 422 NO_VARIANTS shown inline), status badges Draft/Approved; detail shows version + variants + linked BOM status.
**Steps:**
- [ ] Implement; MCP: create style with 2×2 variants → approve → badge changes; duplicate code error path; screenshots → commit `feat(web): styles + variant matrix`.

### Task 21: BOM editor screens [W7-C] — no nav-file edits

**Files:** `src/app/[lang]/(full)/styles/[id]/bom/page.tsx` (or `src/app/[lang]/(full)/boms/[id]/page.tsx` — controller picks one and records in nav merge) + `src/lib/actions/boms.ts`; lines grid (material select, qty, uom, waste%), add-line validation errors (UOM_INVALID, QTY_POSITIVE) inline, release action w/ confirm dialog ("irreversible" confirmation per spec UX sheet).
**Steps:**
- [ ] Implement; MCP: add valid line → release → status Released; invalid uom error visible; screenshots → commit `feat(web): BOM editor`.

### Task 22: Orders screens [W7-D] — no nav-file edits

**Files:** `src/app/[lang]/(full)/orders/{page.tsx, new/page.tsx, [id]/page.tsx}` + `src/lib/actions/orders.ts`; capture form (buyer select, PO no, currency, per-line: approved style version select → variant grid with qty inputs validating (color,size) membership client-side too), confirm button passing `expectedRevision` from loaded detail; conflict 409 renders banner with "refresh to revision N" affordance; cancel w/ reason from Draft/Confirmed.
**Steps:**
- [ ] Implement; MCP: full capture → confirm → status Confirmed; stale-revision banner verified by double-submit in two tabs; screenshots → commit `feat(web): order capture + confirm`.

### Task 23: E2E, smoke, regression, merge wave [W8, controller]

**Files:** `tests/E2E` npm project (`@playwright/test`, `playwright.config.ts` baseURL http://localhost:3101, `API_URL` http://localhost:5101/api/v1, global-setup seeds fresh DB via `db reset`), `scripts/smoke.sh` (curl every endpoint: login, facilities, users, roles, materials, uoms, styles, approve, boms, lines, release, buyers, orders, confirm + 3 negative probes expecting 4xx), `docs/01_Plan.md` done-log updates.
**Steps:**
- [ ] E2E spec `flow.spec.ts`: login → create buyer → style 2×2 → approve → BOM line → release → order capture → confirm → dashboard shows Confirmed. Run green on agent ports; **stop both servers after**.
- [ ] Screenshots light/dark × en/bn of login, dashboard, buyers, styles, BOM, orders → `.superpowers/sdd/foundation-pilot/screenshots/` → review.
- [ ] Full regression on `integration`: backend suite (`dotnet test` all 4 projects), `npm run build`, smoke script → all green.
- [ ] Merge `integration → main`; ledger `.superpowers/sdd/foundation-pilot/LEDGER.md` final entries; commit + tag `pilot-slice-v0.1`.

## Review Focus

1. **Cross-tenant ID substitution on every `/GET {id}`** — a person expects another tenant's order ID to behave as if it doesn't exist (404, never 200/500). Pinned: `TenantGuardTests.CrossTenant_GetById_Returns404` (T7) + per-module `CrossTenant_*Id_404` tests (T8, T10, T12, T14).
2. **Double-submit of confirm (two tabs / retry storm)** — person expects exactly one confirmation and identical response on retry. Pinned: `Confirm_Replay_SameIdempotencyKey_NoDoubleEvent` (T14) + `Idempotent_Replay_SameResponse_NoSecondWrite` (T7).
3. **Concurrent edit → stale revision** — person expects a clear "someone changed this, refresh" message, not silent overwrite. Pinned: `Confirm_StaleRevision_409` (T14) + xmin token tests (T7).
4. **Bangla UI with Latin business codes** — person expects labels/dates/numbers localized while codes like `FAC-01`, `STYLE-001` stay Latin and LTR inside RTL-safe layout. Pinned: T16/T18/T22 screenshot reviews include `/bn/` routes; E2E asserts a bn dictionary key renders (T23).
5. **Boundary junk inputs** (negative/zero qty, bad UOM, variant mismatch, duplicate PO) — person expects per-field 422 problems and **no partial write** (no orphan order/line rows). Pinned: `Capture_VariantMismatch_422_FieldErrors`, `BomLine_ZeroQty_422_QTY_POSITIVE`, `DuplicateBuyerPo_422` (T14), `CreateMaterial_BadUom_422_UOM_INVALID` (T11).
