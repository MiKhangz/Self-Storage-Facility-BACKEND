using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StorEase.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueStaffFacilityIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StaffFacility_UserId",
                table: "StaffFacility");

            migrationBuilder.CreateIndex(
                name: "IX_StaffFacility_UserId_FacilityId",
                table: "StaffFacility",
                columns: new[] { "UserId", "FacilityId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StaffFacility_UserId_FacilityId",
                table: "StaffFacility");

            migrationBuilder.CreateIndex(
                name: "IX_StaffFacility_UserId",
                table: "StaffFacility",
                column: "UserId");
        }
    }
}
