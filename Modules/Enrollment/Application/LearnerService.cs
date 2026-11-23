using Microsoft.EntityFrameworkCore;
using SchoolManagementSystem.Data;
using SchoolManagementSystem.Modules.Enrollment.Domain;

namespace SchoolManagementSystem.Modules.Enrollment.Application;

public class LearnerService(IDbContextFactory<SchoolDbContext> dbFactory) : ILearnerService
{
 public async Task<LearnerListResult> GetLearnersAsync(LearnerFilter filter)
 {
 await using var db = await dbFactory.CreateDbContextAsync();

 var query = db.Learners
 .AsNoTracking()
 .Where(l => l.InstitutionId == filter.InstitutionId);

 if (!string.IsNullOrWhiteSpace(filter.SearchText))
 {
 var search = filter.SearchText.Trim();
 query = query.Where(l =>
 l.FirstName.Contains(search) ||
 l.LastName.Contains(search) ||
 l.Lrn.Contains(search) ||
 l.SectionName.Contains(search));
 }

 if (filter.GradeLevelId is not null)
 query = query.Where(l => l.GradeLevelId == filter.GradeLevelId);

 if (!string.IsNullOrWhiteSpace(filter.SectionName))
 query = query.Where(l => l.SectionName == filter.SectionName);

 if (!string.IsNullOrWhiteSpace(filter.Status))
 query = query.Where(l => l.Status == filter.Status);

 var items = await query
 .OrderBy(l => l.LastName)
 .ThenBy(l => l.FirstName)
 .Join(db.GradeLevels.AsNoTracking(),
 learner => learner.GradeLevelId,
 gradeLevel => gradeLevel.Id,
 (learner, gradeLevel) => new LearnerSummary(
 Id: learner.Id,
 FullName: $"{learner.LastName}, {learner.FirstName} {learner.MiddleName}".Trim(),
 Lrn: learner.Lrn,
 Sex: learner.Sex,
 GradeLevelName: gradeLevel.Name,
 SectionName: learner.SectionName,
 Status: learner.Status))
 .ToListAsync();

 var institutionLearners = db.Learners
 .AsNoTracking()
 .Where(l => l.InstitutionId == filter.InstitutionId);

 var enrolledCount = await institutionLearners
 .CountAsync(l => l.Status == "Enrolled");

 var enrolledMaleCount = await institutionLearners
 .CountAsync(l => l.Status == "Enrolled" && l.Sex == "Male");

 var enrolledFemaleCount = await institutionLearners
 .CountAsync(l => l.Status == "Enrolled" && l.Sex == "Female");

 var distinctGradeLevelCount = await institutionLearners
 .Select(l => l.GradeLevelId)
 .Distinct()
 .CountAsync();

 var distinctSectionCount = await institutionLearners
 .Where(l => l.Status == "Enrolled")
 .Select(l => l.SectionName)
 .Distinct()
 .CountAsync();

 return new LearnerListResult(
 Items: items,
 TotalCount: items.Count,
 EnrolledCount: enrolledCount,
 EnrolledMaleCount: enrolledMaleCount,
 EnrolledFemaleCount: enrolledFemaleCount,
 DistinctGradeLevelCount: distinctGradeLevelCount,
 DistinctSectionCount: distinctSectionCount);
 }

 public async Task<LearnerSummary?> GetByIdAsync(Guid id)
 {
 await using var db = await dbFactory.CreateDbContextAsync();

 return await db.Learners
 .AsNoTracking()
 .Join(db.GradeLevels.AsNoTracking(),
 learner => learner.GradeLevelId,
 gradeLevel => gradeLevel.Id,
 (learner, gradeLevel) => new LearnerSummary(
 Id: learner.Id,
 FullName: $"{learner.LastName}, {learner.FirstName} {learner.MiddleName}".Trim(),
 Lrn: learner.Lrn,
 Sex: learner.Sex,
 GradeLevelName: gradeLevel.Name,
 SectionName: learner.SectionName,
 Status: learner.Status))
 .FirstOrDefaultAsync(x => x.Id == id);
 }

 public async Task<LearnerDetail?> GetDetailByIdAsync(Guid id)
	{
		await using var db = await dbFactory.CreateDbContextAsync();

		return await db.Learners
			.AsNoTracking()
			.Join(db.GradeLevels.AsNoTracking(),
				learner => learner.GradeLevelId,
				gradeLevel => gradeLevel.Id,
				(learner, gradeLevel) => new LearnerDetail(
					Id: learner.Id,
					FullName: $"{learner.LastName}, {learner.FirstName} {learner.MiddleName}".Trim(),
					Lrn: learner.Lrn,
					Sex: learner.Sex,
					BirthDate: learner.BirthDate,
					GradeLevelName: gradeLevel.Name,
					GradeLevelId: learner.GradeLevelId,
					SectionName: learner.SectionName,
					Status: learner.Status,
					GuardianName: learner.GuardianName,
					GuardianContact: learner.GuardianContact,
					Version: learner.Version))
			.FirstOrDefaultAsync(x => x.Id == id);
	}

