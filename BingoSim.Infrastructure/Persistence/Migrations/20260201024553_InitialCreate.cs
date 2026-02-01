using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BingoSim.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlayerProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SkillTimeMultiplier = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Capabilities = table.Column<string>(type: "jsonb", nullable: true),
                    WeeklySchedule = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerProfiles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerProfiles_CreatedAt",
                table: "PlayerProfiles",
                column: "CreatedAt",
                descending: new bool[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerProfiles");
        }
    }
}
