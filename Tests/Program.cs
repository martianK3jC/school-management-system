using System.Data.Common;
using System.Data;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using SchoolManagementSystem.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SchoolManagementSystem.Data;
using SchoolManagementSystem.Modules.Enrollment.Application;
using SchoolManagementSystem.Modules.Enrollment.Domain;

// Explicit opt-in: read the configured development database, never change it.
if (args.Contains("--audit-database"))
{
    var configuration = new ConfigurationBuilder()
        .AddJsonFile(Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json"), optional: true)
        .AddJsonFile(Path.Combine(Directory.GetCurrentDirectory(), "appsettings.Development.json"), optional: true)
        .AddUserSecrets(typeof(SchoolDbContext).Assembly, optional: true)
        .AddEnvironmentVariables().Build();
    var connectionString = configuration.GetConnectionString("SchoolDatabase")
        ?? throw new InvalidOperationException("SchoolDatabase connection is missing.");
    await using var connection = new NpgsqlConnection(connectionString);
    await connection.OpenAsync();
    await using (var history = new NpgsqlCommand("SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\"", connection))
    await using (var reader = await history.ExecuteReaderAsync())
        while (await reader.ReadAsync()) Console.WriteLine("Applied migration: " + reader.GetString(0));
    await using (var count = new NpgsqlCommand("SELECT count(*) FROM school.\"Learners\"", connection))
        Console.WriteLine("Stored learners: " + await count.ExecuteScalarAsync());
    await using var audit = new NpgsqlCommand(await File.ReadAllTextAsync("Data/EnrollmentDataAudit.sql"), connection);
    await using var auditReader = await audit.ExecuteReaderAsync();
    var report = 0;
    do
    {
        var rows = 0;
        while (await auditReader.ReadAsync()) rows++;
        Console.WriteLine($"Audit report {++report}: {rows} record(s) needing review");
    } while (await auditReader.NextResultAsync());
    return;
}

var passed = 0;
void Check(bool condition, string label)
{
    if (!condition) throw new Exception("FAIL: " + label);
    Console.WriteLine("PASS: " + label);
    passed++;
}

var valid = new CreateLearnerRequest(Guid.NewGuid(), Guid.NewGuid(), "001234567890",
    "Kesha Jane", "Lepiten", "Ceniza", "Female", new DateOnly(2015, 1, 1), "", "Guardian", "09123456789");
EnrollmentValidation.Learner(valid);
Check(true, "12 digits including leading zeros and compound names are accepted");
foreach (var bad in new[] { "fdfsdf", "123", new string('9', 100), "12345678901a", "１２３４５６７８９０１２" })
{
    try { EnrollmentValidation.Learner(valid with { Lrn = bad }); throw new Exception("Accepted invalid LRN"); }
    catch (EnrollmentException) { Check(true, "invalid LRN rejected: " + bad[..Math.Min(15, bad.Length)]); }
}
foreach (var bad in new[] {
    valid with { FirstName = " " }, valid with { LastName = new string('a', 101) },
    valid with { BirthDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1) },
    valid with { BirthDate = DateOnly.MinValue }, valid with { Sex = "invalid" },
    valid with { GuardianContact = new string('1', 51) } })
{
    try { EnrollmentValidation.Learner(bad); throw new Exception("Accepted invalid learner"); }
    catch (EnrollmentException) { Check(true, "invalid learner field rejected"); }
}
try { EnrollmentValidation.Learner(valid, "invalid"); throw new Exception("Accepted invalid status"); }
catch (EnrollmentException) { Check(true, "invalid status rejected"); }

// Use the real PostgreSQL provider. Suppress opening the connection and stop at
// command execution. EF must translate the entire query before reaching here.
// This reproduces translation failures without credentials or a live database.
var capture = new CaptureCommands();
var options = new DbContextOptionsBuilder<SchoolDbContext>()
    .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
    .AddInterceptors(new NoConnection(), capture).Options;
