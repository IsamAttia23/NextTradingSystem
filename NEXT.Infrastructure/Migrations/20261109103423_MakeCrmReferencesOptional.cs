using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXT.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MakeCrmReferencesOptional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Customers_Tags_TagId",
                table: "Customers");

            migrationBuilder.DropForeignKey(
                name: "FK_Suppliers_Tags_TagId",
                table: "Suppliers");

            migrationBuilder.AlterColumn<Guid>(
                name: "TagId",
                table: "Suppliers",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "TagId",
                table: "Customers",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            // Preserve legacy contacts without inventing phone numbers; edits now require a phone.
            migrationBuilder.Sql("UPDATE \"CustomerContacts\" SET \"Phone\" = '' WHERE \"Phone\" IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "CustomerContacts",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "CustomerId",
                table: "CustomerContacts",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_Tags_TagId",
                table: "Customers",
                column: "TagId",
                principalTable: "Tags",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_Tags_TagId",
                table: "Suppliers",
                column: "TagId",
                principalTable: "Tags",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Do not replace optional relationships with invalid all-zero foreign keys on rollback.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "Customers" WHERE "TagId" IS NULL)
                        OR EXISTS (SELECT 1 FROM "Suppliers" WHERE "TagId" IS NULL)
                        OR EXISTS (SELECT 1 FROM "CustomerContacts" WHERE "CustomerId" IS NULL) THEN
                        RAISE EXCEPTION 'Assign tags and customers before rolling back optional CRM references.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_Customers_Tags_TagId",
                table: "Customers");

            migrationBuilder.DropForeignKey(
                name: "FK_Suppliers_Tags_TagId",
                table: "Suppliers");

            migrationBuilder.AlterColumn<Guid>(
                name: "TagId",
                table: "Suppliers",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TagId",
                table: "Customers",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "CustomerContacts",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<Guid>(
                name: "CustomerId",
                table: "CustomerContacts",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_Tags_TagId",
                table: "Customers",
                column: "TagId",
                principalTable: "Tags",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_Tags_TagId",
                table: "Suppliers",
                column: "TagId",
                principalTable: "Tags",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
