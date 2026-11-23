namespace SchoolManagementSystem.Modules.Enrollment.Domain;

/// <summary>
/// Represents a class section within an institution (e.g., "Grade 1 - A").
/// Lives in the Enrollment module because sections describe how learners
/// are organized, not academic structure itself.
/// </summary>
public class Section
{
	public Guid Id { get; set; }

	/// <summary>School owning this section. List queries filter by this scope; authenticated access control is a future milestone.</summary>
	public Guid InstitutionId { get; set; }

	/// <summary>Links to the grade level this section belongs to.</summary>
	public Guid GradeLevelId { get; set; }

	/// <summary>Section identifier within the grade level (e.g., "A", "B", "Mabini").</summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// Class adviser's full name. Stored as a string rather than a Teacher FK
	/// because the Teacher entity does not exist yet. When it does, this can be
	/// promoted to a foreign key in a future migration.
	/// </summary>
	public string AdviserName { get; set; } = string.Empty;

	/// <summary>Physical room number or name. Optional.</summary>
	public string RoomNumber { get; set; } = string.Empty;

	/// <summary>Maximum number of learners allowed in this section. 0 means no limit.</summary>
	public int MaxCapacity { get; set; }

	/// <summary>"Active" or "Inactive". Matches the string-status convention used by Learner and AcademicYear.</summary>
	public string Status { get; set; } = "Active";

	/// <summary>Optimistic concurrency token. Configured as IsConcurrencyToken() in DbContext.</summary>
	public long Version { get; set; } = 1;
}