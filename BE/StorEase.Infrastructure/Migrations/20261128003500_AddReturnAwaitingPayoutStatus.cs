using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StorEase.Infrastructure.Persistence;

#nullable disable

namespace StorEase.Infrastructure.Migrations
{
    /// <summary>
    /// Adds ReturnStatus.AwaitingPayout ('AwaitingPayout', 14 chars) to the string-converted UnitReturn.Status
    /// column (varchar(20)). Existing rows in 'Completed' with a Pending or missing DepositRefund are left
    /// untouched for business review using <see cref="ExistingDataCleanup.ListCompletedReturnsWithoutPaidRefund"/>.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261128003500_AddReturnAwaitingPayoutStatus")]
    public partial class AddReturnAwaitingPayoutStatus : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // UnitReturn.Status is already varchar(20) NOT NULL with HasConversion<string>(), which fits
            // 'AwaitingPayout' (14 characters). Also move any return settled under the old code (Completed
            // while its DepositRefund is still Pending) to AwaitingPayout.
            migrationBuilder.Sql("""
                ALTER TABLE [UnitReturn] ALTER COLUMN [Status] varchar(20) NOT NULL;
                UPDATE ur
                SET ur.[Status] = 'AwaitingPayout'
                FROM [UnitReturn] ur
                INNER JOIN [DepositRefund] dr ON dr.[UnitReturnId] = ur.[UnitReturnId]
                WHERE ur.[Status] = 'Completed' AND dr.[Status] = 'Pending';
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE [UnitReturn] ALTER COLUMN [Status] varchar(20) NOT NULL;
                """);
        }
    }
}
