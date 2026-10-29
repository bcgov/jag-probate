using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Probate.Db.Migrations
{
    /// <inheritdoc />
    public partial class DropChefsSubmissionIdFromSubmissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "chefs_submission_id",
                table: "submissions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "chefs_submission_id",
                table: "submissions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");
        }
    }
}
