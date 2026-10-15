using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VpnMonitor.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMonitorHeartbeat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "monitor-heartbeat",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    last_poll = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    active_clients = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_monitor-heartbeat", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "monitor-heartbeat",
                schema: "public");
        }
    }
}
