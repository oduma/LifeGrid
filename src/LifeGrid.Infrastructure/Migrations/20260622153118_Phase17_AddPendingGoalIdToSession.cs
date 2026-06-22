using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeGrid.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase17_AddPendingGoalIdToSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PendingGoalId",
                table: "OnboardingProgressCache",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PendingGoalId",
                table: "OnboardingProgressCache");
        }
    }
}
