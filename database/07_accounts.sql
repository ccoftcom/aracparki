-- Accounts + auth tokens + ownership links (runs after marketplace seed)
-- Fresh installs only need this file; there is no separate migrations/ folder.

CREATE TABLE IF NOT EXISTS accounts (
    id                  BIGSERIAL PRIMARY KEY,
    email               TEXT NOT NULL,
    password_hash       TEXT NOT NULL,
    first_name          TEXT NOT NULL,
    last_name           TEXT NOT NULL,
    phone               TEXT NULL,
    email_confirmed_at  TIMESTAMPTZ NULL,
    security_stamp      TEXT NOT NULL DEFAULT gen_random_uuid()::text,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_accounts_email_lower
    ON accounts (lower(email));

DROP TRIGGER IF EXISTS trg_accounts_updated_at ON accounts;
CREATE TRIGGER trg_accounts_updated_at
    BEFORE UPDATE ON accounts
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();

CREATE TABLE IF NOT EXISTS password_reset_tokens (
    id           BIGSERIAL PRIMARY KEY,
    account_id   BIGINT NOT NULL REFERENCES accounts (id) ON DELETE CASCADE,
    token_hash   TEXT NOT NULL UNIQUE,
    expires_at   TIMESTAMPTZ NOT NULL,
    used_at      TIMESTAMPTZ NULL,
    created_at   TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_password_reset_account
    ON password_reset_tokens (account_id, expires_at DESC);

CREATE TABLE IF NOT EXISTS email_verification_tokens (
    id           BIGSERIAL PRIMARY KEY,
    account_id   BIGINT NOT NULL REFERENCES accounts (id) ON DELETE CASCADE,
    token_hash   TEXT NOT NULL UNIQUE,
    expires_at   TIMESTAMPTZ NOT NULL,
    used_at      TIMESTAMPTZ NULL,
    created_at   TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_email_verification_account
    ON email_verification_tokens (account_id, expires_at DESC);

-- Demo sellers stay unlinked (account_id NULL); real sellers bind here.
ALTER TABLE sellers
    ADD COLUMN IF NOT EXISTS account_id BIGINT;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'sellers_account_id_fkey'
    ) THEN
        ALTER TABLE sellers
            ADD CONSTRAINT sellers_account_id_fkey
            FOREIGN KEY (account_id) REFERENCES accounts (id) ON DELETE SET NULL;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'sellers_account_id_key'
    ) THEN
        ALTER TABLE sellers
            ADD CONSTRAINT sellers_account_id_key UNIQUE (account_id);
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_sellers_account
    ON sellers (account_id)
    WHERE account_id IS NOT NULL;

-- Owned saved searches (created after accounts so FK is inline)
DROP TABLE IF EXISTS saved_searches;

CREATE TABLE saved_searches (
    id           BIGSERIAL PRIMARY KEY,
    account_id   BIGINT NOT NULL REFERENCES accounts (id) ON DELETE CASCADE,
    name         TEXT NOT NULL,
    query_json   JSONB NOT NULL CHECK (jsonb_typeof(query_json) = 'object'),
    created_at   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at   TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_saved_searches_account
    ON saved_searches (account_id, created_at DESC);

DROP TRIGGER IF EXISTS trg_saved_searches_updated_at ON saved_searches;
CREATE TRIGGER trg_saved_searches_updated_at
    BEFORE UPDATE ON saved_searches
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();
