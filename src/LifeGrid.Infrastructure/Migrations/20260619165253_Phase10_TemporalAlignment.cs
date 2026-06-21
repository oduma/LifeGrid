using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeGrid.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase10_TemporalAlignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WeekGoalNumber",
                table: "WeekGoals",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // Backfill WeekGoalNumber: assign sequential 1-based values per GoalId ordered by the
            // linked Week's StartDate, so existing rows get meaningful sequence numbers.
            migrationBuilder.Sql(@"
                UPDATE WeekGoals SET WeekGoalNumber = (
                    SELECT rn FROM (
                        SELECT wg.WeekGoalId,
                               ROW_NUMBER() OVER (
                                   PARTITION BY wg.GoalId
                                   ORDER BY (SELECT w.StartDate FROM Weeks w WHERE w.WeekId = wg.WeekId)
                               ) AS rn
                        FROM WeekGoals wg
                    ) sub
                    WHERE sub.WeekGoalId = WeekGoals.WeekGoalId
                );
            ");

            migrationBuilder.AddColumn<DateTime>(
                name: "StartDate",
                table: "Goals",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2026, 6, 23, 0, 0, 0, DateTimeKind.Local));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WeekGoalNumber",
                table: "WeekGoals");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "Goals");
        }
    }
}
