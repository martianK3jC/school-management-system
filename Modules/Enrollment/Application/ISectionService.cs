namespace SchoolManagementSystem.Modules.Enrollment.Application;

/// <summary>
/// Contract for section operations. The Razor page depends on this interface,
/// not the EF Core implementation, enabling testability and future swapping
/// (e.g., REST API client) without touching UI code.
/// </summary>
public interface ISectionService
{
    Task<SectionListResult> GetSectionsAsync(SectionFilter filter);
    Task<SectionSummary?> GetByIdAsync(Guid id);
    Task<SectionDetail?> GetDetailByIdAsync(Guid id);
    Task<Guid> CreateAsync(CreateSectionRequest request);
    Task UpdateAsync(UpdateSectionRequest request);
    Task DeleteAsync(Guid id);

    /// <summary>
    /// Returns sections formatted as dropdown options. Optional gradeLevelId
    /// filters to a specific grade level (useful when the learner form's
    /// grade level selection changes).
    /// </summary>
    Task<List<SectionOption>> GetSectionOptionsAsync(Guid institutionId, Guid? gradeLevelId);

    /// <summary>Distinct adviser names for the filter dropdown.</summary>
    Task<List<string>> GetAdviserNamesAsync(Guid institutionId);
}