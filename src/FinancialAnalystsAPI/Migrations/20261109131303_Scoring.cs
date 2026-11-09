using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinancialAnalystsAPI.Migrations
{
    /// <inheritdoc />
    public partial class Scoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Rating",
                table: "Analysts");

            migrationBuilder.CreateTable(
                name: "CallOutcomes",
                columns: table => new
                {
                    RatingId = table.Column<int>(type: "INTEGER", nullable: false),
                    HorizonMonths = table.Column<int>(type: "INTEGER", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    StartPrice = table.Column<double>(type: "REAL", nullable: false),
                    EndPrice = table.Column<double>(type: "REAL", nullable: false),
                    Return = table.Column<double>(type: "REAL", nullable: false),
                    BenchmarkReturn = table.Column<double>(type: "REAL", nullable: false),
                    ExcessReturn = table.Column<double>(type: "REAL", nullable: false),
                    DirectionalExcess = table.Column<double>(type: "REAL", nullable: true),
                    Hit = table.Column<bool>(type: "INTEGER", nullable: false),
                    Target = table.Column<double>(type: "REAL", nullable: true),
                    TargetHit = table.Column<bool>(type: "INTEGER", nullable: true),
                    TargetError = table.Column<double>(type: "REAL", nullable: true),
                    ComputedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CallOutcomes", x => new { x.RatingId, x.HorizonMonths });
                    table.ForeignKey(
                        name: "FK_CallOutcomes_Ratings_RatingId",
                        column: x => x.RatingId,
                        principalTable: "Ratings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CallOutcomes_HorizonMonths_EndDate",
                table: "CallOutcomes",
                columns: new[] { "HorizonMonths", "EndDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CallOutcomes");

            migrationBuilder.AddColumn<double>(
                name: "Rating",
                table: "Analysts",
                type: "REAL",
                nullable: true);
        }
    }
}
