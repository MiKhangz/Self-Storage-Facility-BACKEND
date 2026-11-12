using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StorEase.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReturnMoveOutDeadline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AccessLockedAt",
                table: "UnitReturn",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "MoveOutDeadline",
                table: "UnitReturn",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            // Requests made before the rule get the same 15 days, counted from when they were made.
            migrationBuilder.Sql("UPDATE [UnitReturn] SET [MoveOutDeadline] = DATEADD(day, 15, CAST([RequestedAt] AS date));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccessLockedAt",
                table: "UnitReturn");

            migrationBuilder.DropColumn(
                name: "MoveOutDeadline",
                table: "UnitReturn");
        }
    }
}
