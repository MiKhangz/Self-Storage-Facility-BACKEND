using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StorEase.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FacilityPriceVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FacilityPriceVersionId",
                table: "Reservation",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FacilityPriceVersion",
                columns: table => new
                {
                    FacilityPriceVersionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FacilityId = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FacilityPriceVersion", x => x.FacilityPriceVersionId);
                    table.ForeignKey(
                        name: "FK_FacilityPriceVersion_Facility_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facility",
                        principalColumn: "FacilityId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FacilityPriceVersion_User_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FacilityUnitPrice",
                columns: table => new
                {
                    FacilityUnitPriceId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FacilityPriceVersionId = table.Column<int>(type: "int", nullable: false),
                    UnitTypeId = table.Column<int>(type: "int", nullable: false),
                    BasePricePerMonth = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    Discount3Month = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    Discount6Month = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    Discount12Month = table.Column<decimal>(type: "decimal(5,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FacilityUnitPrice", x => x.FacilityUnitPriceId);
                    table.ForeignKey(
                        name: "FK_FacilityUnitPrice_FacilityPriceVersion_FacilityPriceVersionId",
                        column: x => x.FacilityPriceVersionId,
                        principalTable: "FacilityPriceVersion",
                        principalColumn: "FacilityPriceVersionId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FacilityUnitPrice_UnitType_UnitTypeId",
                        column: x => x.UnitTypeId,
                        principalTable: "UnitType",
                        principalColumn: "UnitTypeId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UnitPrice_PricingPolicyId_UnitTypeId",
                table: "UnitPrice",
                columns: new[] { "PricingPolicyId", "UnitTypeId" });

            migrationBuilder.CreateIndex(
                name: "IX_Reservation_FacilityPriceVersionId",
                table: "Reservation",
                column: "FacilityPriceVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_FacilityPriceVersion_CreatedBy",
                table: "FacilityPriceVersion",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_FacilityPriceVersion_FacilityId_Version",
                table: "FacilityPriceVersion",
                columns: new[] { "FacilityId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FacilityUnitPrice_FacilityPriceVersionId_UnitTypeId",
                table: "FacilityUnitPrice",
                columns: new[] { "FacilityPriceVersionId", "UnitTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FacilityUnitPrice_UnitTypeId",
                table: "FacilityUnitPrice",
                column: "UnitTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Reservation_FacilityPriceVersion_FacilityPriceVersionId",
                table: "Reservation",
                column: "FacilityPriceVersionId",
                principalTable: "FacilityPriceVersion",
                principalColumn: "FacilityPriceVersionId",
                onDelete: ReferentialAction.Restrict);

            // Per-size facility prices of the live company version become each facility's own version 1, live from today.
            migrationBuilder.Sql(@"
INSERT INTO FacilityPriceVersion (FacilityId, Version, EffectiveFrom, EffectiveTo, Status, CreatedBy, CreatedAt)
SELECT up.FacilityId, 1, CAST(GETDATE() AS date), NULL, 'Active', MIN(p.CreatedBy), SYSUTCDATETIME()
FROM UnitPrice up JOIN PricingPolicy p ON p.PricingPolicyId = up.PricingPolicyId
WHERE up.FacilityId IS NOT NULL AND p.Status = 'Active'
GROUP BY up.FacilityId;

INSERT INTO FacilityUnitPrice (FacilityPriceVersionId, UnitTypeId, BasePricePerMonth, Discount3Month, Discount6Month, Discount12Month)
SELECT v.FacilityPriceVersionId, up.UnitTypeId, up.BasePricePerMonth, up.Discount3Month, up.Discount6Month, up.Discount12Month
FROM UnitPrice up
JOIN PricingPolicy p ON p.PricingPolicyId = up.PricingPolicyId
JOIN FacilityPriceVersion v ON v.FacilityId = up.FacilityId AND v.Version = 1
WHERE up.FacilityId IS NOT NULL AND p.Status = 'Active';

DELETE FROM UnitPrice WHERE FacilityId IS NOT NULL;");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Reservation_FacilityPriceVersion_FacilityPriceVersionId",
                table: "Reservation");

            migrationBuilder.DropTable(
                name: "FacilityUnitPrice");

            migrationBuilder.DropTable(
                name: "FacilityPriceVersion");

            migrationBuilder.DropIndex(
                name: "IX_UnitPrice_PricingPolicyId_UnitTypeId",
                table: "UnitPrice");

            migrationBuilder.DropIndex(
                name: "IX_Reservation_FacilityPriceVersionId",
                table: "Reservation");

            migrationBuilder.DropColumn(
                name: "FacilityPriceVersionId",
                table: "Reservation");

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
    }
}
