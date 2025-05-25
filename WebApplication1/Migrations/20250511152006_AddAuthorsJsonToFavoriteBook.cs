using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication1.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthorsJsonToFavoriteBook : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Authors",
                table: "FavoriteBooks");

            migrationBuilder.AddColumn<string>(
                name: "AuthorsJson",
                table: "FavoriteBooks",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AuthorsJson",
                table: "FavoriteBooks");

            migrationBuilder.AddColumn<List<string>>(
                name: "Authors",
                table: "FavoriteBooks",
                type: "jsonb",
                nullable: false);
        }
    }
}
