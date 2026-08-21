using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EuroTrade.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxPublishedAtIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_PublishedAt_CreatedAt",
                table: "outbox_messages",
                columns: new[] { "PublishedAt", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_outbox_messages_PublishedAt_CreatedAt",
                table: "outbox_messages");
        }
    }
}
