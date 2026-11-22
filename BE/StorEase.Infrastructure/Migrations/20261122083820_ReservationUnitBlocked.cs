using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StorEase.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReservationUnitBlocked : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "UnitBlocked",
                table: "Reservation",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UnitBlocked",
                table: "Reservation");
        }
    }
}
