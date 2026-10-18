using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StorEase.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUnitVisit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UnitVisit",
                columns: table => new
                {
                    UnitVisitId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContractId = table.Column<int>(type: "int", nullable: false),
                    StorageUnitId = table.Column<int>(type: "int", nullable: false),
                    RecordedBy = table.Column<int>(type: "int", nullable: false),
                    CheckInAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CheckOutAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitVisit", x => x.UnitVisitId);
                    table.ForeignKey(
                        name: "FK_UnitVisit_Contract_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contract",
                        principalColumn: "ContractId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UnitVisit_StorageUnit_StorageUnitId",
                        column: x => x.StorageUnitId,
                        principalTable: "StorageUnit",
                        principalColumn: "StorageUnitId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UnitVisit_User_RecordedBy",
                        column: x => x.RecordedBy,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UnitVisit_CheckInAt",
                table: "UnitVisit",
                column: "CheckInAt");

            migrationBuilder.CreateIndex(
                name: "IX_UnitVisit_ContractId",
                table: "UnitVisit",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitVisit_RecordedBy",
                table: "UnitVisit",
                column: "RecordedBy");

            migrationBuilder.CreateIndex(
                name: "IX_UnitVisit_StorageUnitId",
                table: "UnitVisit",
                column: "StorageUnitId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UnitVisit");
        }
    }
}
