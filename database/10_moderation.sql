-- Roles + listing moderation workflow (pending_review / rejected).
-- Applied by startup migrator after 09_currency.sql.

-- ---------------------------------------------------------------------------
-- accounts.role
-- ---------------------------------------------------------------------------
ALTER TABLE accounts
    ADD COLUMN IF NOT EXISTS role TEXT NOT NULL DEFAULT 'user';

ALTER TABLE accounts
    DROP CONSTRAINT IF EXISTS accounts_role_check;

ALTER TABLE accounts
    ADD CONSTRAINT accounts_role_check
        CHECK (role IN ('user', 'admin'));

CREATE INDEX IF NOT EXISTS ix_accounts_role
    ON accounts (role)
    WHERE role = 'admin';

-- Promote staff via one-off ops SQL (never hard-code emails in migrations):
--   UPDATE accounts SET role = 'admin' WHERE lower(email) = 'you@example.com';

-- ---------------------------------------------------------------------------
-- listings moderation columns + expanded status
-- ---------------------------------------------------------------------------
ALTER TABLE listings
    ADD COLUMN IF NOT EXISTS rejection_reason TEXT NULL;

ALTER TABLE listings
    ADD COLUMN IF NOT EXISTS reviewed_at TIMESTAMPTZ NULL;

ALTER TABLE listings
    ADD COLUMN IF NOT EXISTS reviewed_by_account_id BIGINT NULL;

ALTER TABLE listings
    ADD COLUMN IF NOT EXISTS submitted_at TIMESTAMPTZ NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'listings_reviewed_by_account_id_fkey'
    ) THEN
        ALTER TABLE listings
            ADD CONSTRAINT listings_reviewed_by_account_id_fkey
            FOREIGN KEY (reviewed_by_account_id) REFERENCES accounts (id) ON DELETE SET NULL;
    END IF;
END $$;

-- Drop old status check (name may vary); recreate with moderation states.
ALTER TABLE listings
    DROP CONSTRAINT IF EXISTS listings_status_check;

-- Also drop anonymous CHECK if PostgreSQL named it differently — find by definition.
DO $$
DECLARE
    cname TEXT;
BEGIN
    SELECT con.conname INTO cname
    FROM pg_constraint con
    JOIN pg_class rel ON rel.oid = con.conrelid
    JOIN pg_namespace nsp ON nsp.oid = rel.relnamespace
    WHERE nsp.nspname = 'public'
      AND rel.relname = 'listings'
      AND con.contype = 'c'
      AND pg_get_constraintdef(con.oid) ILIKE '%status%draft%published%archived%'
      AND con.conname <> 'listings_status_check';
    IF cname IS NOT NULL THEN
        EXECUTE format('ALTER TABLE listings DROP CONSTRAINT %I', cname);
    END IF;
END $$;

ALTER TABLE listings
    ADD CONSTRAINT listings_status_check
        CHECK (status IN ('draft', 'pending_review', 'published', 'rejected', 'archived'));

CREATE INDEX IF NOT EXISTS ix_listings_status_submitted_at
    ON listings (status, submitted_at DESC NULLS LAST, id DESC);
