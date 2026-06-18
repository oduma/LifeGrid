using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeGrid.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGoalRefinementAnswerSchemaAndSessionStagingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RefinementQuestionsJson",
                table: "OnboardingProgressCache",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValidatedGoalJson",
                table: "OnboardingProgressCache",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GoalRefinementAnswers",
                columns: table => new
                {
                    RefinementAnswerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RankOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    Question = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Answer = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    GoalId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoalRefinementAnswers", x => x.RefinementAnswerId);
                    table.ForeignKey(
                        name: "FK_GoalRefinementAnswers_Goals_GoalId",
                        column: x => x.GoalId,
                        principalTable: "Goals",
                        principalColumn: "GoalId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GoalRefinementAnswers_GoalId",
                table: "GoalRefinementAnswers",
                column: "GoalId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GoalRefinementAnswers");

            migrationBuilder.DropColumn(
                name: "RefinementQuestionsJson",
                table: "OnboardingProgressCache");

            migrationBuilder.DropColumn(
                name: "ValidatedGoalJson",
                table: "OnboardingProgressCache");
        }
    }
}
