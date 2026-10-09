namespace SchoolManagementSystem.Modules.Academics.Domain;

public class GradeLevel
{
    public Guid Id { get; set; }

    public Guid InstitutionId { get; set; }

    public string Identifier { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public long Version { get; set; } = 1;
    
}