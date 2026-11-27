using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Probate.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatedByUserIdToSubmissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "created_by",
                table: "submissions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100
            );

            migrationBuilder.AddColumn<string>(
                name: "created_by_user_id",
                table: "submissions",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_submissions_created_by_user_id",
                table: "submissions",
                column: "created_by_user_id"
            );
        }

        /// <summary>
        /// Forward-only: dropping owner IDs would orphan new rows with null CreatedBy
        /// and re-enable username fallback on already-claimed rows. Restoring NOT NULL
        /// by inventing usernames would lose ownership data. Roll forward instead.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new System.NotSupportedException(
                "Submission owner-ID migration cannot be safely reversed. "
                    + "Keep both owner columns and roll forward; do not invent legacy usernames."
            );
        }
    }
}
