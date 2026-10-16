using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VpnMonitor.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHeartbeatLastError : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "last_error",
                schema: "public",
                table: "monitor-heartbeat",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_error_at",
                schema: "public",
                table: "monitor-heartbeat",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "last_error",
                schema: "public",
                table: "monitor-heartbeat");

            migrationBuilder.DropColumn(
                name: "last_error_at",
                schema: "public",
                table: "monitor-heartbeat");
        }
    }
}
