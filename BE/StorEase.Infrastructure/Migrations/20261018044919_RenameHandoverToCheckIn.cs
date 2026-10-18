using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StorEase.Infrastructure.Migrations
{
    /// <summary>
    /// SR-01: the start of a rental is now a check-in. Renames only, so existing rows survive; the name
    /// "Handover" is freed for the end of the rental (RenameMoveOutToHandover runs next).
    /// </summary>
    public partial class RenameHandoverToCheckIn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            Rename(migrationBuilder, from: "Handover", to: "CheckIn");
            migrationBuilder.Sql("UPDATE [StaffTask] SET [TaskType] = 'CheckIn' WHERE [TaskType] = 'Handover';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE [StaffTask] SET [TaskType] = 'Handover' WHERE [TaskType] = 'CheckIn';");
            Rename(migrationBuilder, from: "CheckIn", to: "Handover");
        }

        private static void Rename(MigrationBuilder mb, string from, string to)
        {
            string photoFrom = from + "Photo", photoTo = to + "Photo";

            // SQL Server keeps constraint and index names on a table rename; give them the names EF now expects.
            foreach (var column in new[] { "ContractId", "ReservationId", "StaffId", "Status", "StorageUnitId" })
                mb.RenameIndex(name: $"IX_{from}_{column}", table: from, newName: $"IX_{to}_{column}");
            mb.RenameIndex(name: $"IX_{photoFrom}_{from}Id", table: photoFrom, newName: $"IX_{photoTo}_{to}Id");
            mb.RenameIndex(name: $"IX_{photoFrom}_UploadedBy", table: photoFrom, newName: $"IX_{photoTo}_UploadedBy");
            foreach (var name in new[] { "Contract_ContractId", "Reservation_ReservationId", "StorageUnit_StorageUnitId", "User_StaffId" })
                mb.Sql($"EXEC sp_rename N'[FK_{from}_{name}]', N'FK_{to}_{name}', N'OBJECT';");
            mb.Sql($"EXEC sp_rename N'[FK_{photoFrom}_User_UploadedBy]', N'FK_{photoTo}_User_UploadedBy', N'OBJECT';");
            mb.Sql($"EXEC sp_rename N'[FK_{photoFrom}_{from}_{from}Id]', N'FK_{photoTo}_{to}_{to}Id', N'OBJECT';");
            mb.Sql($"EXEC sp_rename N'[PK_{from}]', N'PK_{to}', N'OBJECT';");
            mb.Sql($"EXEC sp_rename N'[PK_{photoFrom}]', N'PK_{photoTo}', N'OBJECT';");

            mb.RenameColumn(name: $"{from}Id", table: photoFrom, newName: $"{to}Id");
            mb.RenameColumn(name: $"{photoFrom}Id", table: photoFrom, newName: $"{photoTo}Id");
            mb.RenameColumn(name: $"{from}Id", table: from, newName: $"{to}Id");
            mb.RenameTable(name: photoFrom, newName: photoTo);
            mb.RenameTable(name: from, newName: to);
        }
    }
}
