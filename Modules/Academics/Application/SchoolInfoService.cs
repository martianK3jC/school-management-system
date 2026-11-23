using Microsoft.EntityFrameworkCore;
using SchoolManagementSystem.Data;

namespace SchoolManagementSystem.Modules.Academics.Application;

public record SchoolInfo(string Name, string? AcademicYear);

public class SchoolInfoService(IDbContextFactory<SchoolDbContext> factory)
{
    public async Task<SchoolInfo?> GetAsync(Guid institutionId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var name = await db.Institutions.AsNoTracking().Where(i => i.Id == institutionId)
            .Select(i => i.DisplayName).FirstOrDefaultAsync();
        if (name is null) return null;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var year = await db.AcademicYears.AsNoTracking()
            .Where(y => y.InstitutionId == institutionId && y.State == "Active" && y.StartDate <= today && y.EndDate >= today)
            .OrderByDescending(y => y.StartDate).Select(y => y.Label).FirstOrDefaultAsync();
        return new SchoolInfo(name, year);
    }
}
