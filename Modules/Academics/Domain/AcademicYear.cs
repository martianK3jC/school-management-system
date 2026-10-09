namespace SchoolManagementSystem.Modules.Academics.Domain;

public class AcademicYear
{
    public Guid Id { get; set; }

    public Guid InstitutionId { get; set; }

    public string Label { get; set; } = string.Empty;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public string State { get; set; } = "Draft";

    public long Version { get; set; } = 1;
}