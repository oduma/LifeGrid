using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeGrid.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase20_AddHabitCompletionLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HabitCompletionLogs",
                columns: table => new
                {
                    LogId = table.Column<Guid>(type: "TEXT", nullable: false),
                    HabitId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ActualValue = table.Column<double>(type: "REAL", nullable: false),
                    MeasurementUnit = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ProofText = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    ProofImageUrl = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HabitCompletionLogs", x => x.LogId);
                    table.ForeignKey(
                        name: "FK_HabitCompletionLogs_Habits_HabitId",
                        column: x => x.HabitId,
                        principalTable: "Habits",
                        principalColumn: "HabitId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HabitCompletionLogs_HabitId",
                table: "HabitCompletionLogs",
                column: "HabitId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HabitCompletionLogs");
        }
    }
}
