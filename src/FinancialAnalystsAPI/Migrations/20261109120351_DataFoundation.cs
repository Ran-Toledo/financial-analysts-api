using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinancialAnalystsAPI.Migrations
{
    /// <inheritdoc />
    public partial class DataFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rows from the old hand-entered CRUD API have no firm and would violate the new required
            // FirmId and unique (FirmId, NormalizedName). Analysts now come from ingestion, so clear them.
            migrationBuilder.Sql("DELETE FROM \"Analysts\";");

            migrationBuilder.DropColumn(
                name: "Expertise",
                table: "Analysts");

            migrationBuilder.AlterColumn<double>(
                name: "Rating",
                table: "Analysts",
                type: "REAL",
                nullable: true,
                oldClrType: typeof(double),
                oldType: "REAL");

            migrationBuilder.AddColumn<int>(
                name: "FirmId",
                table: "Analysts",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedName",
                table: "Analysts",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "Firms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Firms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IngestionRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Backfill = table.Column<bool>(type: "INTEGER", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FinishedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    StocksProcessed = table.Column<int>(type: "INTEGER", nullable: false),
                    PricesAdded = table.Column<int>(type: "INTEGER", nullable: false),
                    RatingsAdded = table.Column<int>(type: "INTEGER", nullable: false),
                    RatingsUpdated = table.Column<int>(type: "INTEGER", nullable: false),
                    Error = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngestionRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Stocks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Ticker = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Sector = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Industry = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Exchange = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    IsTracked = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsBenchmark = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stocks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PriceSnapshots",
                columns: table => new
                {
                    StockId = table.Column<int>(type: "INTEGER", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Close = table.Column<double>(type: "REAL", nullable: false),
                    AdjClose = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceSnapshots", x => new { x.StockId, x.Date });
                    table.ForeignKey(
                        name: "FK_PriceSnapshots_Stocks_StockId",
                        column: x => x.StockId,
                        principalTable: "Stocks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Ratings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StockId = table.Column<int>(type: "INTEGER", nullable: false),
                    FirmId = table.Column<int>(type: "INTEGER", nullable: false),
                    AnalystId = table.Column<int>(type: "INTEGER", nullable: true),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    RawAction = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    RatingFrom = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    RatingTo = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    RatingCarriedForward = table.Column<bool>(type: "INTEGER", nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    PriceTargetFrom = table.Column<double>(type: "REAL", nullable: true),
                    PriceTargetTo = table.Column<double>(type: "REAL", nullable: true),
                    PriceAtCall = table.Column<double>(type: "REAL", nullable: true),
                    Source = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    SourceKey = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    IngestedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ratings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ratings_Analysts_AnalystId",
                        column: x => x.AnalystId,
                        principalTable: "Analysts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Ratings_Firms_FirmId",
                        column: x => x.FirmId,
                        principalTable: "Firms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Ratings_Stocks_StockId",
                        column: x => x.StockId,
                        principalTable: "Stocks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Analysts_FirmId_NormalizedName",
                table: "Analysts",
                columns: new[] { "FirmId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Firms_NormalizedName",
                table: "Firms",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IngestionRuns_StartedAtUtc",
                table: "IngestionRuns",
                column: "StartedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Ratings_AnalystId_Date",
                table: "Ratings",
                columns: new[] { "AnalystId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_Ratings_Date",
                table: "Ratings",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_Ratings_FirmId",
                table: "Ratings",
                column: "FirmId");

            migrationBuilder.CreateIndex(
                name: "IX_Ratings_Source_SourceKey",
                table: "Ratings",
                columns: new[] { "Source", "SourceKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ratings_StockId_Date",
                table: "Ratings",
                columns: new[] { "StockId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_Stocks_Sector",
                table: "Stocks",
                column: "Sector");

            migrationBuilder.CreateIndex(
                name: "IX_Stocks_Ticker",
                table: "Stocks",
                column: "Ticker",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Analysts_Firms_FirmId",
                table: "Analysts",
                column: "FirmId",
                principalTable: "Firms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Analysts_Firms_FirmId",
                table: "Analysts");

            migrationBuilder.DropTable(
                name: "IngestionRuns");

            migrationBuilder.DropTable(
                name: "PriceSnapshots");

            migrationBuilder.DropTable(
                name: "Ratings");

            migrationBuilder.DropTable(
                name: "Firms");

            migrationBuilder.DropTable(
                name: "Stocks");

            migrationBuilder.DropIndex(
                name: "IX_Analysts_FirmId_NormalizedName",
                table: "Analysts");

            migrationBuilder.DropColumn(
                name: "FirmId",
                table: "Analysts");

            migrationBuilder.DropColumn(
                name: "NormalizedName",
                table: "Analysts");

            migrationBuilder.AlterColumn<double>(
                name: "Rating",
                table: "Analysts",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0,
                oldClrType: typeof(double),
                oldType: "REAL",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Expertise",
                table: "Analysts",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
