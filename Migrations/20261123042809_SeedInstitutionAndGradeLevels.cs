using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SchoolManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class SeedInstitutionAndGradeLevels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "school",
                table: "Institutions",
                columns: new[] { "Id", "DisplayName", "Version" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), "Sample School", 1L });

            migrationBuilder.InsertData(
                schema: "school",
                table: "GradeLevels",
                columns: new[] { "Id", "Identifier", "InstitutionId", "Name", "Version" },
                values: new object[,]
                {
                    { new Guid("a0000000-0000-0000-0000-000000000001"), "G1", new Guid("00000000-0000-0000-0000-000000000001"), "Grade 1", 1L },
                    { new Guid("a0000000-0000-0000-0000-000000000002"), "G2", new Guid("00000000-0000-0000-0000-000000000001"), "Grade 2", 1L },
                    { new Guid("a0000000-0000-0000-0000-000000000003"), "G3", new Guid("00000000-0000-0000-0000-000000000001"), "Grade 3", 1L },
                    { new Guid("a0000000-0000-0000-0000-000000000004"), "G4", new Guid("00000000-0000-0000-0000-000000000001"), "Grade 4", 1L },
                    { new Guid("a0000000-0000-0000-0000-000000000005"), "G5", new Guid("00000000-0000-0000-0000-000000000001"), "Grade 5", 1L },
                    { new Guid("a0000000-0000-0000-0000-000000000006"), "G6", new Guid("00000000-0000-0000-0000-000000000001"), "Grade 6", 1L }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "school",
                table: "GradeLevels",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                schema: "school",
                table: "GradeLevels",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                schema: "school",
                table: "GradeLevels",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                schema: "school",
                table: "GradeLevels",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                schema: "school",
                table: "GradeLevels",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000005"));

            migrationBuilder.DeleteData(
                schema: "school",
                table: "GradeLevels",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000006"));

            migrationBuilder.DeleteData(
                schema: "school",
                table: "Institutions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"));
        }
    }
}
