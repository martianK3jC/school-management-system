namespace SchoolManagementSystem.Modules.Enrollment.Application;

/// <summary>
/// Input filter for listing sections. All fields except InstitutionId are optional.
/// Mirrors LearnerFilter's design: required scope + optional refinements.
/// </summary>
public record SectionFilter(
    Guid InstitutionId,
    string SearchText,
    Guid? GradeLevelId,
    string? AdviserName,
    string? Status);

/// <summary>
/// Output of a section list query. Includes aggregate counts for stat cards,
/// computed server-side to avoid loading all records into memory.
/// </summary>
public record SectionListResult(
    List<SectionSummary> Items,
    int TotalCount,
    int ActiveCount);

/// <summary>
/// Lightweight row for the section card/list display. LearnerCount is computed
/// via a subquery against the Learners table (matching by SectionId)
/// rather than a navigation property, keeping the query explicit and predictable.
/// </summary>
public record SectionSummary(
    Guid Id,
    string Name,
    string GradeLevelName,
    string AdviserName,
    string RoomNumber,
    int LearnerCount,
    string Status,
    Guid GradeLevelId);

/// <summary>
/// Input for creating a new section. No Id (generated server-side), no Version
/// (starts at 1), no Status (defaults to "Active" in the service).
/// </summary>
public record CreateSectionRequest(
    Guid InstitutionId,
    Guid GradeLevelId,
    string Name,
    string AdviserName,
    string RoomNumber,
    int MaxCapacity);

/// <summary>
/// Input for updating an existing section. Includes Id + Version for optimistic
/// concurrency, and Status (can be changed to "Inactive").
/// </summary>
public record UpdateSectionRequest(
    Guid Id,
    Guid GradeLevelId,
    string Name,
    string AdviserName,
    string RoomNumber,
    int MaxCapacity,
    string Status,
    long Version);

/// <summary>
/// Dropdown option for section selection. DisplayName combines grade level and
/// section name (e.g., "Grade 1 - A") so users can distinguish sections across
/// grade levels in a flat dropdown.
/// </summary>
public record SectionOption(Guid Id, string Name, string DisplayName, Guid GradeLevelId);
public record SectionDetail(Guid Id, Guid GradeLevelId, string Name, string AdviserName,
    string RoomNumber, int MaxCapacity, string Status, long Version);
