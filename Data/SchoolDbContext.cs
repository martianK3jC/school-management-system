using Microsoft.EntityFrameworkCore;
using SchoolManagementSystem.Modules.Academics.Domain;
using SchoolManagementSystem.Modules.Enrollment.Domain;

namespace SchoolManagementSystem.Data;

public class SchoolDbContext(
    DbContextOptions<SchoolDbContext> options) : DbContext(options)
{
    public DbSet<Institution> Institutions => Set<Institution>();
    public DbSet<Campus> Campuses => Set<Campus>();
    public DbSet<AcademicYear> AcademicYears => Set<AcademicYear>();
    public DbSet<GradeLevel> GradeLevels => Set<GradeLevel>();
    public DbSet<Learner> Learners => Set<Learner>();
    public DbSet<Section> Sections => Set<Section>();

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

        var learner = modelBuilder.Entity<Learner>();

        // CHECK constraints protect writes outside the UI too. The migration installs
        // them NOT VALID so existing invalid records can be corrected without data loss.
        learner.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Learners_Lrn", "\"Lrn\" ~ '^[0-9]{12}$'");
            t.HasCheckConstraint("CK_Learners_Names", "length(btrim(\"FirstName\")) BETWEEN 1 AND 100 AND length(btrim(\"LastName\")) BETWEEN 1 AND 100 AND length(btrim(\"MiddleName\")) <= 100");
            t.HasCheckConstraint("CK_Learners_Guardian", "length(btrim(\"GuardianName\")) <= 200 AND length(btrim(\"GuardianContact\")) <= 50");
            t.HasCheckConstraint("CK_Learners_Status", "\"Status\" IN ('Enrolled', 'Transferred', 'Withdrawn', 'Graduated') AND \"Sex\" IN ('Male', 'Female')");
            t.HasCheckConstraint("CK_Learners_BirthDate", "\"BirthDate\" > DATE '0001-01-01' AND \"BirthDate\" <= CURRENT_DATE");
        });

        learner.HasOne<Institution>()
            .WithMany()
            .HasForeignKey(x => x.InstitutionId)
            .OnDelete(DeleteBehavior.Restrict);

        learner.HasOne<GradeLevel>()
            .WithMany()
            .HasForeignKey(x => x.GradeLevelId)
            .OnDelete(DeleteBehavior.Restrict);

        learner.HasOne(x => x.Section)
            .WithMany()
            .HasForeignKey(x => x.SectionId)
            .OnDelete(DeleteBehavior.Restrict);

        learner.HasIndex(x => new { x.InstitutionId, x.Lrn })
            .IsUnique();

        learner.Property(x => x.Version)
            .IsConcurrencyToken();

        var section = modelBuilder.Entity<Section>();

        section.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Sections_Values", "length(btrim(\"Name\")) BETWEEN 1 AND 100 AND length(btrim(\"AdviserName\")) <= 200 AND length(btrim(\"RoomNumber\")) <= 100 AND \"MaxCapacity\" >= 0 AND \"Status\" IN ('Active', 'Inactive')");
        });

        section.HasOne<Institution>()
            .WithMany()
            .HasForeignKey(x => x.InstitutionId)
            .OnDelete(DeleteBehavior.Restrict);

        section.HasOne<GradeLevel>()
            .WithMany()
            .HasForeignKey(x => x.GradeLevelId)
            .OnDelete(DeleteBehavior.Restrict);

        section.HasIndex(x => new { x.InstitutionId, x.GradeLevelId, x.Name })
            .IsUnique();

        section.Property(x => x.Version)
            .IsConcurrencyToken();

        // Seed data: ensures the app has a working Institution and GradeLevels
        // out of the box so learners and sections can be created immediately.
        var institutionId = Guid.Parse("00000000-0000-0000-0000-000000000001");

        modelBuilder.Entity<Institution>().HasData(new Institution
        {
            Id = institutionId,
            DisplayName = "Sample School",
            Version = 1
        });

        modelBuilder.Entity<GradeLevel>().HasData(
            new GradeLevel { Id = Guid.Parse("a0000000-0000-0000-0000-000000000001"), InstitutionId = institutionId, Identifier = "G1", Name = "Grade 1", Version = 1 },
            new GradeLevel { Id = Guid.Parse("a0000000-0000-0000-0000-000000000002"), InstitutionId = institutionId, Identifier = "G2", Name = "Grade 2", Version = 1 },
            new GradeLevel { Id = Guid.Parse("a0000000-0000-0000-0000-000000000003"), InstitutionId = institutionId, Identifier = "G3", Name = "Grade 3", Version = 1 },
            new GradeLevel { Id = Guid.Parse("a0000000-0000-0000-0000-000000000004"), InstitutionId = institutionId, Identifier = "G4", Name = "Grade 4", Version = 1 },
            new GradeLevel { Id = Guid.Parse("a0000000-0000-0000-0000-000000000005"), InstitutionId = institutionId, Identifier = "G5", Name = "Grade 5", Version = 1 },
            new GradeLevel { Id = Guid.Parse("a0000000-0000-0000-0000-000000000006"), InstitutionId = institutionId, Identifier = "G6", Name = "Grade 6", Version = 1 }
        );
    }
}