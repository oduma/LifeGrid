using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeGrid.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase18_CleanupOnboardingSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Delete all existing sessions — any row predating this migration is invalid
            // because IsComplete-flagged rows are no longer meaningful, and sessions with
            // an old schema cannot be safely continued after the PendingGoalId → GoalId rename.
            migrationBuilder.Sql("DELETE FROM OnboardingProgressCache");

            migrationBuilder.DropColumn(
                name: "CachedGoalId",
                table: "OnboardingProgressCache");

            migrationBuilder.DropColumn(
                name: "IsComplete",
                table: "OnboardingProgressCache");

            migrationBuilder.RenameColumn(
                name: "PendingGoalId",
                table: "OnboardingProgressCache",
                newName: "GoalId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "GoalId",
                table: "OnboardingProgressCache",
                newName: "PendingGoalId");

            migrationBuilder.AddColumn<Guid>(
                name: "CachedGoalId",
                table: "OnboardingProgressCache",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsComplete",
                table: "OnboardingProgressCache",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }
    }
}
