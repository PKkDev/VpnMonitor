using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VpnMonitor.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCommonNameInsertedIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_vpn-client_common_name_inserted",
                schema: "public",
                table: "vpn-client",
                columns: new[] { "common_name", "inserted" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_vpn-client_common_name_inserted",
                schema: "public",
                table: "vpn-client");
        }
    }
}
