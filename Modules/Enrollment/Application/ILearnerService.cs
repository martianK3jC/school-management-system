namespace SchoolManagementSystem.Modules.Enrollment.Application;

public interface ILearnerService
{
	Task<LearnerListResult> GetLearnersAsync(LearnerFilter filter);
	Task<LearnerSummary?> GetByIdAsync(Guid id);
	Task<LearnerDetail?> GetDetailByIdAsync(Guid id);
	Task<Guid> CreateAsync(CreateLearnerRequest request);
	Task UpdateAsync(UpdateLearnerRequest request);
	Task DeleteAsync(Guid id);
	Task<List<string>> GetDistinctSectionsAsync(Guid institutionId);
	Task<List<GradeLevelOption>> GetGradeLevelOptionsAsync(Guid institutionId);
	Task<DashboardStats> GetDashboardStatsAsync(Guid institutionId);
}