	public async Task<Guid> CreateAsync(CreateLearnerRequest request)
 {
 await using var db = await dbFactory.CreateDbContextAsync();

 var lrnExists = await db.Learners
 .AsNoTracking()
 .AnyAsync(l => l.InstitutionId == request.InstitutionId && l.Lrn == request.Lrn);

 if (lrnExists)
 throw new InvalidOperationException(
 $"A learner with LRN '{request.Lrn}' already exists in this institution.");

 var learner = new Learner
 {
 Id = Guid.NewGuid(),
 InstitutionId = request.InstitutionId,
 GradeLevelId = request.GradeLevelId,
 Lrn = request.Lrn.Trim(),
 FirstName = request.FirstName.Trim(),
 MiddleName = request.MiddleName.Trim(),
 LastName = request.LastName.Trim(),
 Sex = request.Sex,
 BirthDate = request.BirthDate,
 SectionName = request.SectionName.Trim(),
 Status = "Enrolled",
 GuardianName = request.GuardianName.Trim(),
 GuardianContact = request.GuardianContact.Trim(),
 Version = 1
 };

 db.Learners.Add(learner);
 await db.SaveChangesAsync();

 return learner.Id;
 }

 public async Task UpdateAsync(UpdateLearnerRequest request)
 {
 await using var db = await dbFactory.CreateDbContextAsync();

 var learner = await db.Learners.FindAsync(request.Id)
 ?? throw new InvalidOperationException("Learner not found.");

 var lrnConflict = await db.Learners
 .AsNoTracking()
 .AnyAsync(l => l.InstitutionId == learner.InstitutionId
 && l.Lrn == request.Lrn
 && l.Id != request.Id);

 if (lrnConflict)
 throw new InvalidOperationException(
 $"Another learner with LRN '{request.Lrn}' already exists in this institution.");

 learner.GradeLevelId = request.GradeLevelId;
 learner.Lrn = request.Lrn.Trim();
 learner.FirstName = request.FirstName.Trim();
 learner.MiddleName = request.MiddleName.Trim();
 learner.LastName = request.LastName.Trim();
 learner.Sex = request.Sex;
 learner.BirthDate = request.BirthDate;
 learner.SectionName = request.SectionName.Trim();
 learner.Status = request.Status;
 learner.GuardianName = request.GuardianName.Trim();
 learner.GuardianContact = request.GuardianContact.Trim();
 learner.Version = request.Version;

 try
 {
 await db.SaveChangesAsync();
 }
 catch (DbUpdateConcurrencyException)
 {
 throw new InvalidOperationException(
 "This record was modified by another user. Please refresh and try again.");
 }
 }

 public async Task DeleteAsync(Guid id)
 {
 await using var db = await dbFactory.CreateDbContextAsync();

 var learner = await db.Learners.FindAsync(id)
 ?? throw new InvalidOperationException("Learner not found.");

 db.Learners.Remove(learner);
 await db.SaveChangesAsync();
 }

 public async Task<List<string>> GetDistinctSectionsAsync(Guid institutionId)
 {
 await using var db = await dbFactory.CreateDbContextAsync();

 return await db.Learners
 .AsNoTracking()
 .Where(l => l.InstitutionId == institutionId)
 .Select(l => l.SectionName)
 .Distinct()
 .OrderBy(s => s)
 .ToListAsync();
 }

 public async Task<List<GradeLevelOption>> GetGradeLevelOptionsAsync(Guid institutionId)
 {
 await using var db = await dbFactory.CreateDbContextAsync();

 return await db.GradeLevels
 .AsNoTracking()
 .Where(g => g.InstitutionId == institutionId)
 .OrderBy(g => g.Identifier)
 .Select(g => new GradeLevelOption(g.Id, g.Name))
 .ToListAsync();
 }

 public async Task<DashboardStats> GetDashboardStatsAsync(Guid institutionId)
 {
 await using var db = await dbFactory.CreateDbContextAsync();

 var enrolledLearners = await db.Learners
 .AsNoTracking()
 .CountAsync(l => l.InstitutionId == institutionId && l.Status == "Enrolled");

 var totalSections = await db.Sections
 .AsNoTracking()
 .CountAsync(s => s.InstitutionId == institutionId);

 // Attendance tracking is not yet implemented. Return 0 rather than
 // fabricated numbers — honest gaps teach better than fake data.
 var presentToday = 0;
 var absentToday = 0;
 var lateToday = 0;

 // Recent activity: show the 5 most recently added learners.
 // Learner has no CreatedAt timestamp yet, so we order by Id (Guid v4
 // is roughly time-ordered on most platforms). When a CreatedAt column
 // is added, switch to ordering by that instead.
 var recentLearners = await db.Learners
 .AsNoTracking()
 .Where(l => l.InstitutionId == institutionId)
 .OrderByDescending(l => l.Id)
 .Take(5)
 .Select(l => new RecentActivity(
 LearnerName: $"{l.LastName}, {l.FirstName}".Trim(),
 Activity: $"Enrolled in {l.SectionName}",
 Timestamp: "Recently added"))
 .ToListAsync();

 return new DashboardStats(
 EnrolledLearners: enrolledLearners,
 TotalSections: totalSections,
 PresentToday: presentToday,
 AbsentToday: absentToday,
 LateToday: lateToday,
 RecentActivities: recentLearners);
 }
}
