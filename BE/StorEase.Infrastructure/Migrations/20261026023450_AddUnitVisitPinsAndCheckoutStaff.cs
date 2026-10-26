using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StorEase.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUnitVisitPinsAndCheckoutStaff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CheckedOutBy",
                table: "UnitVisit",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UnitVisitPin",
                columns: table => new
                {
                    UnitVisitPinId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ContractId = table.Column<int>(type: "int", nullable: false),
                    UnitVisitId = table.Column<int>(type: "int", nullable: true),
                    Purpose = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    CodeHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsUsed = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitVisitPin", x => x.UnitVisitPinId);
                    table.ForeignKey(
                        name: "FK_UnitVisitPin_Contract_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contract",
                        principalColumn: "ContractId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UnitVisitPin_UnitVisit_UnitVisitId",
                        column: x => x.UnitVisitId,
                        principalTable: "UnitVisit",
                        principalColumn: "UnitVisitId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UnitVisitPin_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UnitVisit_CheckedOutBy",
                table: "UnitVisit",
                column: "CheckedOutBy");

            migrationBuilder.CreateIndex(
                name: "IX_UnitVisitPin_CodeHash_Purpose_IsUsed",
                table: "UnitVisitPin",
                columns: new[] { "CodeHash", "Purpose", "IsUsed" });

            migrationBuilder.CreateIndex(
                name: "IX_UnitVisitPin_ContractId",
                table: "UnitVisitPin",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitVisitPin_UnitVisitId",
                table: "UnitVisitPin",
                column: "UnitVisitId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitVisitPin_UserId_ExpiredAt",
                table: "UnitVisitPin",
                columns: new[] { "UserId", "ExpiredAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_UnitVisit_User_CheckedOutBy",
                table: "UnitVisit",
                column: "CheckedOutBy",
                principalTable: "User",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UnitVisit_User_CheckedOutBy",
                table: "UnitVisit");

            migrationBuilder.DropTable(
                name: "UnitVisitPin");

            migrationBuilder.DropIndex(
                name: "IX_UnitVisit_CheckedOutBy",
                table: "UnitVisit");

            migrationBuilder.DropColumn(
                name: "CheckedOutBy",
                table: "UnitVisit");
        }
    }
}
