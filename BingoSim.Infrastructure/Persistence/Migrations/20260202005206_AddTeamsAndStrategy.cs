using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BingoSim.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamsAndStrategy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Teams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teams", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StrategyConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    StrategyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ParamsJson = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StrategyConfigs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StrategyConfigs_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TeamPlayers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerProfileId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamPlayers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeamPlayers_PlayerProfiles_PlayerProfileId",
                        column: x => x.PlayerProfileId,
                        principalTable: "PlayerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeamPlayers_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StrategyConfigs_TeamId",
                table: "StrategyConfigs",
                column: "TeamId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeamPlayers_PlayerProfileId",
                table: "TeamPlayers",
                column: "PlayerProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamPlayers_TeamId_PlayerProfileId",
                table: "TeamPlayers",
                columns: new[] { "TeamId", "PlayerProfileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Teams_EventId",
                table: "Teams",
                column: "EventId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StrategyConfigs");

            migrationBuilder.DropTable(
                name: "TeamPlayers");

            migrationBuilder.DropTable(
                name: "Teams");
        }
    }
}
