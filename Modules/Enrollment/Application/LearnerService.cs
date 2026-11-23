using Microsoft.EntityFrameworkCore;
using SchoolManagementSystem.Data;
using SchoolManagementSystem.Modules.Enrollment.Domain;

namespace SchoolManagementSystem.Modules.Enrollment.Application;

public class LearnerService(IDbContextFactory<SchoolDbContext> dbFactory) : ILearnerService
{
    public async Task<LearnerListResult> GetLearnersAsync(LearnerFilter filter)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var all = db.Learners.AsNoTracking().Where(l => l.InstitutionId == filter.InstitutionId);
        var query = all;
        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var search = filter.SearchText.Trim();
            query = query.Where(l => l.FirstName.Contains(search) || l.LastName.Contains(search)
                || l.Lrn.Contains(search) || (l.Section != null ? l.Section.Name : l.SectionName).Contains(search));
        }
        if (filter.GradeLevelId is not null) query = query.Where(l => l.GradeLevelId == filter.GradeLevelId);
        if (filter.SectionId is not null) query = query.Where(l => l.SectionId == filter.SectionId);
        else if (!string.IsNullOrWhiteSpace(filter.SectionName))
            query = query.Where(l => (l.Section != null ? l.Section.Name : l.SectionName) == filter.SectionName);
        if (!string.IsNullOrWhiteSpace(filter.Status)) query = query.Where(l => l.Status == filter.Status);

        var filteredCount = await query.CountAsync();
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);
        var page = Math.Clamp(filter.Page, 1, Math.Max(1, (int)Math.Ceiling(filteredCount / (double)pageSize)));
        var items = await Summaries(query.OrderBy(l => l.LastName).ThenBy(l => l.FirstName).ThenBy(l => l.Id)
            .Skip((page - 1) * pageSize).Take(pageSize), db).ToListAsync();
        var enrolled = all.Where(l => l.Status == "Enrolled");
        return new LearnerListResult(items, await all.CountAsync(), await enrolled.CountAsync(),
            await enrolled.CountAsync(l => l.Sex == "Male"), await enrolled.CountAsync(l => l.Sex == "Female"),
            await all.Select(l => l.GradeLevelId).Distinct().CountAsync(),
            await enrolled.Where(l => l.SectionId != null).Select(l => l.SectionId).Distinct().CountAsync(),
            page, pageSize, filteredCount);
    }

    private static IQueryable<LearnerSummary> Summaries(IQueryable<Learner> query, SchoolDbContext db) =>
        query.Join(db.GradeLevels.AsNoTracking(), l => l.GradeLevelId, g => g.Id,
            (l, g) => new LearnerSummary(l.Id, (l.LastName + ", " + l.FirstName + " " + l.MiddleName).Trim(),
                l.Lrn, l.Sex, g.Name, l.Section != null ? l.Section.Name : l.SectionName, l.Status));

    public async Task<LearnerSummary?> GetByIdAsync(Guid id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        // Filter entity columns before constructing records: EF can translate this predicate.
        return await Summaries(db.Learners.AsNoTracking().Where(l => l.Id == id), db).FirstOrDefaultAsync();
    }

    public async Task<LearnerDetail?> GetDetailByIdAsync(Guid id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Learners.AsNoTracking().Where(l => l.Id == id)
            .Join(db.GradeLevels.AsNoTracking(), l => l.GradeLevelId, g => g.Id,
                (l, g) => new LearnerDetail(l.Id, l.FirstName, l.MiddleName, l.LastName, l.Lrn, l.Sex,
                    l.BirthDate, g.Name, l.GradeLevelId, l.Section != null ? l.Section.Name : l.SectionName,
                    l.Status, l.GuardianName, l.GuardianContact, l.Version, l.SectionId))
            .FirstOrDefaultAsync();
    }

    public async Task<Guid> CreateAsync(CreateLearnerRequest request)
    {
        EnrollmentValidation.Learner(request);
        await using var db = await dbFactory.CreateDbContextAsync();
        await EnrollmentValidation.GradeAsync(db, request.InstitutionId, request.GradeLevelId);
        var sectionName = await ValidateSectionAsync(db, request.InstitutionId, request.GradeLevelId, request.SectionId);
        var lrn = request.Lrn.Trim();
        if (await db.Learners.AnyAsync(l => l.InstitutionId == request.InstitutionId && l.Lrn == lrn))
            throw new EnrollmentException("A learner with this LRN already exists in this school.");
        var learner = new Learner { Id = Guid.NewGuid(), InstitutionId = request.InstitutionId, CreatedAt = DateTimeOffset.UtcNow };
        Apply(learner, request, sectionName, "Enrolled");
        db.Learners.Add(learner);
        await EnrollmentValidation.SaveAsync(db);
        return learner.Id;
    }

    public async Task UpdateAsync(UpdateLearnerRequest request)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var learner = await db.Learners.FindAsync(request.Id) ?? throw new EnrollmentException("Learner not found.");
        var values = new CreateLearnerRequest(learner.InstitutionId, request.GradeLevelId, request.Lrn,
            request.FirstName, request.MiddleName, request.LastName, request.Sex, request.BirthDate,
            request.SectionName, request.GuardianName, request.GuardianContact, request.SectionId);
        EnrollmentValidation.Learner(values, request.Status);
        if (learner.Version != request.Version) throw new EnrollmentException(EnrollmentValidation.ConflictMessage);
        await EnrollmentValidation.GradeAsync(db, learner.InstitutionId, request.GradeLevelId);
        var sectionName = await ValidateSectionAsync(db, learner.InstitutionId, request.GradeLevelId, request.SectionId, learner.SectionId);
        var lrn = request.Lrn.Trim();
        if (await db.Learners.AnyAsync(l => l.InstitutionId == learner.InstitutionId && l.Lrn == lrn && l.Id != learner.Id))
            throw new EnrollmentException("Another learner with this LRN already exists in this school.");
        Apply(learner, values, sectionName, request.Status);
        learner.Version++;
        // EF retains the loaded original Version in the UPDATE predicate, protecting the save-time race too.
        await EnrollmentValidation.SaveAsync(db);
    }

    private static void Apply(Learner learner, CreateLearnerRequest request, string sectionName, string status)
    {
        learner.GradeLevelId = request.GradeLevelId;
        learner.Lrn = request.Lrn.Trim();
        learner.FirstName = request.FirstName.Trim();
        learner.MiddleName = request.MiddleName.Trim();
        learner.LastName = request.LastName.Trim();
        learner.Sex = request.Sex;
        learner.BirthDate = request.BirthDate;
        learner.SectionId = request.SectionId;
        learner.SectionName = sectionName;
        learner.Status = status;
        learner.GuardianName = request.GuardianName.Trim();
        learner.GuardianContact = request.GuardianContact.Trim();
        learner.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static async Task<string> ValidateSectionAsync(SchoolDbContext db, Guid institutionId, Guid gradeId, Guid? sectionId, Guid? existingSectionId = null)
    {
        if (sectionId is null) return "";
        var section = await db.Sections.AsNoTracking().SingleOrDefaultAsync(s => s.Id == sectionId
            && s.InstitutionId == institutionId && s.GradeLevelId == gradeId);
        if (section is null) throw new EnrollmentException("Select a section belonging to the selected grade and school.");
        if (section.Status != "Active" && sectionId != existingSectionId) throw new EnrollmentException("Select an active section.");
        return section.Name;
    }

    public async Task DeleteAsync(Guid id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var learner = await db.Learners.FindAsync(id) ?? throw new EnrollmentException("Learner not found.");
        db.Learners.Remove(learner);
        await EnrollmentValidation.SaveAsync(db);
    }

    public async Task<List<string>> GetDistinctSectionsAsync(Guid institutionId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Learners.AsNoTracking().Where(l => l.InstitutionId == institutionId)
            .Select(l => l.Section != null ? l.Section.Name : l.SectionName).Distinct().OrderBy(s => s).ToListAsync();
    }

    public async Task<List<GradeLevelOption>> GetGradeLevelOptionsAsync(Guid institutionId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.GradeLevels.AsNoTracking().Where(g => g.InstitutionId == institutionId)
            .OrderBy(g => g.Identifier).Select(g => new GradeLevelOption(g.Id, g.Name)).ToListAsync();
    }

    public async Task<DashboardStats> GetDashboardStatsAsync(Guid institutionId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var enrolled = await db.Learners.CountAsync(l => l.InstitutionId == institutionId && l.Status == "Enrolled");
        var sections = await db.Sections.CountAsync(s => s.InstitutionId == institutionId && s.Status == "Active");
        // Existing records have unknown creation times; never invent an enrollment date for them.
        var recent = await db.Learners.AsNoTracking().Where(l => l.InstitutionId == institutionId && l.CreatedAt != null)
            .OrderByDescending(l => l.CreatedAt).ThenBy(l => l.Id).Take(5)
            .Select(l => new { l.LastName, l.FirstName, l.CreatedAt }).ToListAsync();
        return new DashboardStats(enrolled, sections, 0, 0, 0,
            recent.Select(l => new RecentActivity($"{l.LastName}, {l.FirstName}", "Learner record created",
                l.CreatedAt!.Value.ToString("yyyy-MM-dd HH:mm 'UTC'"))).ToList());
    }
}
