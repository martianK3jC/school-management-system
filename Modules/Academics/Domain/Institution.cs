namespace SchoolManagementSystem.Modules.Academics.Domain;

public class Institution
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public long Version { get; set; } = 1;
}
