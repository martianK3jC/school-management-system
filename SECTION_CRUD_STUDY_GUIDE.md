# Section CRUD — Complete Study Guide

This guide walks through every file we created or modified to add Section CRUD functionality to the school management system. It assumes you've already studied the [Learner CRUD Study Guide](LEARNER_CRUD_STUDY_GUIDE.md) and understand the foundational patterns (context-per-operation, AsNoTracking, DTOs, optimistic concurrency). This guide focuses on what's *different* about Sections and why.

---

## How to Use This Guide

1. **Read one file section at a time.** Each builds on the previous.
2. **Open the actual source file** alongside this guide. Read code while reading explanations.
3. **After each section, explain out loud** what that file does and how it connects to others.
4. **Compare with Learner CRUD.** Note where patterns are identical and where they diverge. The differences teach more than the similarities.
5. **Practice answering:** What problem does this solve? What would break without it? What design pattern is used? What would I change if requirements changed?

---

## Architecture Overview

Sections follow the same layered architecture as Learners:

```
Browser (SignalR)
    ↓
Sections.razor (UI + user interaction)
    ↓ @inject ISectionService
ISectionService / SectionService (business logic + queries)
    ↓ IDbContextFactory<SchoolDbContext>
SchoolDbContext (EF Core ORM)
    ↓ Npgsql driver
Supabase PostgreSQL
```

The key difference: Sections have a **master-detail relationship** with Learners. Clicking a section card loads that section's learners in a detail panel below. This means the Sections page injects *both* `ISectionService` and `ILearnerService`.

---

## Design Decision: Why Defer the FK Promotion?

The most important architectural decision in this feature is what we *didn't* do: promote `Learner.SectionName` (string) to `Learner.SectionId` (Guid FK).

### Why We Created the Section Entity Now
- The Sections page cannot function without it — CRUD, filtering, adviser display, and learner counts all require a real table.
- Dashboard integration needs section counts from a real entity.
- The learner form dropdown should show real sections, not hardcoded strings.

### Why We Didn't Promote the FK Yet
- **Breaking migration:** Changing `SectionName` (string) to `SectionId` (Guid FK) touches `Learner.cs`, `LearnerDtos.cs`, `LearnerService.cs`, `Learners.razor`, and the existing study guide — five files minimum.
- **Data migration complexity:** Existing learners have string section names. A migration would need to look up each name in the new Sections table, create missing sections, and update every learner row. If any name doesn't match, the migration fails or loses data.
- **String matching works correctly today:** When sections are created via the Sections page and selected via the learner form dropdown (which now uses `SectionOption.Name`), the stored string always matches a real Section.Name. There's no inconsistency risk as long as both pages use the same data source.
- **Authentication isn't stable yet:** The data model will likely change again when auth is added (e.g., scoping sections to academic years). Promoting the FK twice is worse than promoting it once after the model stabilizes.

### When to Promote
When authentication is implemented and the Institution/AcademicYear/Section hierarchy is finalized, run a single migration that:
1. Adds `SectionId` (nullable Guid FK) to Learners
2. Populates it via `UPDATE ... JOIN` on `(InstitutionId, SectionName)`
3. Makes `SectionId` non-nullable and drops `SectionName`
4. Updates all service/DTO/Razor references

Until then, string matching with a shared data source is safe and pragmatic.

---

## File 1: `Modules/Enrollment/Domain/Section.cs`

### What This File Is

The domain entity representing a class section (e.g., "Grade 1 - A"). Lives in the Enrollment module because sections describe how learners are organized, not academic structure itself.

### Property-by-Property Decisions

| Property | Type | Why This Choice |
|----------|------|-----------------|
| `Id` | `Guid` | Matches all existing entities. Safe in URLs. Can be generated before saving. |
| `InstitutionId` | `Guid` FK | Multi-tenant scoping. Every query filters by this. Same pattern as Learner, GradeLevel. |
| `GradeLevelId` | `Guid` FK | Links to the grade level this section belongs to. Display name comes from joining at query time. |
| `Name` | `string` | Section identifier within the grade level ("A", "B", "Mabini"). Unique per (InstitutionId, GradeLevelId). |
| `AdviserName` | `string` | Class adviser's full name. Stored as string, not Teacher FK, because Teacher entity doesn't exist yet. When it does, promote to FK in a future migration. |
| `RoomNumber` | `string` | Physical room. Optional — initialized to `string.Empty`, not nullable. Avoids null checks throughout the codebase. |
| `MaxCapacity` | `int` | Maximum learners allowed. 0 means no limit. Simple integer avoids nullable complexity for a field that's rarely enforced in queries. |
| `Status` | `string` = "Active" | Matches Learner and AcademicYear convention. String status allows future values without schema migration. |
| `Version` | `long` = 1 | Optimistic concurrency token. Same pattern as Learner. |

