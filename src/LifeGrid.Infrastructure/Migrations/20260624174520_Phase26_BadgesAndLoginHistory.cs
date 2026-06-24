using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeGrid.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase26_BadgesAndLoginHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserBadges");

            migrationBuilder.CreateTable(
                name: "Badges",
                columns: table => new
                {
                    BadgeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GoalId = table.Column<Guid>(type: "TEXT", nullable: true),
                    WeekId = table.Column<Guid>(type: "TEXT", nullable: true),
                    BadgeType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    BadgeName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    IconName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Tier = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    IsEarned = table.Column<bool>(type: "INTEGER", nullable: false),
                    DateEarned = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Badges", x => x.BadgeId);
                });

            migrationBuilder.CreateTable(
                name: "LoginHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoginHistory", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Badges");

            migrationBuilder.DropTable(
                name: "LoginHistory");

            migrationBuilder.CreateTable(
                name: "UserBadges",
                columns: table => new
                {
                    BadgeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BadgeType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    DateEarned = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    IconName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserBadges", x => x.BadgeId);
                    table.ForeignKey(
                        name: "FK_UserBadges_UserProfiles_UserId",
                        column: x => x.UserId,
                        principalTable: "UserProfiles",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserBadges_UserId",
                table: "UserBadges",
                column: "UserId");
        }
    }
}
