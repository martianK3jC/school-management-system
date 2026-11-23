namespace SchoolManagementSystem.Modules.Enrollment.Application;

public record LearnerFilter(
    Guid InstitutionId,
    string SearchText,
    Guid? GradeLevelId,
    string? SectionName,
    string? Status);

public record LearnerListResult(
    List<LearnerSummary> Items,
    int TotalCount,
    int EnrolledCount,
    int EnrolledMaleCount,
    int EnrolledFemaleCount,
    int DistinctGradeLevelCount,
    int DistinctSectionCount);

public record LearnerSummary(
    Guid Id,
    string FullName,
    string Lrn,
    string Sex,
    string GradeLevelName,
    string SectionName,
    string Status);

public record CreateLearnerRequest(
    Guid InstitutionId,
    Guid GradeLevelId,
    string Lrn,
    string FirstName,
    string MiddleName,
    string LastName,
    string Sex,
    DateOnly BirthDate,
    string SectionName,
    string GuardianName,
    string GuardianContact);

public record UpdateLearnerRequest(
    Guid Id,
    Guid GradeLevelId,
    string Lrn,
    string FirstName,
    string MiddleName,
    string LastName,
    string Sex,
    DateOnly BirthDate,
    string SectionName,
    string Status,
    string GuardianName,
    string GuardianContact,
    long Version);

/// <summary>
/// Full learner detail for the edit form. Includes fields that LearnerSummary
/// omits (guardian info, birth date, version) so the edit modal can pre-populate
/// every field and send the correct concurrency token back on save.
/// </summary>
public record LearnerDetail(
    Guid Id,
    string FullName,
    string Lrn,
    string Sex,
    DateOnly BirthDate,
    string GradeLevelName,
    Guid GradeLevelId,
    string SectionName,
    string Status,
    string GuardianName,
    string GuardianContact,
    long Version);

/// <summary>
/// Aggregate statistics for the dashboard. Metrics that require unimplemented
/// entities (Teachers, Attendance) return 0 rather than fabricated data.
/// </summary>
public record DashboardStats(
    int EnrolledLearners,
    int TotalSections,
    int PresentToday,
    int AbsentToday,
    int LateToday,
    List<RecentActivity> RecentActivities);

/// <summary>
/// A single row in the dashboard's recent activity feed. Currently sourced from
/// learner creation events; will expand when an audit log entity is added.
/// </summary>
public record RecentActivity(string LearnerName, string Activity, string Timestamp);

public record GradeLevelOption(Guid Id, string Name);