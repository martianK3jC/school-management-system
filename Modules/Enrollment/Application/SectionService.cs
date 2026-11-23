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
						&& l.SectionName == x.section.Name),
				Status: x.section.Status))
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
						&& l.SectionName == x.section.Name),
				Status: x.section.Status))
			.FirstOrDefaultAsync();
	}

	public async Task<Guid> CreateAsync(CreateSectionRequest request)
	{
		await using var db = await dbFactory.CreateDbContextAsync();

		// Pre-check uniqueness: section name must be unique within
		// (InstitutionId, GradeLevelId). The DB unique index is the safety net
		// for race conditions, but checking first gives a friendly error message.
		var nameExists = await db.Sections
			.AsNoTracking()
			.AnyAsync(s => s.InstitutionId == request.InstitutionId
				&& s.GradeLevelId == request.GradeLevelId
				&& s.Name == request.Name.Trim());

		if (nameExists)
			throw new InvalidOperationException(
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
		await db.SaveChangesAsync();

		return section.Id;
	}

	public async Task UpdateAsync(UpdateSectionRequest request)
	{
		await using var db = await dbFactory.CreateDbContextAsync();

		var section = await db.Sections.FindAsync(request.Id)
			?? throw new InvalidOperationException("Section not found.");

		// Check name uniqueness excluding the current record.
		var nameConflict = await db.Sections
			.AsNoTracking()
			.AnyAsync(s => s.InstitutionId == section.InstitutionId
				&& s.GradeLevelId == request.GradeLevelId
				&& s.Name == request.Name.Trim()
				&& s.Id != request.Id);

		if (nameConflict)
			throw new InvalidOperationException(
				$"Another section named '{request.Name}' already exists for this grade level.");

		section.GradeLevelId = request.GradeLevelId;
		section.Name = request.Name.Trim();
		section.AdviserName = request.AdviserName.Trim();
		section.RoomNumber = request.RoomNumber.Trim();
		section.MaxCapacity = request.MaxCapacity;
		section.Status = request.Status;
		section.Version = request.Version;

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

		var section = await db.Sections.FindAsync(id)
			?? throw new InvalidOperationException("Section not found.");

		db.Sections.Remove(section);
		await db.SaveChangesAsync();
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
					DisplayName: $"{gradeLevel.Name} - {section.Name}"))
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