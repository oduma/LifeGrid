using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeGrid.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserProfileAndGoalSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "OnboardingProgressCache",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UserProfiles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CurrentLevel = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserProfiles", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "Goals",
                columns: table => new
                {
                    GoalId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    AmbientTag = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Duration = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    DeadlineDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Goals", x => x.GoalId);
                    table.ForeignKey(
                        name: "FK_Goals_UserProfiles_UserId",
                        column: x => x.UserId,
                        principalTable: "UserProfiles",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserActiveStates",
                columns: table => new
                {
                    UserProfileUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DoubleXpMode = table.Column<bool>(type: "INTEGER", nullable: false),
                    DoubleXpExpiry = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserActiveStates", x => x.UserProfileUserId);
                    table.ForeignKey(
                        name: "FK_UserActiveStates_UserProfiles_UserProfileUserId",
                        column: x => x.UserProfileUserId,
                        principalTable: "UserProfiles",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserBadges",
                columns: table => new
                {
                    BadgeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BadgeType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    DateEarned = table.Column<DateTime>(type: "TEXT", nullable: false),
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

            migrationBuilder.CreateTable(
                name: "UserEconomy",
                columns: table => new
                {
                    UserProfileUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LifetimeGpAverage = table.Column<double>(type: "REAL", nullable: false),
                    LifetimeXp = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentSp = table.Column<int>(type: "INTEGER", nullable: false),
                    ShieldsAvailable = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxShieldCap = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserEconomy", x => x.UserProfileUserId);
                    table.ForeignKey(
                        name: "FK_UserEconomy_UserProfiles_UserProfileUserId",
                        column: x => x.UserProfileUserId,
                        principalTable: "UserProfiles",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GoalLinkedBadHabits",
                columns: table => new
                {
                    BadHabitId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    DangerLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    GoalId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoalLinkedBadHabits", x => x.BadHabitId);
                    table.ForeignKey(
                        name: "FK_GoalLinkedBadHabits_Goals_GoalId",
                        column: x => x.GoalId,
                        principalTable: "Goals",
                        principalColumn: "GoalId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingProgressCache_UserId",
                table: "OnboardingProgressCache",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_GoalLinkedBadHabits_GoalId",
                table: "GoalLinkedBadHabits",
                column: "GoalId");

            migrationBuilder.CreateIndex(
                name: "IX_Goals_UserId",
                table: "Goals",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserBadges_UserId",
                table: "UserBadges",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_OnboardingProgressCache_UserProfiles_UserId",
                table: "OnboardingProgressCache",
                column: "UserId",
                principalTable: "UserProfiles",
                principalColumn: "UserId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OnboardingProgressCache_UserProfiles_UserId",
                table: "OnboardingProgressCache");

            migrationBuilder.DropTable(
                name: "GoalLinkedBadHabits");

            migrationBuilder.DropTable(
                name: "UserActiveStates");

            migrationBuilder.DropTable(
                name: "UserBadges");

            migrationBuilder.DropTable(
                name: "UserEconomy");

            migrationBuilder.DropTable(
                name: "Goals");

            migrationBuilder.DropTable(
                name: "UserProfiles");

            migrationBuilder.DropIndex(
                name: "IX_OnboardingProgressCache_UserId",
                table: "OnboardingProgressCache");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "OnboardingProgressCache");
        }
    }
}
