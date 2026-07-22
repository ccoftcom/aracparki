-- OTP attempt lockout + search index hardening
-- Applied by startup migrator after 12_corporate.sql.

-- ---------------------------------------------------------------------------
-- phone_otp_tokens: failed verify attempts (lock after MaxAttempts in app)
-- ---------------------------------------------------------------------------
ALTER TABLE phone_otp_tokens
    ADD COLUMN IF NOT EXISTS attempt_count INT NOT NULL DEFAULT 0
        CHECK (attempt_count >= 0);

-- ---------------------------------------------------------------------------
-- Search: brands.name trigram for ILIKE '%q%' in listing search
-- ---------------------------------------------------------------------------
CREATE INDEX IF NOT EXISTS ix_brands_name_trgm
    ON brands USING GIN (name gin_trgm_ops);

-- Sort helpers for price/hours OFFSET pagination (status-scoped)
CREATE INDEX IF NOT EXISTS ix_listings_status_price
    ON listings (status, price ASC, id ASC);

CREATE INDEX IF NOT EXISTS ix_listings_status_hours
    ON listings (status, hours ASC NULLS LAST, id ASC);
