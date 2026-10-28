-- Apply to an existing PostgreSQL database before enabling inventory writes.
-- No data is deleted. Duplicate operation keys must be reviewed if index creation fails.
CREATE UNIQUE INDEX IF NOT EXISTS "IX_StockMovements_ReferenceId_Type"
ON "StockMovements" ("ReferenceId", "Type")
WHERE "ReferenceType" IN ('Receipt', 'Transfer');

CREATE UNIQUE INDEX IF NOT EXISTS "IX_Warehouses_IsDefault"
ON "Warehouses" ("IsDefault") WHERE "IsDefault" = TRUE AND "DeletedAt" IS NULL;
