using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuizApp.Server.Persistence.Migrations
{
    [DbContext(typeof(QuizAppDbContext))]
    [Migration("20260402120000_AddQuizStartPermissionLock")]
    public partial class AddQuizStartPermissionLock : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsStartAllowedForEveryone",
                table: "Quizzes",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsStartAllowedForEveryone",
                table: "Quizzes");
        }
    }
}