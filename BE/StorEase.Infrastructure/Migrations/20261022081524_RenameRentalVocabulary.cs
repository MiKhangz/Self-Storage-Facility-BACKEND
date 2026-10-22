using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StorEase.Infrastructure.Migrations
{
    /// <summary>
    /// The instructor's vocabulary: the start of a rental is a Handover, a visit in and out is a Check-in, and giving
    /// the unit back is a Return. Handover -> UnitReturn, HandoverInspection -> ReturnInspection, then CheckIn -> Handover,
    /// CheckInPhoto -> HandoverPhoto, then UnitVisit -> CheckIn. Renames only, so rows survive; each step frees the name
    /// the next one takes.
    /// </summary>
    public partial class RenameRentalVocabulary : Migration
    {
        private record Table(string From, string To, (string From, string To)[] Columns, string[] Indexes,
            (string PrincipalFrom, string PrincipalTo, string Column)[] Fks);

        private static readonly Table[][] Steps =
        [
            [
                new("Handover", "UnitReturn", [("HandoverId", "UnitReturnId"), ("PlannedHandoverDate", "PlannedReturnDate")],
                    ["ContractId", "Status"], [("Contract", "Contract", "ContractId")]),
                new("HandoverInspection", "ReturnInspection", [("HandoverInspectionId", "ReturnInspectionId"), ("HandoverId", "UnitReturnId")],
                    ["HandoverId", "StaffId", "Status", "StorageUnitId"],
                    [("Handover", "UnitReturn", "HandoverId"), ("StorageUnit", "StorageUnit", "StorageUnitId"), ("User", "User", "StaffId")]),
                new("DepositRefund", "DepositRefund", [("HandoverId", "UnitReturnId")], [], [("Handover", "UnitReturn", "HandoverId")]),
            ],
            [
                new("CheckIn", "Handover", [("CheckInId", "HandoverId"), ("CheckInTime", "HandoverTime")],
                    ["ContractId", "ReservationId", "StaffId", "Status", "StorageUnitId"],
                    [("Contract", "Contract", "ContractId"), ("Reservation", "Reservation", "ReservationId"),
                     ("StorageUnit", "StorageUnit", "StorageUnitId"), ("User", "User", "StaffId")]),
                new("CheckInPhoto", "HandoverPhoto", [("CheckInPhotoId", "HandoverPhotoId"), ("CheckInId", "HandoverId")],
                    ["CheckInId", "UploadedBy"], [("CheckIn", "Handover", "CheckInId"), ("User", "User", "UploadedBy")]),
                new("Reservation", "Reservation", [("CheckInSlot", "HandoverSlot")], [], []),
            ],
            [
                new("UnitVisit", "CheckIn", [("UnitVisitId", "CheckInId")], ["CheckInAt", "ContractId", "RecordedBy", "StorageUnitId"],
                    [("Contract", "Contract", "ContractId"), ("StorageUnit", "StorageUnit", "StorageUnitId"), ("User", "User", "RecordedBy")]),
            ],
        ];

        private static readonly (string Table, string Column, string From, string To)[] Values =
        [
            ("Contract", "Status", "PendingHandover", "PendingReturn"),
            ("Reservation", "Status", "CheckedIn", "HandedOver"),
            ("DepositRefund", "Source", "Handover", "Return"),
            ("StaffTask", "TaskType", "CheckIn", "Handover"),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The filter names the column, so the filtered index is rebuilt rather than renamed.
            migrationBuilder.DropIndex(name: "IX_DepositRefund_HandoverId", table: "DepositRefund");
            foreach (var step in Steps)
                foreach (var table in step)
                    Rename(migrationBuilder, table);
            migrationBuilder.CreateIndex(name: "IX_DepositRefund_UnitReturnId", table: "DepositRefund", column: "UnitReturnId",
                unique: true, filter: "[UnitReturnId] IS NOT NULL");
            foreach (var (table, column, from, to) in Values)
                migrationBuilder.Sql($"UPDATE [{table}] SET [{column}] = '{to}' WHERE [{column}] = '{from}';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (table, column, from, to) in Values)
                migrationBuilder.Sql($"UPDATE [{table}] SET [{column}] = '{from}' WHERE [{column}] = '{to}';");
            migrationBuilder.DropIndex(name: "IX_DepositRefund_UnitReturnId", table: "DepositRefund");
            foreach (var step in Enumerable.Reverse(Steps))
                foreach (var table in step)
                    Rename(migrationBuilder, Reversed(table));
            migrationBuilder.CreateIndex(name: "IX_DepositRefund_HandoverId", table: "DepositRefund", column: "HandoverId",
                unique: true, filter: "[HandoverId] IS NOT NULL");
        }

        private static Table Reversed(Table t)
        {
            string Column(string c) => t.Columns.FirstOrDefault(x => x.From == c).To ?? c;
            return new(t.To, t.From, t.Columns.Select(c => (c.To, c.From)).ToArray(), t.Indexes.Select(Column).ToArray(),
                t.Fks.Select(f => (f.PrincipalTo, f.PrincipalFrom, Column(f.Column))).ToArray());
        }

        private static void Rename(MigrationBuilder mb, Table t)
        {
            string Column(string c) => t.Columns.FirstOrDefault(x => x.From == c).To ?? c;

            // SQL Server keeps constraint and index names on a table rename; give them the names EF now expects.
            foreach (var column in t.Indexes)
                mb.RenameIndex(name: $"IX_{t.From}_{column}", table: t.From, newName: $"IX_{t.To}_{Column(column)}");
            foreach (var (principalFrom, principalTo, column) in t.Fks)
                mb.Sql($"EXEC sp_rename N'[FK_{t.From}_{principalFrom}_{column}]', N'FK_{t.To}_{principalTo}_{Column(column)}', N'OBJECT';");
            if (t.From != t.To)
                mb.Sql($"EXEC sp_rename N'[PK_{t.From}]', N'PK_{t.To}', N'OBJECT';");
            foreach (var (from, to) in t.Columns)
                mb.RenameColumn(name: from, table: t.From, newName: to);
            if (t.From != t.To)
                mb.RenameTable(name: t.From, newName: t.To);
        }
    }
}
