using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StorEase.Infrastructure.Persistence;

#nullable disable

namespace StorEase.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20261126100000_AddUnitAssignmentModeAndTicketLink")]
    public partial class AddUnitAssignmentModeAndTicketLink : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('Reservation', 'SameDay') IS NULL
                    ALTER TABLE [Reservation] ADD [SameDay] bit NOT NULL DEFAULT 0;
                IF COL_LENGTH('Reservation', 'UnitAssignmentMode') IS NULL
                    ALTER TABLE [Reservation] ADD [UnitAssignmentMode] varchar(20) NOT NULL DEFAULT 'AUTO';
                IF COL_LENGTH('Reservation', 'Preferences') IS NULL
                    ALTER TABLE [Reservation] ADD [Preferences] nvarchar(500) NULL;
                IF COL_LENGTH('Reservation', 'PreferenceNote') IS NULL
                    ALTER TABLE [Reservation] ADD [PreferenceNote] nvarchar(500) NULL;
                ALTER TABLE [Reservation] ALTER COLUMN [Status] varchar(30) NOT NULL;
                IF COL_LENGTH('SupportTicket', 'ReservationId') IS NULL
                BEGIN
                    ALTER TABLE [SupportTicket] ADD [ReservationId] int NULL;
                    CREATE INDEX [IX_SupportTicket_ReservationId] ON [SupportTicket] ([ReservationId]);
                    ALTER TABLE [SupportTicket] ADD CONSTRAINT [FK_SupportTicket_Reservation_ReservationId]
                        FOREIGN KEY ([ReservationId]) REFERENCES [Reservation] ([ReservationId]) ON DELETE NO ACTION;
                END
                IF COL_LENGTH('SupportTicket', 'FlaggedForReview') IS NULL
                    ALTER TABLE [SupportTicket] ADD [FlaggedForReview] bit NOT NULL DEFAULT 0;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "FlaggedForReview", table: "SupportTicket");
            migrationBuilder.DropForeignKey(name: "FK_SupportTicket_Reservation_ReservationId", table: "SupportTicket");
            migrationBuilder.DropIndex(name: "IX_SupportTicket_ReservationId", table: "SupportTicket");
            migrationBuilder.DropColumn(name: "ReservationId", table: "SupportTicket");
            migrationBuilder.DropColumn(name: "PreferenceNote", table: "Reservation");
            migrationBuilder.DropColumn(name: "Preferences", table: "Reservation");
            migrationBuilder.DropColumn(name: "UnitAssignmentMode", table: "Reservation");
        }
    }
}
