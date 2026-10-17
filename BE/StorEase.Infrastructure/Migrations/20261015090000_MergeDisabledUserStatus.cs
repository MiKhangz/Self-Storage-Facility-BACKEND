using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StorEase.Infrastructure.Persistence;

#nullable disable

namespace StorEase.Infrastructure.Migrations
{
    /// <summary>Suspended and Inactive behaved the same (no sign-in), so they are now one status: Disabled. Data only.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261015090000_MergeDisabledUserStatus")]
    public partial class MergeDisabledUserStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql("UPDATE [User] SET [Status] = 'Disabled' WHERE [Status] IN ('Suspended', 'Inactive');");

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql("UPDATE [User] SET [Status] = 'Inactive' WHERE [Status] = 'Disabled';");
    }
}