var factory = new ContextFactory(options);
var learners = new LearnerService(factory);
var sections = new SectionService(factory);
foreach (var (label, action) in new (string, Func<Task>)[] {
    ("learner detail", async () => { await learners.GetDetailByIdAsync(Guid.NewGuid()); }),
    ("learner summary", async () => { await learners.GetByIdAsync(Guid.NewGuid()); }),
    ("section detail", async () => { await sections.GetDetailByIdAsync(Guid.NewGuid()); }),
    ("section summary", async () => { await sections.GetByIdAsync(Guid.NewGuid()); }) })
{
    capture.Sql = "";
    try { await action(); throw new Exception("Expected captured command"); }
    catch (CommandCaptured) { Check(capture.Sql.Contains("WHERE") && capture.Sql.Contains("LIMIT 1"), label + " translates to a filtered PostgreSQL query"); }
}
capture.Stop = false;
capture.Commands.Clear();
var paged = await learners.GetLearnersAsync(new LearnerFilter(valid.InstitutionId, "Jane", valid.GradeLevelId,
    null, "Enrolled", Guid.NewGuid(), Page: 2, PageSize: 25));
Check(paged.Page == 2 && paged.PageSize == 25 && paged.FilteredCount == 51,
    "pagination retains database totals separately from page size");
Check(capture.Commands.Any(sql => sql.Contains("LIMIT") && sql.Contains("OFFSET") && sql.Contains("ORDER BY")),
    "filtered learner page translates with stable ordering and server pagination");
capture.Commands.Clear();
await sections.GetSectionsAsync(new SectionFilter(valid.InstitutionId, "A", valid.GradeLevelId, null, "Active"));
Check(capture.Commands.Any(sql => sql.Contains("count(*)") && sql.Contains("SectionId")),
    "section learner counts translate using the section foreign key");
await sections.GetSectionOptionsAsync(valid.InstitutionId, valid.GradeLevelId);
Check(true, "grade-specific section dropdown query translates");

await using (var db = factory.CreateDbContext())
{
    var learner = new Learner { Id = Guid.NewGuid(), Version = 7 };
    db.Attach(learner);
    learner.Version++;
    db.ChangeTracker.DetectChanges();
    var version = db.Entry(learner).Property(l => l.Version);
    Check(version.Metadata.IsConcurrencyToken && version.OriginalValue == 7 && version.CurrentValue == 8,
        "incrementing version preserves the original concurrency predicate");
    Check(db.Model.FindEntityType(typeof(Learner))!.GetForeignKeys().Any(f =>
        f.PrincipalEntityType.ClrType == typeof(Section) && f.DeleteBehavior == DeleteBehavior.Restrict),
        "assigned sections have a restricted foreign key");
}
// Rollback must remove new checks before touching invalid preserved legacy rows.
var rollback = new HardenEnrollmentRecords().DownOperations.ToList();
var restoreNames = rollback.FindIndex(op => op is SqlOperation sql && sql.Sql.Contains("UPDATE school."));
var learnerChecks = rollback.Select((operation, index) => (operation, index))
    .Where(item => item.operation is DropCheckConstraintOperation { Table: "Learners" }).ToList();
Check(restoreNames >= 0 && learnerChecks.Count == 5 && learnerChecks.All(item => item.index < restoreNames),
    "rollback removes learner checks before restoring legacy section names");
Check(rollback.FindIndex(op => op is DropColumnOperation { Name: "SectionId" }) > restoreNames,
    "rollback preserves current section names before dropping the relationship");
Console.WriteLine($"All {passed} regression checks passed. Live database persistence is a separate integration check.");

sealed class ContextFactory(DbContextOptions<SchoolDbContext> options) : IDbContextFactory<SchoolDbContext>
{
    public SchoolDbContext CreateDbContext() => new(options);
}
sealed class CommandCaptured : Exception;
sealed class NoConnection : DbConnectionInterceptor
{
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection,
        ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(InterceptionResult.Suppress());
}
sealed class CaptureCommands : DbCommandInterceptor
{
    public string Sql { get; set; } = "";
    public bool Stop { get; set; } = true;
    public List<string> Commands { get; } = [];
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        Sql = command.CommandText;
        Commands.Add(Sql);
        if (Stop) throw new CommandCaptured();
        var table = new DataTable();
        if (Sql.StartsWith("SELECT count(*)", StringComparison.Ordinal))
        {
            table.Columns.Add("count", typeof(int));
            table.Rows.Add(51);
        }
        return ValueTask.FromResult(InterceptionResult<DbDataReader>.SuppressWithResult(table.CreateDataReader()));
    }
}
