using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EuroTrade.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxTraceContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TraceParent",
                table: "outbox_messages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TraceState",
                table: "outbox_messages",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TraceParent",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "TraceState",
                table: "outbox_messages");
        }
    }
}
