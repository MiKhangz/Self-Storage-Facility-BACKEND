using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StorEase.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedTicketCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Customers pick one of these to open a support ticket; with none, nobody could ask for help.
            // Only into an empty table, so categories an operator already set up are left alone.
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM [TicketCategory])
                INSERT INTO [TicketCategory] ([Name], [DefaultPriority]) VALUES
                    (N'Ra vào / khoá kho', 'High'),
                    (N'Hư hỏng / sửa chữa', 'Normal'),
                    (N'Vệ sinh / côn trùng', 'Normal'),
                    (N'Thanh toán / hoá đơn', 'Normal'),
                    (N'Khác', 'Low');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM [TicketCategory]
                WHERE [Name] IN (N'Ra vào / khoá kho', N'Hư hỏng / sửa chữa', N'Vệ sinh / côn trùng', N'Thanh toán / hoá đơn', N'Khác')
                  AND [CategoryId] NOT IN (SELECT [CategoryId] FROM [SupportTicket]);
                """);
        }
    }
}
