using Microsoft.EntityFrameworkCore;
using SchoolManagementSystem.Modules.Academics.Domain;

namespace SchoolManagementSystem.Data;

public class SchoolDbContext(
    DbContextOptions<SchoolDbContext> options) : DbContext(options)
{
    public DbSet<Institution> Institutions => Set<Institution>();
    public DbSet<Campus> Campuses => Set<Campus>();
    public DbSet<AcademicYear> AcademicYears => Set<AcademicYear>();
    public DbSet<GradeLevel> GradeLevels => Set<GradeLevel>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("school");

        modelBuilder.Entity<Campus>()
            .HasOne<Institution>()
            .WithMany()
            .HasForeignKey(x => x.InstitutionId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AcademicYear>()
            .HasOne<Institution>()
            .WithMany()
            .HasForeignKey(x => x.InstitutionId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<GradeLevel>()
            .HasOne<Institution>()
            .WithMany()
            .HasForeignKey(x => x.InstitutionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}