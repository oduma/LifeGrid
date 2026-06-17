using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeGrid.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialOnboardingSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OnboardingProgressCache",
                columns: table => new
                {
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CurrentStep = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    IsComplete = table.Column<bool>(type: "INTEGER", nullable: false),
                    RawGoalDraft = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    LastActiveTimestamp = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingProgressCache", x => x.SessionId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OnboardingProgressCache");
        }
    }
}
