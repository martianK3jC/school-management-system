using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddLearnersTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Learners",
                schema: "school",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    GradeLevelId = table.Column<Guid>(type: "uuid", nullable: false),
                    Lrn = table.Column<string>(type: "text", nullable: false),
                    FirstName = table.Column<string>(type: "text", nullable: false),
                    MiddleName = table.Column<string>(type: "text", nullable: false),
                    LastName = table.Column<string>(type: "text", nullable: false),
                    Sex = table.Column<string>(type: "text", nullable: false),
                    BirthDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SectionName = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    GuardianName = table.Column<string>(type: "text", nullable: false),
                    GuardianContact = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Learners", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Learners_GradeLevels_GradeLevelId",
                        column: x => x.GradeLevelId,
                        principalSchema: "school",
                        principalTable: "GradeLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Learners_Institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalSchema: "school",
                        principalTable: "Institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Learners_GradeLevelId",
                schema: "school",
                table: "Learners",
                column: "GradeLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_Learners_InstitutionId_Lrn",
                schema: "school",
                table: "Learners",
                columns: new[] { "InstitutionId", "Lrn" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Learners",
                schema: "school");
        }
    }
}
