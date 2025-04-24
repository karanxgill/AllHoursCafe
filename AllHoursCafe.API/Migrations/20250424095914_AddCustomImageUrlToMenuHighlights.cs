using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AllHoursCafe.API.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomImageUrlToMenuHighlights : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomImageUrl",
                table: "MenuHighlights",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomImageUrl",
                table: "MenuHighlights");
        }
    }
}
