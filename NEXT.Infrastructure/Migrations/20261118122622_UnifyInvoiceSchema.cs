using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXT.Infrastructure.Migrations
{
    public partial class UnifyInvoiceSchema : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Payments_PurchaseInvoices_PurchaseInvoiceId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_PurchaseInvoiceId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "PurchaseInvoiceId",
                table: "Payments");

            migrationBuilder.DropTable(name: "PurchaseInvoiceItems");
            migrationBuilder.DropTable(name: "PurchaseInvoices");

            migrationBuilder.RenameTable(name: "SalesInvoices", newName: "Invoices");
            migrationBuilder.RenameTable(name: "SalesInvoiceItems", newName: "InvoiceItems");
            migrationBuilder.RenameColumn(name: "SalesInvoiceId", table: "Payments", newName: "InvoiceId");
            migrationBuilder.RenameColumn(name: "InvoiceType", table: "Invoices", newName: "LegacyInvoiceTypeCode");

            migrationBuilder.AlterColumn<int>(
                name: "LegacyInvoiceTypeCode",
                table: "Invoices",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            // All rows from the former sales table belong to the sales side, including historical returns.
            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "Invoices",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                ALTER TABLE "Invoices" ALTER COLUMN "Type" DROP DEFAULT;
                UPDATE "StockMovements" SET "ReferenceType" = 'Invoice' WHERE "ReferenceType" = 'SalesInvoice';
                """);

            migrationBuilder.AddColumn<string>(
                name: "CreatedByFullName",
                table: "Invoices",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerName",
                table: "Invoices",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DiscountType",
                table: "Invoices",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountValue",
                table: "Invoices",
                type: "numeric(15,2)",
                precision: 15,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            RenameInvoiceKeysAndIndexes(migrationBuilder, upgrading: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "Invoices" WHERE "Type" <> 0) THEN
                        RAISE EXCEPTION 'Cannot roll back unified invoices while non-sales invoices exist.';
                    END IF;
                END $$;
                UPDATE "Invoices" SET "LegacyInvoiceTypeCode" = 0 WHERE "LegacyInvoiceTypeCode" IS NULL;
                UPDATE "StockMovements" SET "ReferenceType" = 'SalesInvoice' WHERE "ReferenceType" = 'Invoice';
                """);

            migrationBuilder.DropColumn(
                name: "CreatedByFullName",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "CustomerName",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "DiscountType",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "DiscountValue",
                table: "Invoices");

            RenameInvoiceKeysAndIndexes(migrationBuilder, upgrading: false);

            migrationBuilder.DropColumn(name: "Type", table: "Invoices");
            migrationBuilder.AlterColumn<int>(
                name: "LegacyInvoiceTypeCode",
                table: "Invoices",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.RenameColumn(name: "LegacyInvoiceTypeCode", table: "Invoices", newName: "InvoiceType");
            migrationBuilder.RenameColumn(name: "InvoiceId", table: "Payments", newName: "SalesInvoiceId");
            migrationBuilder.RenameTable(name: "InvoiceItems", newName: "SalesInvoiceItems");
            migrationBuilder.RenameTable(name: "Invoices", newName: "SalesInvoices");

            // This restores the old purchase schema. Dropped rows must be restored from backup.
            migrationBuilder.AddColumn<Guid>(
                name: "PurchaseInvoiceId",
                table: "Payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PurchaseInvoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    DiscountAmount = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    InvoiceDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    PaidAmount = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    PaymentStatus = table.Column<int>(type: "integer", nullable: false),
                    RemainingAmount = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SubTotal = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseInvoices_PurchaseOrders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PurchaseInvoices_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseInvoices_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseInvoiceItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductBatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseInvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    DiscountAmount = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(15,3)", precision: 15, scale: 3, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    TaxRate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseInvoiceItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseInvoiceItems_ProductBatches_ProductBatchId",
                        column: x => x.ProductBatchId,
                        principalTable: "ProductBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PurchaseInvoiceItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseInvoiceItems_PurchaseInvoices_PurchaseInvoiceId",
                        column: x => x.PurchaseInvoiceId,
                        principalTable: "PurchaseInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_PurchaseInvoiceId",
                table: "Payments",
                column: "PurchaseInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceItems_ProductBatchId",
                table: "PurchaseInvoiceItems",
                column: "ProductBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceItems_ProductId",
                table: "PurchaseInvoiceItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceItems_PurchaseInvoiceId",
                table: "PurchaseInvoiceItems",
                column: "PurchaseInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoices_InvoiceNumber",
                table: "PurchaseInvoices",
                column: "InvoiceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoices_PurchaseOrderId",
                table: "PurchaseInvoices",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoices_SupplierId",
                table: "PurchaseInvoices",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoices_WarehouseId",
                table: "PurchaseInvoices",
                column: "WarehouseId");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_PurchaseInvoices_PurchaseInvoiceId",
                table: "Payments",
                column: "PurchaseInvoiceId",
                principalTable: "PurchaseInvoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        private static void RenameInvoiceKeysAndIndexes(MigrationBuilder migrationBuilder, bool upgrading)
        {
            var (from, to) = upgrading ? ("SalesInvoices", "Invoices") : ("Invoices", "SalesInvoices");
            var (fromItems, toItems) = upgrading
                ? ("SalesInvoiceItems", "InvoiceItems") : ("InvoiceItems", "SalesInvoiceItems");
            var (fromPaymentKey, toPaymentKey) = upgrading ? ("SalesInvoiceId", "InvoiceId") : ("InvoiceId", "SalesInvoiceId");

            // Both calls run on the unified table names. PostgreSQL updates foreign-key targets on RenameTable.
            migrationBuilder.Sql($"""
                ALTER TABLE "Invoices" RENAME CONSTRAINT "PK_{from}" TO "PK_{to}";
                ALTER TABLE "Invoices" RENAME CONSTRAINT "FK_{from}_Customers_CustomerId" TO "FK_{to}_Customers_CustomerId";
                ALTER TABLE "Invoices" RENAME CONSTRAINT "FK_{from}_{from}_ParentInvoiceId" TO "FK_{to}_{to}_ParentInvoiceId";
                ALTER TABLE "Invoices" RENAME CONSTRAINT "FK_{from}_Users_UserId" TO "FK_{to}_Users_UserId";
                ALTER TABLE "Invoices" RENAME CONSTRAINT "FK_{from}_Warehouses_WarehouseId" TO "FK_{to}_Warehouses_WarehouseId";
                ALTER TABLE "InvoiceItems" RENAME CONSTRAINT "PK_{fromItems}" TO "PK_{toItems}";
                ALTER TABLE "InvoiceItems" RENAME CONSTRAINT "FK_{fromItems}_{from}_InvoiceId" TO "FK_{toItems}_{to}_InvoiceId";
                ALTER TABLE "InvoiceItems" RENAME CONSTRAINT "FK_{fromItems}_ProductBatches_ProductBatchId" TO "FK_{toItems}_ProductBatches_ProductBatchId";
                ALTER TABLE "InvoiceItems" RENAME CONSTRAINT "FK_{fromItems}_Products_ProductId" TO "FK_{toItems}_Products_ProductId";
                ALTER TABLE "Payments" RENAME CONSTRAINT "FK_Payments_{from}_{fromPaymentKey}" TO "FK_Payments_{to}_{toPaymentKey}";
                """);

            foreach (var column in new[] { "CustomerId", "InvoiceNumber", "ParentInvoiceId", "UserId", "WarehouseId" })
            {
                migrationBuilder.RenameIndex(name: $"IX_{from}_{column}", table: "Invoices", newName: $"IX_{to}_{column}");
            }
            foreach (var column in new[] { "InvoiceId", "ProductBatchId", "ProductId" })
            {
                migrationBuilder.RenameIndex(name: $"IX_{fromItems}_{column}", table: "InvoiceItems", newName: $"IX_{toItems}_{column}");
            }
            migrationBuilder.RenameIndex(name: $"IX_Payments_{fromPaymentKey}", table: "Payments", newName: $"IX_Payments_{toPaymentKey}");
        }
    }
}
