namespace SchoolManagementSystem.Modules.Enrollment.Domain;

public class Learner
{
    public Guid Id { get; set; }

    public Guid InstitutionId { get; set; }

    public Guid GradeLevelId { get; set; }

    public string Lrn { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string MiddleName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Sex { get; set; } = string.Empty;

    public DateOnly BirthDate { get; set; }

    public Guid? SectionId { get; set; }

    public Section? Section { get; set; }

    public DateTimeOffset? CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public string SectionName { get; set; } = string.Empty;

    public string Status { get; set; } = "Enrolled";

    public string GuardianName { get; set; } = string.Empty;

    public string GuardianContact { get; set; } = string.Empty;

    public long Version { get; set; } = 1;
}