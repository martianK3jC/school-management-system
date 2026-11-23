using Microsoft.EntityFrameworkCore;
using SchoolManagementSystem.Data;
using SchoolManagementSystem.Modules.Enrollment.Domain;

namespace SchoolManagementSystem.Modules.Enrollment.Application;

/// <summary>
/// EF Core implementation of ISectionService. Follows the exact same patterns
/// as LearnerService: context-per-operation, AsNoTracking on reads, IQueryable
/// composition for filtering, optimistic concurrency on writes, and uniqueness
/// pre-checks before insert/update.
/// </summary>
public class SectionService(IDbContextFactory<SchoolDbContext> dbFactory) : ISectionService
{
    public async Task<SectionListResult> GetSectionsAsync(SectionFilter filter)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var query = db.Sections
            .AsNoTracking()
            .Where(s => s.InstitutionId == filter.InstitutionId);

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var search = filter.SearchText.Trim();
            query = query.Where(s =>
                s.Name.Contains(search) ||
                s.AdviserName.Contains(search));
        }

        if (filter.GradeLevelId is not null)
            query = query.Where(s => s.GradeLevelId == filter.GradeLevelId);

        if (!string.IsNullOrWhiteSpace(filter.AdviserName))
            query = query.Where(s => s.AdviserName == filter.AdviserName);

        if (!string.IsNullOrWhiteSpace(filter.Status))
            query = query.Where(s => s.Status == filter.Status);

        // Join with GradeLevels for display name, and compute LearnerCount
        // via a correlated subquery against the Learners table. This avoids
        // navigation properties and keeps SQL generation transparent.
        var items = await query
            .OrderBy(s => s.Name)
            .Join(db.GradeLevels.AsNoTracking(),
                section => section.GradeLevelId,
                gradeLevel => gradeLevel.Id,
                (section, gradeLevel) => new { section, gradeLevel })
            .Select(x => new SectionSummary(
                Id: x.section.Id,
                Name: x.section.Name,
                GradeLevelName: x.gradeLevel.Name,
                AdviserName: x.section.AdviserName,
                RoomNumber: x.section.RoomNumber,
                LearnerCount: db.Learners.AsNoTracking()
                    .Count(l => l.InstitutionId == x.section.InstitutionId
                        && l.SectionId == x.section.Id),
                Status: x.section.Status,
                GradeLevelId: x.section.GradeLevelId))
            .ToListAsync();

        var activeCount = await db.Sections
            .AsNoTracking()
            .CountAsync(s => s.InstitutionId == filter.InstitutionId && s.Status == "Active");

        return new SectionListResult(
            Items: items,
            TotalCount: items.Count,
            ActiveCount: activeCount);
    }

    public async Task<SectionSummary?> GetByIdAsync(Guid id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        return await db.Sections
            .AsNoTracking()
            .Join(db.GradeLevels.AsNoTracking(),
                section => section.GradeLevelId,
                gradeLevel => gradeLevel.Id,
                (section, gradeLevel) => new { section, gradeLevel })
            .Where(x => x.section.Id == id)
            .Select(x => new SectionSummary(
                Id: x.section.Id,
                Name: x.section.Name,
                GradeLevelName: x.gradeLevel.Name,
                AdviserName: x.section.AdviserName,
                RoomNumber: x.section.RoomNumber,
                LearnerCount: db.Learners.AsNoTracking()
                    .Count(l => l.InstitutionId == x.section.InstitutionId
                        && l.SectionId == x.section.Id),
                Status: x.section.Status,
                GradeLevelId: x.section.GradeLevelId))
            .FirstOrDefaultAsync();
    }

    public async Task<SectionDetail?> GetDetailByIdAsync(Guid id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Sections.AsNoTracking().Where(s => s.Id == id)
            .Select(s => new SectionDetail(s.Id, s.GradeLevelId, s.Name, s.AdviserName,
                s.RoomNumber, s.MaxCapacity, s.Status, s.Version)).FirstOrDefaultAsync();
    }

    private static void Validate(string name, string adviser, string room, int capacity, string status)
    {
        EnrollmentValidation.Text(name, "Section name", 100, true);
        EnrollmentValidation.Text(adviser, "Adviser name", 200);
        EnrollmentValidation.Text(room, "Room number", 100);
        if (capacity < 0) throw new EnrollmentException("Capacity cannot be negative.");
        if (status is not ("Active" or "Inactive")) throw new EnrollmentException("Select a valid section status.");
    }

    public async Task<Guid> CreateAsync(CreateSectionRequest request)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        Validate(request.Name, request.AdviserName, request.RoomNumber, request.MaxCapacity, "Active");
        await EnrollmentValidation.GradeAsync(db, request.InstitutionId, request.GradeLevelId);
        // Pre-check uniqueness: section name must be unique within
        // (InstitutionId, GradeLevelId). The DB unique index is the safety net
        // for race conditions, but checking first gives a friendly error message.
        var nameExists = await db.Sections
            .AsNoTracking()
            .AnyAsync(s => s.InstitutionId == request.InstitutionId
                && s.GradeLevelId == request.GradeLevelId
                && s.Name == request.Name.Trim());

        if (nameExists)
            throw new EnrollmentException(
                $"A section named '{request.Name}' already exists for this grade level.");

        var section = new Section
        {
            Id = Guid.NewGuid(),
            InstitutionId = request.InstitutionId,
            GradeLevelId = request.GradeLevelId,
            Name = request.Name.Trim(),
            AdviserName = request.AdviserName.Trim(),
            RoomNumber = request.RoomNumber.Trim(),
            MaxCapacity = request.MaxCapacity,
            Status = "Active",
            Version = 1
        };

        db.Sections.Add(section);
        await EnrollmentValidation.SaveAsync(db);

        return section.Id;
    }

    public async Task UpdateAsync(UpdateSectionRequest request)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var section = await db.Sections.FindAsync(request.Id)
            ?? throw new EnrollmentException("Section not found.");

        Validate(request.Name, request.AdviserName, request.RoomNumber, request.MaxCapacity, request.Status);
        if (section.Version != request.Version) throw new EnrollmentException(EnrollmentValidation.ConflictMessage);
        await EnrollmentValidation.GradeAsync(db, section.InstitutionId, request.GradeLevelId);
        if (section.GradeLevelId != request.GradeLevelId && await db.Learners.AnyAsync(l => l.SectionId == section.Id))
            throw new EnrollmentException("Reassign this section's learners before changing its grade level.");
        // Check name uniqueness excluding the current record.
        var nameConflict = await db.Sections
            .AsNoTracking()
            .AnyAsync(s => s.InstitutionId == section.InstitutionId
                && s.GradeLevelId == request.GradeLevelId
                && s.Name == request.Name.Trim()
                && s.Id != request.Id);

        if (nameConflict)
            throw new EnrollmentException(
                $"Another section named '{request.Name}' already exists for this grade level.");

        section.GradeLevelId = request.GradeLevelId;
        section.Name = request.Name.Trim();
        section.AdviserName = request.AdviserName.Trim();
        section.RoomNumber = request.RoomNumber.Trim();
        section.MaxCapacity = request.MaxCapacity;
        section.Status = request.Status;
        section.Version++;

        await EnrollmentValidation.SaveAsync(db);
    }

    public async Task DeleteAsync(Guid id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var section = await db.Sections.FindAsync(id)
            ?? throw new EnrollmentException("Section not found.");

        if (await db.Learners.AnyAsync(l => l.SectionId == id))
            throw new EnrollmentException("Reassign this section's learners before deleting it, or mark it inactive.");
        db.Sections.Remove(section);
        await EnrollmentValidation.SaveAsync(db);
    }

    public async Task<List<SectionOption>> GetSectionOptionsAsync(Guid institutionId, Guid? gradeLevelId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var query = db.Sections
            .AsNoTracking()
            .Where(s => s.InstitutionId == institutionId && s.Status == "Active");

        if (gradeLevelId is not null)
            query = query.Where(s => s.GradeLevelId == gradeLevelId);

        return await query
            .OrderBy(s => s.Name)
            .Join(db.GradeLevels.AsNoTracking(),
                section => section.GradeLevelId,
                gradeLevel => gradeLevel.Id,
                (section, gradeLevel) => new SectionOption(
                    Id: section.Id,
                    Name: section.Name,
                    DisplayName: $"{gradeLevel.Name} - {section.Name}",
                    GradeLevelId: section.GradeLevelId))
            .ToListAsync();
    }

    public async Task<List<string>> GetAdviserNamesAsync(Guid institutionId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        return await db.Sections
            .AsNoTracking()
            .Where(s => s.InstitutionId == institutionId
                && s.Status == "Active"
                && s.AdviserName != "")
            .Select(s => s.AdviserName)
            .Distinct()
            .OrderBy(a => a)
            .ToListAsync();
    }
}