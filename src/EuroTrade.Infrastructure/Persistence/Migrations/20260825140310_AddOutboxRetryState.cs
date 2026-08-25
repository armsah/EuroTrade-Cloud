using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EuroTrade.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxRetryState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_outbox_messages_PublishedAt_CreatedAt",
                table: "outbox_messages");

            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "outbox_messages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FailedAt",
                table: "outbox_messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastAttemptAt",
                table: "outbox_messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextAttemptAt",
                table: "outbox_messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_PublishedAt_FailedAt_NextAttemptAt_CreatedAt",
                table: "outbox_messages",
                columns: new[] { "PublishedAt", "FailedAt", "NextAttemptAt", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_outbox_messages_PublishedAt_FailedAt_NextAttemptAt_CreatedAt",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "FailedAt",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "LastAttemptAt",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "NextAttemptAt",
                table: "outbox_messages");

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_PublishedAt_CreatedAt",
                table: "outbox_messages",
                columns: new[] { "PublishedAt", "CreatedAt" });
        }
    }
}