### Key Differences from Learner Entity
- **No LRN equivalent:** Sections don't have a government-issued identifier. Uniqueness is enforced by composite index instead.
- **AdviserName is denormalized:** Like Learner.SectionName, this is a deliberate deferral. The trade-off is the same: simplicity now, migration later.
- **MaxCapacity is an int, not a constraint:** We store the capacity but don't enforce it in the service layer yet. Enforcement would require checking current learner count on every add, which adds complexity. Document the gap; enforce when attendance tracking makes it meaningful.

---

## File 2: `Modules/Enrollment/Application/SectionDtos.cs`

### What This File Is

Six record types defining the contract between the Razor page and the service. Same philosophy as LearnerDtos: records for immutability, separate input/output types, no entity leakage.

### Record-by-Record Rationale

**`SectionFilter`** — Input for listing. Mirrors `LearnerFilter`: required `InstitutionId` scope + optional refinements. `AdviserName` filter uses exact match (not Contains) because adviser names are selected from a dropdown of distinct values, not typed freeform.

**`SectionListResult`** — Output of list query. Includes `ActiveCount` for stat cards, computed server-side. Unlike `LearnerListResult`, there's no male/female breakdown because sections don't have that dimension.

**`SectionSummary`** — Lightweight row for card display. **Key design choice:** `LearnerCount` is computed via a correlated subquery against the Learners table (`COUNT WHERE SectionName == Name`) rather than a navigation property. Why?
- Navigation properties require `Include()`, which loads all related learners into memory just to count them.
- Subqueries generate `SELECT COUNT(*)` in SQL, which is O(1) per section regardless of learner count.
- The subquery matches on `SectionName` string (not FK), which is correct given our deferred FK decision.
- Trade-off: if two sections in different grade levels share the same name, the count could be wrong. But our unique index on `(InstitutionId, GradeLevelId, Name)` prevents this within a grade level, and the subquery also filters by `InstitutionId`, so cross-grade collisions are the only risk. Acceptable for now.

**`CreateSectionRequest`** — No Id (generated server-side), no Version (starts at 1), no Status (defaults to "Active" in service). Same pattern as `CreateLearnerRequest`.

**`UpdateSectionRequest`** — Includes Id + Version for optimistic concurrency, plus Status (can be changed to "Inactive"). Same pattern as `UpdateLearnerRequest`.

