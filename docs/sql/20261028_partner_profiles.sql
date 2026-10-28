-- Additive upgrade for existing PostgreSQL databases. Existing profile images remain empty.
ALTER TABLE "Customers" ADD COLUMN IF NOT EXISTS "AvatarUrl" bytea NULL;
ALTER TABLE "Suppliers" ADD COLUMN IF NOT EXISTS "AvatarUrl" bytea NULL;
