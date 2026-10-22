using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StorEase.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDepositRefund : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DepositRefund",
                columns: table => new
                {
                    DepositRefundId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    FacilityId = table.Column<int>(type: "int", nullable: false),
                    ReservationId = table.Column<int>(type: "int", nullable: false),
                    HandoverId = table.Column<int>(type: "int", nullable: true),
                    Source = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Method = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    RefundAccount = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PaidBy = table.Column<int>(type: "int", nullable: true),
                    ReferenceNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepositRefund", x => x.DepositRefundId);
                    table.ForeignKey(
                        name: "FK_DepositRefund_Facility_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facility",
                        principalColumn: "FacilityId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DepositRefund_Handover_HandoverId",
                        column: x => x.HandoverId,
                        principalTable: "Handover",
                        principalColumn: "HandoverId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DepositRefund_Reservation_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "Reservation",
                        principalColumn: "ReservationId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DepositRefund_User_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DepositRefund_User_PaidBy",
                        column: x => x.PaidBy,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DepositRefund_CustomerId",
                table: "DepositRefund",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_DepositRefund_FacilityId",
                table: "DepositRefund",
                column: "FacilityId");

            migrationBuilder.CreateIndex(
                name: "IX_DepositRefund_HandoverId",
                table: "DepositRefund",
                column: "HandoverId",
                unique: true,
                filter: "[HandoverId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DepositRefund_PaidBy",
                table: "DepositRefund",
                column: "PaidBy");

            migrationBuilder.CreateIndex(
                name: "IX_DepositRefund_ReservationId_Source",
                table: "DepositRefund",
                columns: new[] { "ReservationId", "Source" },
                unique: true,
                filter: "[Source] = 'Cancellation'");

            migrationBuilder.CreateIndex(
                name: "IX_DepositRefund_Status",
                table: "DepositRefund",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DepositRefund");
        }
    }
}
