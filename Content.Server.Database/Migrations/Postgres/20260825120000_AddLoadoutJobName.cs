using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddLoadoutJobName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // #Sirius Add: per-job loadout storage.
            migrationBuilder.DropIndex(
                name: "IX_loadout_profile_id_loadout_name",
                table: "loadout");

            migrationBuilder.AddColumn<string>(
                name: "job_name",
                table: "loadout",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_loadout_profile_id_job_name_loadout_name",
                table: "loadout",
                columns: new[] { "profile_id", "job_name", "loadout_name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_loadout_profile_id_job_name_loadout_name",
                table: "loadout");

            migrationBuilder.DropColumn(
                name: "job_name",
                table: "loadout");

            migrationBuilder.CreateIndex(
                name: "IX_loadout_profile_id_loadout_name",
                table: "loadout",
                columns: new[] { "profile_id", "loadout_name" },
                unique: true);
        }
    }
}
