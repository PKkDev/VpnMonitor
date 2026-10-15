using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace VpnMonitor.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTrafficStatistics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Text to number needs an explicit USING, which AlterColumn does not generate
            migrationBuilder.Sql("""
                ALTER TABLE public."vpn-client"
                    ALTER COLUMN bytes_sent TYPE bigint USING bytes_sent::bigint,
                    ALTER COLUMN bytes_received TYPE bigint USING bytes_received::bigint;
                """);

            migrationBuilder.AddColumn<string>(
                name: "server_title",
                schema: "public",
                table: "monitor-heartbeat",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "client-traffic-hourly",
                schema: "public",
                columns: table => new
                {
                    common_name = table.Column<string>(type: "text", nullable: false),
                    hour = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    bytes_received = table.Column<long>(type: "bigint", nullable: false),
                    bytes_sent = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client-traffic-hourly", x => new { x.common_name, x.hour });
                });

            migrationBuilder.CreateTable(
                name: "monitor-poll",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    polled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    active_clients = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_monitor-poll", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_vpn-client_inserted",
                schema: "public",
                table: "vpn-client",
                column: "inserted");

            migrationBuilder.CreateIndex(
                name: "IX_monitor-poll_polled_at",
                schema: "public",
                table: "monitor-poll",
                column: "polled_at");

            // Hourly traffic from the snapshots collected so far: the delta between consecutive snapshots of a session,
            // the first snapshot of a session counts as a whole since counters start from zero on connect
            migrationBuilder.Sql("""
                INSERT INTO public."client-traffic-hourly" (common_name, hour, bytes_received, bytes_sent)
                SELECT common_name, hour, sum(received), sum(sent)
                FROM (
                    SELECT
                        common_name,
                        date_trunc('hour', inserted AT TIME ZONE 'UTC') AT TIME ZONE 'UTC' AS hour,
                        CASE WHEN prev_received IS NULL OR bytes_received < prev_received
                            THEN bytes_received ELSE bytes_received - prev_received END AS received,
                        CASE WHEN prev_sent IS NULL OR bytes_sent < prev_sent
                            THEN bytes_sent ELSE bytes_sent - prev_sent END AS sent
                    FROM (
                        SELECT
                            common_name, inserted, bytes_received, bytes_sent,
                            lag(bytes_received) OVER w AS prev_received,
                            lag(bytes_sent) OVER w AS prev_sent
                        FROM public."vpn-client"
                        WINDOW w AS (PARTITION BY common_name, connected_since ORDER BY inserted)
                    ) AS snapshots
                ) AS deltas
                GROUP BY common_name, hour
                HAVING sum(received) > 0 OR sum(sent) > 0;
                """);

            // Poll history from the snapshots: polls without clients were not recorded before
            migrationBuilder.Sql("""
                INSERT INTO public."monitor-poll" (polled_at, active_clients)
                SELECT inserted, count(*)
                FROM public."vpn-client"
                GROUP BY inserted;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "client-traffic-hourly",
                schema: "public");

            migrationBuilder.DropTable(
                name: "monitor-poll",
                schema: "public");

            migrationBuilder.DropIndex(
                name: "IX_vpn-client_inserted",
                schema: "public",
                table: "vpn-client");

            migrationBuilder.DropColumn(
                name: "server_title",
                schema: "public",
                table: "monitor-heartbeat");

            migrationBuilder.AlterColumn<string>(
                name: "bytes_sent",
                schema: "public",
                table: "vpn-client",
                type: "text",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<string>(
                name: "bytes_received",
                schema: "public",
                table: "vpn-client",
                type: "text",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");
        }
    }
}
