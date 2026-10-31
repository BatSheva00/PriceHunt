using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PriceHunt.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SearchRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FromLocation = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    ToLocation = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    FromDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ToDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SupplierNamesCsv = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TotalSuppliers = table.Column<int>(type: "INTEGER", nullable: false),
                    RespondedCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SearchRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Quotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SearchRunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SupplierName = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Price = table.Column<double>(type: "REAL", nullable: false),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    ResponseTimeMs = table.Column<int>(type: "INTEGER", nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    FromLocation = table.Column<string>(type: "TEXT", nullable: false),
                    ToLocation = table.Column<string>(type: "TEXT", nullable: false),
                    SearchStartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FromDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ToDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Quotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Quotes_SearchRuns_SearchRunId",
                        column: x => x.SearchRunId,
                        principalTable: "SearchRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_FromLocation_ToLocation_SearchStartedAt",
                table: "Quotes",
                columns: new[] { "FromLocation", "ToLocation", "SearchStartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_SearchRunId_SupplierName",
                table: "Quotes",
                columns: new[] { "SearchRunId", "SupplierName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_SearchStartedAt",
                table: "Quotes",
                column: "SearchStartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_SupplierName_SearchStartedAt",
                table: "Quotes",
                columns: new[] { "SupplierName", "SearchStartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SearchRuns_StartedAt",
                table: "SearchRuns",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SearchRuns_Status",
                table: "SearchRuns",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Quotes");

            migrationBuilder.DropTable(
                name: "SearchRuns");
        }
    }
}
