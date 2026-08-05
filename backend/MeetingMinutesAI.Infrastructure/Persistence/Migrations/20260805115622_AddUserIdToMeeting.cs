using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeetingMinutesAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserIdToMeeting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "Meetings",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Meetings");
        }
    }
}
