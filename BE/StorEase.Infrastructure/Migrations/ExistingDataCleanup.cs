namespace StorEase.Infrastructure.Migrations;

/// <summary>
/// Data fixes that must run before a migration adds a unique index, so databases that already hold
/// rows violating the new rule can still upgrade. Plain portable SQL (no T-SQL-only syntax) so the
/// same text is exercised by the SQLite tests.
/// </summary>
public static class ExistingDataCleanup
{
    /// <summary>
    /// Keeps one open (Active/Draft) contract per unit and terminates the rest. The Active contract
    /// wins over a Draft; between equals the newest ContractId wins. Terminated rows keep their history.
    /// </summary>
    public const string TerminateExtraOpenContracts = @"
UPDATE [Contract] SET [Status] = 'Terminated'
WHERE [ContractId] IN (
    SELECT [ContractId] FROM (
        SELECT [ContractId],
               ROW_NUMBER() OVER (
                   PARTITION BY [StorageUnitId]
                   ORDER BY CASE WHEN [Status] = 'Active' THEN 0 ELSE 1 END, [ContractId] DESC) AS rn
        FROM [Contract]
        WHERE [Status] IN ('Active', 'Draft')
    ) ranked
    WHERE rn > 1)";

    /// <summary>Removes repeated (UserId, FacilityId) assignment rows, keeping the oldest one.</summary>
    public const string DeduplicateStaffFacility = @"
DELETE FROM [StaffFacility]
WHERE [StaffFacilityId] NOT IN (
    SELECT MIN([StaffFacilityId]) FROM [StaffFacility] GROUP BY [UserId], [FacilityId])";

    /// <summary>
    /// Diagnostic query listing existing returns in Completed whose DepositRefund is missing or not Paid,
    /// so business operations can review them without altering historical records automatically.
    /// </summary>
    public const string ListCompletedReturnsWithoutPaidRefund = @"
SELECT
    ur.[UnitReturnId],
    ur.[ContractId],
    c.[ContractNumber],
    c.[CustomerId],
    u.[FullName] AS [CustomerName],
    su.[UnitCode],
    ur.[PlannedReturnDate],
    ur.[Status] AS [ReturnStatus],
    ri.[RefundAmount] AS [InspectedRefundAmount],
    dr.[DepositRefundId],
    dr.[Amount] AS [PayoutAmount],
    dr.[Status] AS [DepositRefundStatus]
FROM [UnitReturn] ur
INNER JOIN [Contract] c ON c.[ContractId] = ur.[ContractId]
INNER JOIN [User] u ON u.[UserId] = c.[CustomerId]
INNER JOIN [StorageUnit] su ON su.[StorageUnitId] = c.[StorageUnitId]
LEFT JOIN [ReturnInspection] ri ON ri.[UnitReturnId] = ur.[UnitReturnId]
LEFT JOIN [DepositRefund] dr ON dr.[UnitReturnId] = ur.[UnitReturnId]
WHERE ur.[Status] = 'Completed'
  AND (dr.[DepositRefundId] IS NULL OR dr.[Status] <> 'Paid')
ORDER BY ur.[UnitReturnId] DESC";
}
