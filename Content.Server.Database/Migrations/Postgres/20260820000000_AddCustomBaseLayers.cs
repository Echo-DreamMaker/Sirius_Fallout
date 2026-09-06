using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres
{
    /// <summary>
    /// #Cythisiax Added - Persist roundstart prosthetic choices (CustomBaseLayers) on the profile table so they
    /// survive round/server restarts. Stored as a jsonb list of "{layer}@{id}@{colorHex}" strings, mirroring Markings.
    /// </summary>
    public partial class AddCustomBaseLayers : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<JsonDocument>(
                name: "custom_base_layers",
                table: "profile",
                type: "jsonb",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "custom_base_layers",
                table: "profile");
        }
    }
}
