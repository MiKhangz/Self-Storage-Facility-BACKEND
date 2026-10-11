using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StorEase.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddShiftDateIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Shift_FacilityId",
                table: "Shift");

            migrationBuilder.DropIndex(
                name: "IX_Shift_UserId",
                table: "Shift");

            migrationBuilder.CreateIndex(
                name: "IX_Shift_FacilityId_ShiftDate",
                table: "Shift",
                columns: new[] { "FacilityId", "ShiftDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Shift_UserId_ShiftDate",
                table: "Shift",
                columns: new[] { "UserId", "ShiftDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Shift_FacilityId_ShiftDate",
                table: "Shift");

            migrationBuilder.DropIndex(
                name: "IX_Shift_UserId_ShiftDate",
                table: "Shift");

            migrationBuilder.CreateIndex(
                name: "IX_Shift_FacilityId",
                table: "Shift",
                column: "FacilityId");

            migrationBuilder.CreateIndex(
                name: "IX_Shift_UserId",
                table: "Shift",
                column: "UserId");
        }
    }
}
