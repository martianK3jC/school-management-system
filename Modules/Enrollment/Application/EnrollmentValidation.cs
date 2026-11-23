using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SchoolManagementSystem.Data;

namespace SchoolManagementSystem.Modules.Enrollment.Application;

// Only this exception's messages are safe to show to users.
public sealed class EnrollmentException(string message) : Exception(message);

public static class EnrollmentValidation
{
    public const string LrnPattern = "^[0-9]{12}$";
    public const string ConflictMessage = "This record changed while you were editing. Close the form and reopen it before saving.";

    public static void Text(string? value, string label, int maxLength, bool required = false)
    {
        if (required && string.IsNullOrWhiteSpace(value))
            throw new EnrollmentException($"{label} is required.");
        if (value is null || value.Trim().Length > maxLength)
            throw new EnrollmentException($"{label} must be at most {maxLength} characters.");
    }

    public static void Learner(CreateLearnerRequest request, string status = "Enrolled")
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(request.Lrn?.Trim() ?? "", LrnPattern))
            throw new EnrollmentException("LRN must contain exactly 12 digits.");
        Text(request.FirstName, "First name", 100, true);
        Text(request.MiddleName, "Middle name", 100);
        Text(request.LastName, "Last name", 100, true);
        Text(request.GuardianName, "Guardian name", 200);
        Text(request.GuardianContact, "Guardian contact", 50);
        if (request.Sex is not ("Male" or "Female"))
            throw new EnrollmentException("Select a valid sex.");
        if (request.BirthDate == DateOnly.MinValue || request.BirthDate > DateOnly.FromDateTime(DateTime.UtcNow))
            throw new EnrollmentException("Birth date must be a valid date no later than today.");
        if (status is not ("Enrolled" or "Transferred" or "Withdrawn" or "Graduated"))
            throw new EnrollmentException("Select a valid learner status.");
    }

    public static async Task GradeAsync(SchoolDbContext db, Guid institutionId, Guid gradeLevelId)
    {
        if (!await db.GradeLevels.AnyAsync(g => g.Id == gradeLevelId && g.InstitutionId == institutionId))
            throw new EnrollmentException("Select a grade level belonging to this school.");
    }

    public static async Task SaveAsync(SchoolDbContext db)
    {
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { throw new EnrollmentException(ConflictMessage); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        { throw new EnrollmentException("A record with this LRN or section name already exists. Refresh and check your entry."); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23503" })
        { throw new EnrollmentException("A related record changed or is still in use. Refresh and try again."); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23514" })
        { throw new EnrollmentException("Some values do not meet the school record rules. Check the form and try again."); }
    }
}

public sealed class PastOrTodayAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) => value is null ||
        value is DateOnly date && date != DateOnly.MinValue && date <= DateOnly.FromDateTime(DateTime.UtcNow);
}
