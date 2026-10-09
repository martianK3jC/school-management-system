namespace SchoolManagementSystem.Modules.Academics.Domain;

public class Campus
{
    public Guid Id { get; set; }

    public Guid InstitutionId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public long Version { get; set; } = 1;
}