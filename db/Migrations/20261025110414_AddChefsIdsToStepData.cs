using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Probate.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddChefsIdsToStepData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "chefs_form_id",
                table: "step_data",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "chefs_submission_id",
                table: "step_data",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "chefs_form_id",
                table: "step_data");

            migrationBuilder.DropColumn(
                name: "chefs_submission_id",
                table: "step_data");
        }
    }
}
