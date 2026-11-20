using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StorEase.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExaminerFixes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Contract_StorageUnitId",
                table: "Contract");

            migrationBuilder.AddColumn<DateTime>(
                name: "ProposedAt",
                table: "Reservation",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProposedStorageUnitId",
                table: "Reservation",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TransferStorageUnitId",
                table: "Renewal",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EndReminderDays",
                table: "PricingPolicy",
                type: "int",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.AddColumn<int>(
                name: "MoveOutAfterDays",
                table: "PricingPolicy",
                type: "int",
                nullable: false,
                defaultValue: 7);

            migrationBuilder.AddColumn<decimal>(
                name: "OverstayFeePercent",
                table: "PricingPolicy",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 150m);

            migrationBuilder.AddColumn<string>(
                name: "EndContactNote",
                table: "Contract",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EndReminderAt",
                table: "Contract",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MovedToCentralStorageAt",
                table: "Contract",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reservation_ProposedStorageUnitId",
                table: "Reservation",
                column: "ProposedStorageUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_Renewal_TransferStorageUnitId",
                table: "Renewal",
                column: "TransferStorageUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_Contract_StorageUnitId",
                table: "Contract",
                column: "StorageUnitId",
                unique: true,
                filter: "[Status] IN ('Active', 'Draft', 'Overstay')");

            migrationBuilder.AddForeignKey(
                name: "FK_Renewal_StorageUnit_TransferStorageUnitId",
                table: "Renewal",
                column: "TransferStorageUnitId",
                principalTable: "StorageUnit",
                principalColumn: "StorageUnitId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Reservation_StorageUnit_ProposedStorageUnitId",
                table: "Reservation",
                column: "ProposedStorageUnitId",
                principalTable: "StorageUnit",
                principalColumn: "StorageUnitId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Renewal_StorageUnit_TransferStorageUnitId",
                table: "Renewal");

            migrationBuilder.DropForeignKey(
                name: "FK_Reservation_StorageUnit_ProposedStorageUnitId",
                table: "Reservation");

            migrationBuilder.DropIndex(
                name: "IX_Reservation_ProposedStorageUnitId",
                table: "Reservation");

            migrationBuilder.DropIndex(
                name: "IX_Renewal_TransferStorageUnitId",
                table: "Renewal");

            migrationBuilder.DropIndex(
                name: "IX_Contract_StorageUnitId",
                table: "Contract");

            migrationBuilder.DropColumn(
                name: "ProposedAt",
                table: "Reservation");

            migrationBuilder.DropColumn(
                name: "ProposedStorageUnitId",
                table: "Reservation");

            migrationBuilder.DropColumn(
                name: "TransferStorageUnitId",
                table: "Renewal");

            migrationBuilder.DropColumn(
                name: "EndReminderDays",
                table: "PricingPolicy");

            migrationBuilder.DropColumn(
                name: "MoveOutAfterDays",
                table: "PricingPolicy");

            migrationBuilder.DropColumn(
                name: "OverstayFeePercent",
                table: "PricingPolicy");

            migrationBuilder.DropColumn(
                name: "EndContactNote",
                table: "Contract");

            migrationBuilder.DropColumn(
                name: "EndReminderAt",
                table: "Contract");

            migrationBuilder.DropColumn(
                name: "MovedToCentralStorageAt",
                table: "Contract");

            migrationBuilder.CreateIndex(
                name: "IX_Contract_StorageUnitId",
                table: "Contract",
                column: "StorageUnitId",
                unique: true,
                filter: "[Status] IN ('Active', 'Draft')");
        }
    }
}
