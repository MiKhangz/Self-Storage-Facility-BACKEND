using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StorEase.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PinPricingPolicyToRentals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PricingPolicyId",
                table: "Reservation",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PricingPolicyId",
                table: "Contract",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reservation_PricingPolicyId",
                table: "Reservation",
                column: "PricingPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_Contract_PricingPolicyId",
                table: "Contract",
                column: "PricingPolicyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Contract_PricingPolicy_PricingPolicyId",
                table: "Contract",
                column: "PricingPolicyId",
                principalTable: "PricingPolicy",
                principalColumn: "PricingPolicyId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Reservation_PricingPolicy_PricingPolicyId",
                table: "Reservation",
                column: "PricingPolicyId",
                principalTable: "PricingPolicy",
                principalColumn: "PricingPolicyId",
                onDelete: ReferentialAction.Restrict);
        
            // Existing rows get the version that was in force on their date (Draft versions never were).
            migrationBuilder.Sql(@"
UPDATE r SET PricingPolicyId = (
    SELECT TOP 1 p.PricingPolicyId FROM PricingPolicy p
    WHERE p.Status IN ('Active', 'Superseded') AND p.EffectiveFrom <= r.MoveInDate
      AND (p.EffectiveTo IS NULL OR p.EffectiveTo >= r.MoveInDate)
    ORDER BY p.EffectiveFrom DESC)
FROM Reservation r;

UPDATE c SET PricingPolicyId = COALESCE(r.PricingPolicyId, (
    SELECT TOP 1 p.PricingPolicyId FROM PricingPolicy p
    WHERE p.Status IN ('Active', 'Superseded') AND p.EffectiveFrom <= c.StartDate
      AND (p.EffectiveTo IS NULL OR p.EffectiveTo >= c.StartDate)
    ORDER BY p.EffectiveFrom DESC))
FROM Contract c JOIN Reservation r ON r.ReservationId = c.ReservationId;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contract_PricingPolicy_PricingPolicyId",
                table: "Contract");

            migrationBuilder.DropForeignKey(
                name: "FK_Reservation_PricingPolicy_PricingPolicyId",
                table: "Reservation");

            migrationBuilder.DropIndex(
                name: "IX_Reservation_PricingPolicyId",
                table: "Reservation");

            migrationBuilder.DropIndex(
                name: "IX_Contract_PricingPolicyId",
                table: "Contract");

            migrationBuilder.DropColumn(
                name: "PricingPolicyId",
                table: "Reservation");

            migrationBuilder.DropColumn(
                name: "PricingPolicyId",
                table: "Contract");
        }
    }
}
