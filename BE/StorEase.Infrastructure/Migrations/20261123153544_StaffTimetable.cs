using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StorEase.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class StaffTimetable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Timetable",
                table: "StaffFacility",
                type: "varchar(200)",
                unicode: false,
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "TimetableThrough",
                table: "StaffFacility",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "FromTimetable",
                table: "Shift",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Timetable",
                table: "StaffFacility");

            migrationBuilder.DropColumn(
                name: "TimetableThrough",
                table: "StaffFacility");

            migrationBuilder.DropColumn(
                name: "FromTimetable",
                table: "Shift");
        }
    }
}
