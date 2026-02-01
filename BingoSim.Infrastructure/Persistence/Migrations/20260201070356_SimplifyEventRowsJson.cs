using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BingoSim.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyEventRowsJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Rows",
                table: "Events",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]",
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Rows",
                table: "Events",
                type: "jsonb",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "jsonb");
        }
    }
}
