using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
{
    /// <summary>
    /// #Cythisiax Added - Patreon supporter tier column to gate supporter-exclusive features.
    /// </summary>
    public partial class SupporterTier : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "tier",
                table: "supporter",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "tier",
                table: "supporter");
        }
    }
}
