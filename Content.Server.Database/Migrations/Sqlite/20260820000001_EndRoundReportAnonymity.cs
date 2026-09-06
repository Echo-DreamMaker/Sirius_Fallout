using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
{
    /// <summary>
    /// #Cythisiax Added - Account-level toggle to hide a player's characters from other players in the
    /// end-of-round report (round end anonymity).
    /// </summary>
    public partial class EndRoundReportAnonymity : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "anonymous_round_end_report",
                table: "preference",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "anonymous_round_end_report",
                table: "preference");
        }
    }
}
