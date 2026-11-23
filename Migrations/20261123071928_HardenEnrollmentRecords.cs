using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class HardenEnrollmentRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "school",
                table: "Learners",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SectionId",
                schema: "school",
                table: "Learners",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                schema: "school",
                table: "Learners",
                type: "timestamp with time zone",
                nullable: true);

            // Link only exact institution + grade + name matches. Keep legacy text
            // and leave unmatched assignments null for an explicit correction later.
            migrationBuilder.Sql("""
                UPDATE school."Learners" AS l SET "SectionId" = s."Id"
                FROM school."Sections" AS s
                WHERE l."InstitutionId" = s."InstitutionId"
                  AND l."GradeLevelId" = s."GradeLevelId"
                  AND l."SectionName" = s."Name";
                """);

            // NOT VALID skips scanning old rows; PostgreSQL still checks all new
            // inserts and updates. Correct old data, then VALIDATE CONSTRAINT.
            migrationBuilder.Sql("ALTER TABLE school.\"Sections\" ADD CONSTRAINT \"CK_Sections_Values\" CHECK (length(btrim(\"Name\")) BETWEEN 1 AND 100 AND length(btrim(\"AdviserName\")) <= 200 AND length(btrim(\"RoomNumber\")) <= 100 AND \"MaxCapacity\" >= 0 AND \"Status\" IN ('Active', 'Inactive')) NOT VALID;");

            migrationBuilder.CreateIndex(
                name: "IX_Learners_SectionId",
                schema: "school",
                table: "Learners",
                column: "SectionId");

            migrationBuilder.Sql("ALTER TABLE school.\"Learners\" ADD CONSTRAINT \"CK_Learners_BirthDate\" CHECK (\"BirthDate\" > DATE '0001-01-01' AND \"BirthDate\" <= CURRENT_DATE) NOT VALID;");

            migrationBuilder.Sql("ALTER TABLE school.\"Learners\" ADD CONSTRAINT \"CK_Learners_Guardian\" CHECK (length(btrim(\"GuardianName\")) <= 200 AND length(btrim(\"GuardianContact\")) <= 50) NOT VALID;");

            migrationBuilder.Sql("ALTER TABLE school.\"Learners\" ADD CONSTRAINT \"CK_Learners_Lrn\" CHECK (\"Lrn\" ~ '^[0-9]{12}$') NOT VALID;");

            migrationBuilder.Sql("ALTER TABLE school.\"Learners\" ADD CONSTRAINT \"CK_Learners_Names\" CHECK (length(btrim(\"FirstName\")) BETWEEN 1 AND 100 AND length(btrim(\"LastName\")) BETWEEN 1 AND 100 AND length(btrim(\"MiddleName\")) <= 100) NOT VALID;");

            migrationBuilder.Sql("ALTER TABLE school.\"Learners\" ADD CONSTRAINT \"CK_Learners_Status\" CHECK (\"Status\" IN ('Enrolled', 'Transferred', 'Withdrawn', 'Graduated') AND \"Sex\" IN ('Male', 'Female')) NOT VALID;");

            migrationBuilder.AddForeignKey(
                name: "FK_Learners_Sections_SectionId",
                schema: "school",
                table: "Learners",
                column: "SectionId",
                principalSchema: "school",
                principalTable: "Sections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Learners_Sections_SectionId",
                schema: "school",
                table: "Learners");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Sections_Values",
                schema: "school",
                table: "Sections");

            migrationBuilder.DropIndex(
                name: "IX_Learners_SectionId",
                schema: "school",
                table: "Learners");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Learners_BirthDate",
                schema: "school",
                table: "Learners");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Learners_Guardian",
                schema: "school",
                table: "Learners");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Learners_Lrn",
                schema: "school",
                table: "Learners");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Learners_Names",
                schema: "school",
                table: "Learners");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Learners_Status",
                schema: "school",
                table: "Learners");

            // Drop new checks before updating preserved legacy rows: NOT VALID
            // checks still reject updates to old records with invalid values.
            // Preserve current names if a section was renamed since the upgrade.
            migrationBuilder.Sql("""
                UPDATE school."Learners" AS l SET "SectionName" = s."Name"
                FROM school."Sections" AS s WHERE l."SectionId" = s."Id";
                """);

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "school",
                table: "Learners");

            migrationBuilder.DropColumn(
                name: "SectionId",
                schema: "school",
                table: "Learners");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                schema: "school",
                table: "Learners");
        }
    }
}
