using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StorEase.Infrastructure.Migrations
{
    /// <summary>
    /// SR-03: giving the unit back is now a handover. MoveOutRequest -> Handover, ReturnInspection ->
    /// HandoverInspection, contract status PendingMoveOut -> PendingHandover. Renames only, so rows survive.
    /// Must run after RenameHandoverToCheckIn, which freed the Handover name.
    /// </summary>
    public partial class RenameMoveOutToHandover : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            Rename(migrationBuilder, request: ("MoveOutRequest", "Handover"), inspection: ("ReturnInspection", "HandoverInspection"),
                plannedDate: ("PlannedMoveOutDate", "PlannedHandoverDate"));
            migrationBuilder.Sql("UPDATE [Contract] SET [Status] = 'PendingHandover' WHERE [Status] = 'PendingMoveOut';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE [Contract] SET [Status] = 'PendingMoveOut' WHERE [Status] = 'PendingHandover';");
            Rename(migrationBuilder, request: ("Handover", "MoveOutRequest"), inspection: ("HandoverInspection", "ReturnInspection"),
                plannedDate: ("PlannedHandoverDate", "PlannedMoveOutDate"));
        }

        private static void Rename(MigrationBuilder mb, (string From, string To) request, (string From, string To) inspection,
            (string From, string To) plannedDate)
        {
            var (rf, rt) = request;
            var (inf, int_) = inspection;

            // SQL Server keeps constraint and index names on a table rename; give them the names EF now expects.
            mb.RenameIndex(name: $"IX_{rf}_ContractId", table: rf, newName: $"IX_{rt}_ContractId");
            mb.RenameIndex(name: $"IX_{rf}_Status", table: rf, newName: $"IX_{rt}_Status");
            mb.RenameIndex(name: $"IX_{inf}_{rf}Id", table: inf, newName: $"IX_{int_}_{rt}Id");
            foreach (var column in new[] { "StaffId", "Status", "StorageUnitId" })
                mb.RenameIndex(name: $"IX_{inf}_{column}", table: inf, newName: $"IX_{int_}_{column}");
            mb.Sql($"EXEC sp_rename N'[FK_{rf}_Contract_ContractId]', N'FK_{rt}_Contract_ContractId', N'OBJECT';");
            mb.Sql($"EXEC sp_rename N'[FK_{inf}_{rf}_{rf}Id]', N'FK_{int_}_{rt}_{rt}Id', N'OBJECT';");
            mb.Sql($"EXEC sp_rename N'[FK_{inf}_StorageUnit_StorageUnitId]', N'FK_{int_}_StorageUnit_StorageUnitId', N'OBJECT';");
            mb.Sql($"EXEC sp_rename N'[FK_{inf}_User_StaffId]', N'FK_{int_}_User_StaffId', N'OBJECT';");
            mb.Sql($"EXEC sp_rename N'[PK_{rf}]', N'PK_{rt}', N'OBJECT';");
            mb.Sql($"EXEC sp_rename N'[PK_{inf}]', N'PK_{int_}', N'OBJECT';");

            mb.RenameColumn(name: $"{rf}Id", table: inf, newName: $"{rt}Id");
            mb.RenameColumn(name: $"{inf}Id", table: inf, newName: $"{int_}Id");
            mb.RenameColumn(name: $"{rf}Id", table: rf, newName: $"{rt}Id");
            mb.RenameColumn(name: plannedDate.From, table: rf, newName: plannedDate.To);
            mb.RenameTable(name: inf, newName: int_);
            mb.RenameTable(name: rf, newName: rt);
        }
    }
}
