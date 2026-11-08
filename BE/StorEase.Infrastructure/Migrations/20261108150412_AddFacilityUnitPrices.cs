using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StorEase.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFacilityUnitPrices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UnitPrice_PricingPolicyId",
                table: "UnitPrice");

            migrationBuilder.AddColumn<int>(
                name: "FacilityId",
                table: "UnitPrice",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UnitPrice_FacilityId",
                table: "UnitPrice",
                column: "FacilityId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitPrice_PricingPolicyId_UnitTypeId_FacilityId",
                table: "UnitPrice",
                columns: new[] { "PricingPolicyId", "UnitTypeId", "FacilityId" });

            migrationBuilder.AddForeignKey(
                name: "FK_UnitPrice_Facility_FacilityId",
                table: "UnitPrice",
                column: "FacilityId",
                principalTable: "Facility",
                principalColumn: "FacilityId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UnitPrice_Facility_FacilityId",
                table: "UnitPrice");

            migrationBuilder.DropIndex(
                name: "IX_UnitPrice_FacilityId",
                table: "UnitPrice");

            migrationBuilder.DropIndex(
                name: "IX_UnitPrice_PricingPolicyId_UnitTypeId_FacilityId",
                table: "UnitPrice");

            migrationBuilder.DropColumn(
                name: "FacilityId",
                table: "UnitPrice");

            migrationBuilder.CreateIndex(
                name: "IX_UnitPrice_PricingPolicyId",
                table: "UnitPrice",
                column: "PricingPolicyId");
        }
    }
}