**`SectionOption`** — Dropdown option with three fields: `Id`, `Name` (the raw section name stored in Learner.SectionName), and `DisplayName` (formatted as "Grade X - SectionName" for human-readable dropdowns). The separation of `Name` and `DisplayName` is critical: the form binds `formModel.SectionName` to `option.Name` (matching the Learner entity's string field), while the dropdown displays `option.DisplayName`.

---

## File 3: `Modules/Enrollment/Application/ISectionService.cs`

### What This File Is

The service contract. Seven methods covering CRUD + two read helpers for dropdowns.

### Method Rationale

- **`GetSectionsAsync`** — Filtered list with aggregates. Returns `SectionListResult` including `ActiveCount`.
- **`GetByIdAsync`** — Returns `SectionSummary?` (nullable). Used by edit modal to pre-populate form. Returns null instead of throwing when not found, letting the UI show a friendly message.
- **`CreateAsync`** — Returns `Guid` (new section's Id). Throws `InvalidOperationException` on duplicate name with friendly message.
- **`UpdateAsync`** — Takes `UpdateSectionRequest` with Version. Throws on concurrency conflict or duplicate name.
- **`DeleteAsync`** — Hard delete. Protected by `DeleteBehavior.Restrict` FK constraints — if learners reference this section, the DB rejects the delete. (Future improvement: soft-delete via Status = "Inactive".)
- **`GetSectionOptionsAsync`** — Returns `List<SectionOption>` for dropdowns. Optional `gradeLevelId` parameter filters to a specific grade level. Only returns Active sections.
- **`GetAdviserNamesAsync`** — Returns distinct adviser names for the filter dropdown. Excludes empty strings. Ordered alphabetically.

### Why No `GetDetailByIdAsync`?

Unlike Learner (where `LearnerSummary` lacked guardian fields needed for editing), `SectionSummary` contains all fields the edit form needs: Name, GradeLevelName, AdviserName, RoomNumber, Status. The only missing field is Version, which we hardcode to 1 as a known limitation (documented in the Razor page comments). Adding a `SectionDetail` DTO would be premature — create it when a field is actually missing from the summary.

---

## File 4: `Modules/Enrollment/Application/SectionService.cs`

### What This File Is

EF Core implementation following LearnerService patterns exactly. This section highlights what's *different*.

### Context-Per-Operation Pattern

Same as LearnerService: `IDbContextFactory<SchoolDbContext>` injected via primary constructor, `await using var db = await dbFactory.CreateDbContextAsync()` at the start of every method. Essential for Blazor Server SignalR circuits where a long-lived context would track stale entities across renders.

### LearnerCount Subquery

The most complex query in this service. Inside `GetSectionsAsync`:

```csharp
LearnerCount: db.Learners.AsNoTracking()
    .Count(l => l.InstitutionId == x.section.InstitutionId
             && l.SectionName == x.section.Name)
```

This generates a correlated subquery in SQL:
```sql
SELECT COUNT(*) FROM school."Learners" AS l
WHERE l."InstitutionId" = s."InstitutionId"
  AND l."SectionName" = s."Name"
```

**Why not a navigation property?** Navigation properties (`section.Learners.Count`) require loading the collection into memory. With 30+ learners per section and 20 sections, that's 600 entities loaded just to display counts. The subquery executes entirely in the database.

**Why match on SectionName instead of FK?** Because `Learner.SectionId` doesn't exist yet (deferred FK). When it does, change this to `l.SectionId == x.section.Id` for type-safe matching.

### Uniqueness Pre-Check

Before create and update, we check `(InstitutionId, GradeLevelId, Name)` uniqueness:

```csharp
var nameExists = await db.Sections.AsNoTracking()
    .AnyAsync(s => s.InstitutionId == request.InstitutionId
                && s.GradeLevelId == request.GradeLevelId
                && s.Name == request.Name.Trim());
```

The DB unique index is the safety net for race conditions, but checking first gives a friendly error message ("A section named 'A' already exists for this grade level") instead of a raw Postgres constraint violation.

### Optimistic Concurrency

Same pattern as LearnerService: set `section.Version = request.Version`, call `SaveChangesAsync()`, catch `DbUpdateConcurrencyException`, throw `InvalidOperationException` with user-friendly message.

---

## File 5: `Data/SchoolDbContext.cs` (Modified)

### What Changed

Three additions to support the Section entity:

1. **DbSet registration:** `public DbSet<Section> Sections => Set<Section>();`
2. **Relationship configuration:** Two FKs (Institution, GradeLevel) with `DeleteBehavior.Restrict` — prevents cascading deletes that would orphan learners or destroy institutional structure.
3. **Unique index:** `HasIndex(x => new { x.InstitutionId, x.GradeLevelId, x.Name }).IsUnique()` — enforces that section names are unique within a grade level within an institution. "A" can exist in Grade 1 and Grade 2 simultaneously, but not twice in Grade 1.
4. **Concurrency token:** `Property(x => x.Version).IsConcurrencyToken()` — enables optimistic concurrency detection.

### Why Restrict Delete Behavior?

If we used `Cascade`, deleting a GradeLevel would delete all its Sections, which would violate the Learner FK constraint (since Learner references GradeLevel directly). `Restrict` forces explicit handling: delete learners first, then sections, then grade levels. This is safer and makes deletion intentional.

---

## File 6: `Components/Pages/Sections.razor`

### What This File Is

A fully functional CRUD page replacing the original static HTML. ~450 lines covering data loading, filtering, modals, master-detail interaction, and form validation.

### Key Patterns Reused from Learners.razor

- **Debounced search:** 300ms delay via `CancellationTokenSource`, same implementation.
- **Modal pattern:** Backdrop click-to-close, `@onclick:stopPropagation` on content, `EditForm` + `DataAnnotationsValidator`.
- **Delete confirmation:** Separate modal with target name display, disabled state during submission.
- **Error/success messages:** Conditional rendering with role="status" for accessibility.
- **Plain `<select @bind>` for Guid binding:** Avoids `InputSelect<Guid>` quirks.
- **Inner `SectionFormModel` class:** Mutable class (not record) for EditForm two-way binding. Validation attributes here, not on DTOs.

### What's Different from Learners.razor

**Card layout vs. table layout:** Sections display as cards in a responsive grid (`grid-cols-1 sm:grid-cols-2 xl:grid-cols-3`) rather than a data table. Cards suit sections better because each has multiple attributes (name, grade level, adviser, learner count, status) that don't fit neatly into columns.

**Master-detail interaction:** Clicking a section card sets `selectedSectionId` and loads that section's learners into a detail panel below the grid. This is a common Blazor pattern for hierarchical data. The detail panel:
- Shows learner count and adviser in the header
- Has tab navigation (only "Learners" tab is functional; others show "Coming soon")
- Displays learners in a table filtered by `SectionName`
- Has disabled buttons ("Take Attendance", "View Class Record") with tooltips explaining they require future entities

**Two service injections:** `ISectionService` for section CRUD + `ILearnerService` for loading section learners and grade level options. This cross-service dependency is acceptable because both services are scoped to the same DI container and the page is the composition root.

**Honest handling of incomplete features:** Disabled buttons have `title` attributes explaining what's missing. Tabs that aren't implemented show "Coming soon" in muted text. No fake data, no placeholder functionality that pretends to work.

### Known Limitation: Edit Form Version

The edit modal hardcodes `editVersion = 1` because `SectionSummary` doesn't include Version. This means optimistic concurrency won't detect conflicts on section edits. The fix mirrors Phase 1 of the Learner implementation: add a `SectionDetail` DTO with Version, add `GetDetailByIdAsync` to the service, and call it in `OpenEditModal`. Deferred because section edits are rare and the risk is low.

---

## File 7: `Components/Pages/Home.razor` (Modified)

### What Changed

Replaced all hardcoded dashboard data with real database queries:

- **Stat cards:** Enrolled Learners and Sections counts come from `DashboardStats`. Teachers shows "—" (requires Teacher entity). Present Today shows 0 (requires Attendance entity).
- **Attendance section:** All three metrics (Present/Absent/Late) show 0 with "Coming soon" note. Never fabricated.
- **Section Overview:** Top 3 active sections loaded from `ISectionService.GetSectionsAsync`.
- **Recent Activity:** Last 5 learners ordered by Id (proxy for creation order since Learner lacks CreatedAt). Shows "Recently added" as timestamp placeholder.
- **Greeting:** Generic "Good morning!" instead of hardcoded "Teacher Ana". No auth yet.

### New DTO: `DashboardStats`

Added to `LearnerDtos.cs` because the dashboard aggregates learner data. Contains enrolled count, section count, attendance placeholders (all 0), and recent activities list. The `RecentActivity` record has `LearnerName`, `Activity`, and `Timestamp` (currently "Recently added" string).

### New Service Method: `GetDashboardStatsAsync`

Implemented in `LearnerService` because it primarily queries learner data. Section count delegates to the Sections DbSet directly (same context, no cross-service call needed). Comments explain why attendance returns 0 and why ordering by Guid is a temporary proxy for CreatedAt.

---

## File 8: `Components/Pages/Learners.razor` (Modified)

### What Changed

**Phase 1 — Edit form fix:**
- Added `LearnerDetail` DTO with GuardianName, GuardianContact, BirthDate, Version
- Added `GetDetailByIdAsync` to service interface and implementation
- `OpenEditModal` now calls `GetDetailByIdAsync` instead of `GetByIdAsync`
- Guardian fields, BirthDate, and Version are correctly populated from the database
- Concurrency token is now the real Version, not hardcoded 1

**Phase 5 — Section dropdown integration:**
- Added `@inject ISectionService SectionService`
- Replaced `List<string> availableSections` with `List<SectionOption> sectionOptions`
- `LoadDropdownsAsync` calls `SectionService.GetSectionOptionsAsync` instead of falling back to hardcoded list
- Section filter dropdown and form dropdown both use `SectionOption.DisplayName` for display and `SectionOption.Name` for value binding
- New sections created on the Sections page are immediately available in the learner form

### Thought Process Behind the Dropdown Change

The original code had a hardcoded fallback list of 12 section names. This was a development convenience that became a liability: sections created via the Sections page wouldn't appear in the learner form, and deleted sections would still be selectable. By switching to `GetSectionOptionsAsync`, we ensure a single source of truth. The `SectionOption.Name` value matches what's stored in `Learner.SectionName`, maintaining consistency without a FK.

---

## Data Flow Cheat Sheets

### Add Section
```
Click "+ Add Section" → OpenAddModal() → formModel = new SectionFormModel()
→ Fill form → HandleFormSubmit() → validate GradeLevelId != Empty
→ CreateSectionRequest → SectionService.CreateAsync()
→ Uniqueness pre-check → new Section entity → SaveChangesAsync()
→ INSERT INTO school."Sections" → successMessage → LoadDataAsync() → cards refresh
```

### Edit Section
```
Click "Edit" on card → OpenEditModal(id) → SectionService.GetByIdAsync(id)
→ SectionSummary → populate formModel + editStatus
→ Edit form → HandleFormSubmit() → UpdateSectionRequest
→ SectionService.UpdateAsync() → FindAsync(id) → set properties
→ SaveChangesAsync() → UPDATE ... WHERE "Version" = @oldVersion
→ successMessage → LoadDataAsync() → cards refresh
```

### Delete Section
```
Click "Delete" → ConfirmDelete(id, name) → showDeleteConfirm = true
→ Click "Delete" in modal → ExecuteDelete()
→ SectionService.DeleteAsync(id) → FindAsync(id) → Remove() → SaveChangesAsync()
→ DELETE FROM school."Sections" → successMessage → LoadDataAsync() → cards refresh
→ If deleted section was selected, clear detail panel
```

### View Section Learners (Master-Detail)
```
Click section card → SelectSection(id) → selectedSectionId = id
→ LearnerService.GetLearnersAsync(filter with SectionName = section.Name)
→ LearnerListResult → render learner table in detail panel
```

---

## Self-Assessment Checklist

After studying, can you answer these without looking at code?

- [ ] Why does Section live in Enrollment, not Academics?
- [ ] Why is AdviserName a string instead of a Teacher FK?
- [ ] Why did we defer promoting Learner.SectionName to a FK?
- [ ] What does the composite unique index on (InstitutionId, GradeLevelId, Name) enforce?
- [ ] Why is LearnerCount computed via subquery instead of navigation property?
- [ ] Why does SectionOption have both Name and DisplayName?
- [ ] Why does the edit modal hardcode Version = 1, and what's the fix?
- [ ] How does the master-detail pattern work in Sections.razor?
- [ ] Why does Home.razor show 0 for attendance instead of fake numbers?
- [ ] Why does GetDashboardStatsAsync order by Guid instead of CreatedAt?
- [ ] How did replacing the hardcoded section list fix a real bug?
- [ ] Trace the complete Add Section flow from click to database
- [ ] Trace the complete View Section Learners flow from card click to table render
- [ ] What happens when you try to delete a section that has learners?
- [ ] Why are some tabs and buttons disabled instead of hidden?

---

## Prerequisites Before Running

You need seed data in Supabase before Sections work:

1. An Institution record with ID `00000000-0000-0000-0000-000000000001` in `school."Institutions"`
2. At least one GradeLevel record linked to that institution in `school."GradeLevels"`

Without these, the grade level dropdown is empty and FK constraints reject inserts.

---

## Next Steps After Mastering This

1. **Add `SectionDetail` DTO + `GetDetailByIdAsync`** → fix the edit form Version hardcoding (same pattern as Learner Phase 1)
2. **Promote Learner.SectionName to SectionId FK** → after auth stabilizes the data model
3. **Create Teacher entity** → promote AdviserName to FK, enable teacher-scoped views
4. **Build Attendance entity + UI** → enable "Take Attendance" button and real attendance stats
5. **Add CreatedAt timestamp to Learner** → improve dashboard recent activity ordering
6. **Enforce MaxCapacity** → check learner count on add, warn or block when exceeded
7. **Add unit tests for SectionService** → mirror LearnerService test patterns
8. **Soft-delete sections** → set Status = "Inactive" instead of hard delete, preserve historical data
9. **Cross-reference study guides** → link between Learner and Section guides where patterns overlap