using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXT.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductPackaging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PackagingUnitId",
                table: "Products",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UnitsPerPackage",
                table: "Products",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_PackagingUnitId",
                table: "Products",
                column: "PackagingUnitId");

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Units_PackagingUnitId",
                table: "Products",
                column: "PackagingUnitId",
                principalTable: "Units",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_Units_PackagingUnitId",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_PackagingUnitId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "PackagingUnitId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "UnitsPerPackage",
                table: "Products");
        }
    }
}
