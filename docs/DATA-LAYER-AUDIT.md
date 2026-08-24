# Data layer audit (LiberationFleet.Server)

Audit of the EF Core schema, lookup usage, redundancy, and query patterns. Remediation items in this PR are marked **Fixed**; remaining follow-ups are intentional backlog.

## Architecture snapshot

- **Stack:** ASP.NET Core + EF Core 10 / SQL Server, CQRS (MediatR) → repository interfaces → `ApplicationDbContext`.
- **Entities:** ~78 domain entities, ~79 `DbSet`s, ~40 enums (statuses/kinds stored as ints).
- **Lookups already seeded:** `PaymentPlatform` (global catalog), `LibraryCategory` (+ M:N `LibraryOfferingCategory`).
- **Soft deletes:** Manual `!IsDeleted` filters (no global query filters). Lazy loading is off.

## What was fixed in this pass

| Issue | Fix |
|-------|-----|
| N+1 in `MergePlaceholderIdentityDataAsync` (per-threshold DB round-trip) | Batch-load claimant thresholds into a dictionary |
| `AppDonation.Status` magic strings (`"pending"` / `"completed"` / `"failed"`) | `AppDonationStatus` enum + int column migration |
| Contribution aggregates string-matching `"Library of Things"` | `CrewPaymentPlatform.IsLibraryOfThings` flag + filtered unique index; queries use the flag |
| LoT platform selectable as a payment method | Excluded from platform list APIs; blocked in `EnsurePlatformAsync` / profile updates |
| Unused unbounded forum list APIs | Removed `GetByCrewIdAsync` / `GetByFleetIdAsync` (paged APIs remain) |
| Tracking overhead on read-heavy lists | `AsNoTracking` on forum pages/comments, chat room/message lists, security alerts, proposal lists/comments, gift contribution aggregates |
| Unbounded security alerts / proposal lists | Safety caps: alerts `Take(100)`, proposals `Take(500)`, proposal comments `Take(1000)` |

Migration: `20260824160000_NormalizeDonationStatusAndLotPlatformFlag`.

Startup also runs `LotPlatformSchemaRepair` (idempotent) and keeps `/api` + hubs on **503** until migrate+repair finish, so clients are not served against a half-updated schema.

## Schema health (current assessment)

### Good practices already in place

- Statuses and kinds are overwhelmingly **enums** (int), not free-text.
- Explicit Fluent relationships, cascade/restrict choices for SQL Server multiple-cascade paths.
- Filtered unique indexes (e.g. gift likes, season primary cycle, LoT platform).
- Check constraints where XOR relationships apply (crew/fleet scope, gift/comment likes).
- Recent perf indexes on notifications, gifts, and crew memberships.
- Gift-log and notification feeds already paginate and often use `AsNoTracking` / `AsSplitQuery`.

### Intentional denormalization (keep)

These duplicate data for read performance or E2EE display; do not “normalize away” without a product reason:

- `Proposal.ApproveCount` / `DisapproveCount` (vote tallies)
- `CrewMembership.CurrentPriorityScore` (cached season score)
- `EmergencyRequest.AmountSplitCommitted`
- `LibraryOffering.TitleNormalized` / preview fields; `Gift.LibraryItemTitle`
- Proposal detail title/description snapshots and `RolesJson` / `ChangesJson` blobs (audit of what was proposed)
- Parallel crew/fleet setting columns (product-parallel domains)

### Remaining improvements (follow-up)

1. **Global soft-delete query filters** for entities with `IsDeleted`, with `IgnoreQueryFilters()` where activity feeds must report deleted resources (`UserActivityRepository` `ResourceExists` pattern). High value, needs careful call-site audit.
2. **Default `QueryTrackingBehavior.NoTracking`** on `DbContext` options, opting into tracking only in write paths — reduces accidental tracking cost.
3. **DTO projections** on gift-log / forum list endpoints instead of full `User` graphs (username/avatar only).
4. **`User.IdentityGroups` CSV** → optional normalized join table if filtering/reporting by group becomes hot.
5. **Crew roles as bit flags** on `CrewMembership` — fine for a fixed role set; only introduce a roles table if roles become user-defined.
6. **Global `PaymentPlatform` unused by gifts** — crew platforms are free-text by design; optional FK to catalog for known brands later.
7. **Paginate** remaining crew-scoped unbounded lists (open emergency requests, library request lists) if they grow in production.
8. **Split `OnModelCreating`** into `IEntityTypeConfiguration<>` modules for maintainability (no runtime effect).

## Query checklist for new code

- Prefer enums / bool flags over string equality in filters.
- Use `AsNoTracking` for read-only queries; keep tracking when mutating returned entities.
- Always bound list endpoints (`Skip`/`Take` or keyset pagination).
- Avoid loops with `await` DB calls — preload into dictionaries.
- Soft-deleted rows: filter `!IsDeleted` (until global filters land).
- Include only navigations needed; prefer `Select` projections for list DTOs.

## Verification

Run after applying the migration:

```bash
dotnet ef database update --project LiberationFleet.Server
dotnet test LiberationFleet.Server.Tests
```
