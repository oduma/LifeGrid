using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeGrid.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase33_AddViceCheckAudits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ViceCheckAudits",
                columns: table => new
                {
                    AuditId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WeekId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WeekGoalId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BadHabitId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GoalDescription = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    BadHabitDescription = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    DangerLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    Question = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Answer = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    PenaltyPercentApplied = table.Column<double>(type: "REAL", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ViceCheckAudits", x => x.AuditId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ViceCheckAudits_BadHabitId",
                table: "ViceCheckAudits",
                column: "BadHabitId");

            migrationBuilder.CreateIndex(
                name: "IX_ViceCheckAudits_WeekId",
                table: "ViceCheckAudits",
                column: "WeekId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ViceCheckAudits");
        }
    }
}
