using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bulky.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddIsFrontCoverToProductImage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsFrontCover",
                table: "ProductImages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            //backfill existing data: mark the first-uploaded image of each product as the front cover,
            //so products that already had images (before this feature) get a sensible front cover too.
            migrationBuilder.Sql(@"
                UPDATE ""ProductImages""
                SET ""IsFrontCover"" = true
                WHERE ""Id"" IN (
                    SELECT DISTINCT ON (""ProductId"") ""Id""
                    FROM ""ProductImages""
                    ORDER BY ""ProductId"", ""Id""
                );");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsFrontCover",
                table: "ProductImages");
        }
    }
}
