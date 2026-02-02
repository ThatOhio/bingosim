using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BingoSim.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSimulationBatchAndRun : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BatchTeamAggregates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SimulationBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StrategyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MeanPoints = table.Column<double>(type: "double precision", nullable: false),
                    MinPoints = table.Column<int>(type: "integer", nullable: false),
                    MaxPoints = table.Column<int>(type: "integer", nullable: false),
                    MeanTilesCompleted = table.Column<double>(type: "double precision", nullable: false),
                    MinTilesCompleted = table.Column<int>(type: "integer", nullable: false),
                    MaxTilesCompleted = table.Column<int>(type: "integer", nullable: false),
                    MeanRowReached = table.Column<double>(type: "double precision", nullable: false),
                    MinRowReached = table.Column<int>(type: "integer", nullable: false),
                    MaxRowReached = table.Column<int>(type: "integer", nullable: false),
                    WinnerRate = table.Column<double>(type: "double precision", nullable: false),
                    RunCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BatchTeamAggregates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EventSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SimulationBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventConfigJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SimulationBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RunsRequested = table.Column<int>(type: "integer", nullable: false),
                    Seed = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ExecutionMode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SimulationBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SimulationRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SimulationBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    RunIndex = table.Column<int>(type: "integer", nullable: false),
                    Seed = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    LastAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SimulationRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TeamRunResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SimulationRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StrategyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StrategyParamsJson = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    TotalPoints = table.Column<int>(type: "integer", nullable: false),
                    TilesCompletedCount = table.Column<int>(type: "integer", nullable: false),
                    RowReached = table.Column<int>(type: "integer", nullable: false),
                    IsWinner = table.Column<bool>(type: "boolean", nullable: false),
                    RowUnlockTimesJson = table.Column<string>(type: "jsonb", nullable: false),
                    TileCompletionTimesJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamRunResults", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BatchTeamAggregates_SimulationBatchId",
                table: "BatchTeamAggregates",
                column: "SimulationBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_EventSnapshots_SimulationBatchId",
                table: "EventSnapshots",
                column: "SimulationBatchId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SimulationBatches_EventId",
                table: "SimulationBatches",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_SimulationBatches_Status",
                table: "SimulationBatches",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SimulationRuns_SimulationBatchId",
                table: "SimulationRuns",
                column: "SimulationBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamRunResults_SimulationRunId",
                table: "TeamRunResults",
                column: "SimulationRunId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BatchTeamAggregates");

            migrationBuilder.DropTable(
                name: "EventSnapshots");

            migrationBuilder.DropTable(
                name: "SimulationBatches");

            migrationBuilder.DropTable(
                name: "SimulationRuns");

            migrationBuilder.DropTable(
                name: "TeamRunResults");
        }
    }
}
