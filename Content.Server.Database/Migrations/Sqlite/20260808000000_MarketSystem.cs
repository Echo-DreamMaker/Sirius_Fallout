using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
{
    /// <summary>
    /// #Cythisiax Add - Free market tables and multi-currency columns on character_currency.
    /// </summary>
    public partial class MarketSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Add multi-currency columns to character_currency ──────────────

            migrationBuilder.AddColumn<int>(
                name: "ncr_dollars",
                table: "character_currency",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "silver",
                table: "character_currency",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "gold",
                table: "character_currency",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // ── market_listing ───────────────────────────────────────────────

            migrationBuilder.CreateTable(
                name: "market_listing",
                columns: table => new
                {
                    market_listing_id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    listing_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    seller_player_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    seller_character_name = table.Column<string>(type: "TEXT", nullable: false),
                    prototype_id = table.Column<string>(type: "TEXT", nullable: false),
                    quantity = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                    stack_count = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    currency = table.Column<string>(type: "TEXT", nullable: false),
                    price_per_unit = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    requested_item_id = table.Column<string>(type: "TEXT", nullable: true),
                    requested_quantity = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    listed_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    expires_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    status = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "Active"),
                    sold_to_character = table.Column<string>(type: "TEXT", nullable: true),
                    sold_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    sold_item_tag = table.Column<string>(type: "TEXT", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_market_listing", x => x.market_listing_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_market_listing_listing_id",
                table: "market_listing",
                column: "listing_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_market_listing_seller_player_id_status",
                table: "market_listing",
                columns: new[] { "seller_player_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_market_listing_expires_at",
                table: "market_listing",
                column: "expires_at");

            // ── market_price_history ─────────────────────────────────────────

            migrationBuilder.CreateTable(
                name: "market_price_history",
                columns: table => new
                {
                    market_price_history_id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    prototype_id = table.Column<string>(type: "TEXT", nullable: false),
                    timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    reference_price = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    supply = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    demand = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_market_price_history", x => x.market_price_history_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_market_price_history_prototype_id_timestamp",
                table: "market_price_history",
                columns: new[] { "prototype_id", "timestamp" });

            // ── market_sale ──────────────────────────────────────────────────

            migrationBuilder.CreateTable(
                name: "market_sale",
                columns: table => new
                {
                    market_sale_id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    listing_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    item_proto = table.Column<string>(type: "TEXT", nullable: false),
                    price = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    currency = table.Column<string>(type: "TEXT", nullable: false),
                    seller_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    seller_name = table.Column<string>(type: "TEXT", nullable: false),
                    buyer_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    buyer_name = table.Column<string>(type: "TEXT", nullable: false),
                    sold_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_market_sale", x => x.market_sale_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_market_sale_sold_at",
                table: "market_sale",
                column: "sold_at");

            // ── market_sold_item ─────────────────────────────────────────────

            migrationBuilder.CreateTable(
                name: "market_sold_item",
                columns: table => new
                {
                    sold_tag = table.Column<string>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_market_sold_item", x => x.sold_tag);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "market_sold_item");
            migrationBuilder.DropTable(name: "market_sale");
            migrationBuilder.DropTable(name: "market_price_history");
            migrationBuilder.DropTable(name: "market_listing");

            migrationBuilder.DropColumn(name: "gold", table: "character_currency");
            migrationBuilder.DropColumn(name: "silver", table: "character_currency");
            migrationBuilder.DropColumn(name: "ncr_dollars", table: "character_currency");
        }
    }
}
