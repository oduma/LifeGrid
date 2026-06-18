using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeGrid.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHabitScheduleSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Weeks",
                columns: table => new
                {
                    WeekId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WeekNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    StartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    TotalWeeklySpEarned = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Weeks", x => x.WeekId);
                });

            migrationBuilder.CreateTable(
                name: "WeekGoals",
                columns: table => new
                {
                    WeekGoalId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WeekId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GoalId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PenaltyState = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    GoalWeeklyGp = table.Column<double>(type: "REAL", nullable: false),
                    GoalWeeklyXpEarned = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeekGoals", x => x.WeekGoalId);
                    table.ForeignKey(
                        name: "FK_WeekGoals_Weeks_WeekId",
                        column: x => x.WeekId,
                        principalTable: "Weeks",
                        principalColumn: "WeekId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Habits",
                columns: table => new
                {
                    HabitId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WeekGoalId = table.Column<Guid>(type: "TEXT", nullable: false),
                    HabitType = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    HabitName = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    HabitDescription = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    TargetValue = table.Column<double>(type: "REAL", nullable: false),
                    MeasurementUnit = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    DeadlineDateTime = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Habits", x => x.HabitId);
                    table.ForeignKey(
                        name: "FK_Habits_WeekGoals_WeekGoalId",
                        column: x => x.WeekGoalId,
                        principalTable: "WeekGoals",
                        principalColumn: "WeekGoalId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Habits_WeekGoalId",
                table: "Habits",
                column: "WeekGoalId");

            migrationBuilder.CreateIndex(
                name: "IX_WeekGoals_WeekId",
                table: "WeekGoals",
                column: "WeekId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Habits");

            migrationBuilder.DropTable(
                name: "WeekGoals");

            migrationBuilder.DropTable(
                name: "Weeks");
        }
    }
}
