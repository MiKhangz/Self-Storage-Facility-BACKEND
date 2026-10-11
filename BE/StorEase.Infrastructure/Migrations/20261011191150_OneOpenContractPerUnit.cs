using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StorEase.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OneOpenContractPerUnit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Contract_StorageUnitId",
                table: "Contract");

            migrationBuilder.CreateIndex(
                name: "IX_Contract_StorageUnitId",
                table: "Contract",
                column: "StorageUnitId",
                unique: true,
                filter: "[Status] IN ('Active', 'Draft')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Contract_StorageUnitId",
                table: "Contract");

            migrationBuilder.CreateIndex(
                name: "IX_Contract_StorageUnitId",
                table: "Contract",
                column: "StorageUnitId");
        }
    }
}
