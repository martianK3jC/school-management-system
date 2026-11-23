# Learner CRUD — Complete Study Guide

> **Purpose**: This document explains every architectural decision, pattern, and implementation detail in the Learner CRUD module. It is written for a learner who wants to understand *why* things are built this way, not just *how*.

---

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Domain Model Decisions](#domain-model-decisions)
3. [Database Schema & EF Core Configuration](#database-schema--ef-core-configuration)
4. [DTO Design](#dto-design)
5. [Service Layer](#service-layer)
6. [Dependency Injection](#dependency-injection)
7. [Blazor Server UI Patterns](#blazor-server-ui-patterns)
8. [Security & Multi-Tenancy](#security--multi-tenancy)
9. [Common Pitfalls Encountered](#common-pitfalls-encountered)
10. [Self-Assessment Checklist](#self-assessment-checklist)
11. [Prerequisites Before Running](#prerequisites-before-running)
12. [Next Steps After Mastering This](#next-steps-after-mastering-this)

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────┐
│                  Blazor Server UI                    │
│  (AddLearner.razor / EditLearner.razor / Index.razor)│
└──────────────────────┬──────────────────────────────┘
                       │ ILearnerService
┌──────────────────────▼──────────────────────────────┐
│              LearnerService (Scoped)                 │
│  - Validates business rules                          │
│  - Maps DTOs ↔ Entities                              │
│  - Handles concurrency                               │
└──────────────────────┬──────────────────────────────┘
                       │ EF Core / DbContextFactory
┌──────────────────────▼──────────────────────────────┐
│         SchoolDbContext (Supabase/PostgreSQL)        │
│  - Entity configurations                             │
│  - Concurrency tokens                                │
│  - Composite indexes                                 │
└─────────────────────────────────────────────────────┘
```

### Why This Layering?

- **UI never touches DbContext directly** → prevents accidental lazy loading, N+1 queries, and disposed-context bugs in Blazor Server's long-lived component lifecycle.
- **Service owns all business logic** → validation, mapping, and concurrency handling live in one testable place.
- **DbContextFactory over injected DbContext** → Blazor Server components are long-lived; a scoped DbContext would be disposed after the first operation. The factory creates a fresh context per operation.

---

## Domain Model Decisions

### Why Does Learner Live in `Enrollment`, Not `Academics`?

A Learner is an *enrolled person*, not an academic concept. Academics contains GradeLevel, Subject, Curriculum — structural definitions that exist independently of any student. Enrollment captures the relationship between a person and the school: their LRN, section assignment, guardian, and status. If a student transfers out, their Learner record changes status but GradeLevel and Subject remain untouched. This separation follows the **bounded context** principle from domain-driven design.

### Why Guid PKs Instead of Int?

- **Multi-tenancy safety**: Sequential ints leak tenant boundaries (tenant A sees ID 1–100, infers tenant B starts at 101). GUIDs are opaque.
- **Offline/distributed creation**: Records can be created without a database round-trip for ID generation.
- **Supabase compatibility**: Supabase's auth and realtime features assume UUID primary keys.
- **Trade-off acknowledged**: GUIDs are larger (16 bytes vs 4), slower to index, and harder to read in URLs. For a school management system with <1M learners per institution, this is acceptable.

### Why `= string.Empty` on String Properties?

EF Core treats `null` and `string.Empty` differently in PostgreSQL. Nullable strings require `?` annotations and null-checks everywhere. By defaulting to `string.Empty`, we enforce that these fields are always present (validated at the service layer) and avoid null-reference exceptions in LINQ projections and Razor bindings. Required fields like `FirstName` and `Lrn` should never be null semantically.

### Why `DeleteBehavior.Restrict`?

A Learner has related records: enrollments, attendance, grades. Deleting a Learner silently cascading would destroy audit history. `Restrict` forces explicit handling — you must archive or reassign dependent records before deletion. This is a **data integrity safeguard**, not an inconvenience.

---

## Database Schema & EF Core Configuration

### Composite Unique Index on (InstitutionId, Lrn)

LRN uniqueness is *per institution*, not global. Two schools can have a learner with LRN "12345". The composite index enforces this at the database level, making race-condition-safe what application-level checks alone cannot guarantee.

```csharp
builder.HasIndex(l => new { l.InstitutionId, l.Lrn })
    .IsUnique()
    .HasDatabaseName("IX_Learners_InstitutionId_Lrn");
```

### Version as Concurrency Token

```csharp
builder.Property(l => l.Version)
    .IsRowVersion()      // PostgreSQL: xmin system column
    .IsConcurrencyToken();
```

Every UPDATE includes `WHERE "Version" = @originalVersion`. If another user modified the row between your SELECT and UPDATE, zero rows are affected and EF throws `DbUpdateConcurrencyException`. The service catches this and returns a user-friendly error instead of crashing.

> **Note**: On Supabase/PostgreSQL, `.IsRowVersion()` maps to the `xmin` system column automatically. You do NOT need a separate `byte[] Version` column in your migration unless targeting SQL Server. Verify your actual schema matches the configuration.

### Why No Cascade Delete on GradeLevel FK?

GradeLevels are reference data shared across many learners. Deleting a GradeLevel should fail if learners reference it, not orphan them. This is configured via:

```csharp
builder.HasOne(l => l.GradeLevel)
    .WithMany()
    .HasForeignKey(l => l.GradeLevelId)
    .OnDelete(DeleteBehavior.Restrict);
```

---

## DTO Design

### Why DTOs Instead of Returning Entities?

1. **Serialization safety**: Entities have navigation properties that cause circular references when serialized to JSON.
2. **API contract stability**: Internal entity refactors shouldn't break UI code.
3. **Projection efficiency**: DTOs let us select only needed columns (`AsNoTracking` + projection = no change tracking overhead).
4. **Input validation boundary**: Create/Update DTOs carry validation attributes separate from persistence concerns.

### Why Records for DTOs?

Records provide value equality, immutability by default, and concise syntax. DTOs are pure data carriers — they should not have mutable state or behavior. Records enforce this contract at the type level.

```csharp
public record LearnerDto(
    Guid Id,
    string Lrn,
    string FullName,
    string GradeLevelName,
    string SectionName,
    string Status);
```

### Why Separate Create/Update DTOs?

Create doesn't have an Id (server generates it). Update requires Id and Version (for concurrency). Merging them into one type forces nullable Id/Version on Create and optional fields on Update — both are lies. Separate types make illegal states unrepresentable.

---

## Service Layer

### Why `GetByIdAsync` Returns Nullable Instead of Throwing?

Throwing on missing records conflates "not found" with "system error." In Blazor, a thrown exception crashes the circuit. Returning `null` lets the UI decide: show a 404 message, redirect, or display an inline error. Exceptions are reserved for truly exceptional conditions (DB down, constraint violation).

### Why Check LRN Uniqueness Before Insert?

The composite unique index will reject duplicates, but catching it at the DB level produces a raw Postgres exception that leaks schema details. Pre-checking allows a clean, localized error message: *"A learner with LRN {lrn} already exists in this institution."* The index remains as the ultimate safety net for race conditions.

### Context-Per-Operation Pattern

```csharp
public async Task<LearnerDto?> GetByIdAsync(Guid id, Guid institutionId)
{
    await using var context = await _dbFactory.CreateDbContextAsync();
    // ... query
}
```

Blazor Server components live for the duration of a SignalR connection. A single injected DbContext would accumulate tracked entities, grow stale, and eventually dispose mid-session. Creating a fresh context per method call ensures isolation, freshness, and proper disposal.

### AsNoTracking() Usage

Used on **read-only** queries where returned entities won't be modified. Skips change tracker overhead, reducing memory and CPU. Never use on queries whose results will be updated — EF needs tracking to generate correct UPDATE statements.

---

## Dependency Injection

### Why Scoped Lifetime for LearnerService?

The service depends on `IDbContextFactory<SchoolDbContext>`, which is registered as scoped. A singleton service holding a scoped dependency causes captive dependency bugs. Scoped matches the Blazor Server circuit lifetime and aligns with per-request semantics.

### Why Interface-Based Registration?

```csharp
services.AddScoped<ILearnerService, LearnerService>();
```

Components depend on `ILearnerService`, not the concrete class. This enables:
- Unit testing with mocks
- Swapping implementations (e.g., cached decorator)
- Clear contract documentation via the interface

---

## Blazor Server UI Patterns

### Why Plain `<select>` Instead of `<InputSelect>` for Guid Binding?

`<InputSelect<T>>` has known issues binding to `Guid` in .NET 8 Blazor Server — it treats the value as a string and fails conversion silently. A plain `<select>` with `@bind="Model.GradeLevelId"` and manual `@onchange` handling gives reliable Guid binding. This is a documented framework limitation, not a design choice.

### Debounced Search Implementation

```csharp
private CancellationTokenSource? _searchCts;

private async Task OnSearchInputChanged(ChangeEventArgs e)
{
    _searchCts?.Cancel();
    _searchCts = new CancellationTokenSource();
    var token = _searchCts.Token;

    await Task.Delay(300, token); // debounce
    if (!token.IsCancellationRequested)
    {
        SearchTerm = e.Value?.ToString() ?? "";
        await LoadLearnersAsync();
    }
}
```

Without debouncing, every keystroke triggers a DB query. The 300ms delay collapses rapid typing into a single query. Cancellation ensures stale searches don't overwrite newer results.

### Why `@onclick:stopPropagation` on Modal Content?

Clicking inside the modal dialog should not bubble to the backdrop overlay's close handler. Without `stopPropagation`, clicking a form field or button inside the modal accidentally closes it.

### Why LearnerFormModel Is a Class, Not a Record?

Forms require mutable state bound to input fields. Records are immutable by default and fight against two-way binding. Classes with `{ get; set; }` properties work naturally with `EditForm` and `@bind`. Mutability here is a feature, not a flaw.

---

## Security & Multi-Tenancy

### CurrentInstitutionId Hardcoding (Temporary)

Currently hardcoded to `00000000-0000-0000-0000-000000000001` for development. In production, this comes from authenticated user claims:

```csharp
// Future: 
var institutionId = User.FindFirst("institution_id")?.Value
    ?? throw new UnauthorizedAccessException();
```

All queries filter by InstitutionId. This is the **tenant isolation boundary**. Missing this filter is a critical security vulnerability — always verify every query includes it.

---

## Common Pitfalls Encountered

| Pitfall | Symptom | Fix |
|---------|---------|-----|
| Injecting DbContext directly in Blazor component | ObjectDisposedException after first operation | Use IDbContextFactory, create context per operation |
| Forgetting InstitutionId filter | Cross-tenant data leakage | Always include WHERE InstitutionId = @id |
| Using InputSelect<Guid> | Silent binding failure, null values | Use plain <select> with manual binding |
| Not cancelling previous search | Out-of-order results overwriting current | CancellationTokenSource with Cancel() before new delay |
| Returning entity from service | Circular reference on serialize | Project to DTO with AsNoTracking |
| Mutable record for form model | Binding errors, compile failures | Use class for form models, records for DTOs |

---

## Self-Assessment Checklist

After studying, can you answer these without looking at code?

- [ ] Why does Learner live in Enrollment, not Academics?
- [ ] Why Guid PKs instead of int?
- [ ] Why `= string.Empty` on string properties?
- [ ] What does `DeleteBehavior.Restrict` do and why use it?
- [ ] Why composite unique index on (InstitutionId, Lrn)?
- [ ] What does `IsConcurrencyToken()` do?
- [ ] Why DTOs instead of returning entities?
- [ ] Why records for DTOs?
- [ ] Why `GetByIdAsync` returns nullable instead of throwing?
- [ ] Why context-per-operation pattern in Blazor Server?
- [ ] What does `AsNoTracking()` do and when to use it?
- [ ] How does IQueryable composition work?
- [ ] Why check LRN uniqueness before insert?
- [ ] Why Scoped DI lifetime for the service?
- [ ] Why interface-based registration?
- [ ] Why plain `<select>` instead of `<InputSelect>` for Guid binding?
- [ ] How does debounced search work?
- [ ] Why `@onclick:stopPropagation` on modal content?
- [ ] Why LearnerFormModel is a class, not a record?
- [ ] Trace the complete Add Learner flow from click to database

---

## Prerequisites Before Running

You need seed data in Supabase before Add Learner works:

1. An Institution record with ID `00000000-0000-0000-0000-000000000001` in `school."Institutions"`
2. At least one GradeLevel record linked to that institution in `school."GradeLevels"`

Without these, the grade level dropdown is empty and FK constraints reject inserts.

---

## Next Steps After Mastering This

- [ ] Add proper authentication → replace hardcoded `CurrentInstitutionId` with user claims
- [ ] Create Section entity → promote `SectionName` string to FK (see [SECTION_ENTITY_PLAN](./docs/SECTION_ENTITY_PLAN.md))
- [x] ~~Add `GetFullByIdAsync`~~ → **Done.** Edit form now loads guardian fields and actual Version via projection DTO. See [`LearnerService.GetFullByIdAsync`](../src/SchoolManagementSystem.Infrastructure/Services/LearnerService.cs) and [`EditLearner.razor`](../src/SchoolManagementSystem.Web/Components/Pages/Learners/EditLearner.razor).
- [ ] Add max-length constraints → `[MaxLength(100)]` on string properties + DB migration
- [x] ~~Configure Version as concurrency token~~ → **Done.** `Version` uses PostgreSQL `xmin` rowversion via `.IsRowVersion().IsConcurrencyToken()`. Optimistic concurrency tested in `UpdateAsync`. See [`SchoolDbContext.cs`](../src/SchoolManagementSystem.Infrastructure/Data/SchoolDbContext.cs) and [Concurrency Testing Notes](./docs/CONCURRENCY_TESTING.md).
- [ ] Add unit tests for LearnerService
- [ ] Build reusable form/table components → reduce duplication across future CRUD pages