using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KYAUCPChapter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTestCaseIsSample : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSample",
                table: "TestCases",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsSample",
                table: "TestCases");
        }
    }
}
