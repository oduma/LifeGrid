using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeGrid.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase16_AddBlueprintCacheToSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BlueprintJson",
                table: "OnboardingProgressCache",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CachedGoalId",
                table: "OnboardingProgressCache",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BlueprintJson",
                table: "OnboardingProgressCache");

            migrationBuilder.DropColumn(
                name: "CachedGoalId",
                table: "OnboardingProgressCache");
        }
    }
}
