# Section CRUD study guide

Read the [start-to-finish learner and project walkthrough](LEARNER_CRUD_STUDY_GUIDE.md) first. That guide now covers the shared architecture, migration rollout, validation, concurrency, dialogs, and verification. This page records the section-specific implementation and replaces the earlier guide's outdated assumptions.

## Files to trace

1. `Modules/Enrollment/Domain/Section.cs`: the stored section record.
2. `Modules/Enrollment/Application/SectionDtos.cs`: filters, summaries, full edit detail, create/update requests, and dropdown options.
3. `Modules/Enrollment/Application/ISectionService.cs`: the page's service contract.
4. `Modules/Enrollment/Application/SectionService.cs`: filtered reads, counts, validation, and writes.
5. `Data/SchoolDbContext.cs`: relationships, checks, uniqueness, and concurrency tokens.
6. `Components/Pages/Sections.razor`: cards, filters, paged learner details, and dialogs.
7. `Migrations/*_HardenEnrollmentRecords.cs`: the transition from name-only membership to a section foreign key.

## Membership now uses an ID

`Learner.SectionId` is nullable and references `Section.Id`. Section learner counts and the detail panel use this ID. Learners in Grade 1 “Rizal” and Grade 3 “Rizal” no longer share a count just because the names match.

`Learner.SectionName` remains legacy text/a name snapshot. Displays prefer the linked section's current name. Renaming a section therefore preserves assignments. The migration links exact institution + grade + name matches and retains unmatched names for explicit correction. See the read-only `Data/EnrollmentDataAudit.sql` report and migration instructions in the main guide.

The foreign key restricts deletion of assigned sections. Unlike the old implementation, this restriction actually exists between learners and sections. The service also checks membership and returns a friendly business error. Mark a section inactive or reassign learners before deleting it.

## Editing preserves the complete record

The page calls `GetDetailByIdAsync` and receives `SectionDetail`, including `GradeLevelId`, `MaxCapacity`, and `Version`. It does not infer grade IDs from display names, assume version 1, or reset omitted capacity to zero.

Updates compare the submitted version with the loaded row, increment it, and save using EF's original concurrency value. If a stale form or a save-time race is detected, the service rejects the update. Version is a manually advanced `long`, not PostgreSQL xmin.

Changing the grade of a section with learners is blocked until they are reassigned. Renaming the section is allowed because the assignment key stays the same.

## Validation and limitations

The service checks school/grade ownership, trimmed name uniqueness, allowed status, string lengths, and nonnegative capacity. Database checks and indexes provide a second boundary. A uniqueness pre-check is helpful feedback but cannot replace the unique index during simultaneous writes.

AdviserName is still text; there is no Teacher entity. Capacity is stored and validated but is not yet an enrollment limit. Inactive sections cannot receive new assignments; an existing learner may keep an inactive section while other fields are edited.

Section counts currently include all assigned learner statuses. School-wide enrolled statistics are separate. Filters apply to the section list, while ActiveCount describes the institution's active sections. The learner detail panel is paginated and identifies the matching total separately from displayed rows.

## Learning checkpoints

- Trace card click → section ID → LearnerFilter → ID predicate → paged learner rows.
- Explain why matching a section by name alone was wrong across grades.
- Explain why a detail DTO includes fields that a display summary does not need.
- Explain why restricted deletion is enforced by both the service and the database.
- Try editing capacity, then editing only the room. Verify capacity stays intact.
- Open two edit forms and explain how the stale version should be rejected.
- Rename an assigned section and explain why learner membership remains intact.

Attendance, Class Record, and Adviser Information panels are future work. Their disabled controls do not represent working features.
