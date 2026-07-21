-- Media pipeline: enriched listing_images, watermark templates, upload sessions.
-- Safe to re-run (IF NOT EXISTS / ADD COLUMN IF NOT EXISTS).

-- ---------------------------------------------------------------------------
-- Watermark templates (central config; logo bytes live in R2)
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS watermark_templates (
    id              BIGSERIAL PRIMARY KEY,
    code            TEXT NOT NULL UNIQUE,
    logo_r2_key     TEXT NOT NULL,
    opacity         NUMERIC(3, 2) NOT NULL DEFAULT 0.35
                    CHECK (opacity > 0 AND opacity <= 1),
    anchor          TEXT NOT NULL DEFAULT 'bottom-right'
                    CHECK (anchor IN (
                        'top-left', 'top-right', 'bottom-left', 'bottom-right', 'center'
                    )),
    scale_pct       NUMERIC(5, 2) NOT NULL DEFAULT 18
                    CHECK (scale_pct > 0 AND scale_pct <= 100),
    margin_px       INT NOT NULL DEFAULT 24 CHECK (margin_px >= 0),
    apply_to_variants TEXT[] NOT NULL DEFAULT ARRAY['lg', 'xl', 'og']::text[],
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

INSERT INTO watermark_templates (code, logo_r2_key, opacity, anchor, scale_pct, margin_px, apply_to_variants)
VALUES (
    'default',
    'assets/watermarks/default.png',
    0.14,
    'center',
    58,
    0,
    ARRAY['card', 'md', 'lg', 'xl', 'og']::text[]
)
ON CONFLICT (code) DO UPDATE SET
    opacity = EXCLUDED.opacity,
    anchor = EXCLUDED.anchor,
    scale_pct = EXCLUDED.scale_pct,
    margin_px = EXCLUDED.margin_px,
    apply_to_variants = EXCLUDED.apply_to_variants,
    updated_at = NOW();

-- ---------------------------------------------------------------------------
-- listing_images enrichment (url remains the primary delivery URL)
-- ---------------------------------------------------------------------------
ALTER TABLE listing_images
    ADD COLUMN IF NOT EXISTS image_id            TEXT,
    ADD COLUMN IF NOT EXISTS storage_key         TEXT,
    ADD COLUMN IF NOT EXISTS original_filename   TEXT,
    ADD COLUMN IF NOT EXISTS version             INT NOT NULL DEFAULT 1,
    ADD COLUMN IF NOT EXISTS width               INT,
    ADD COLUMN IF NOT EXISTS height              INT,
    ADD COLUMN IF NOT EXISTS byte_size           BIGINT,
    ADD COLUMN IF NOT EXISTS mime_type           TEXT,
    ADD COLUMN IF NOT EXISTS checksum_sha256     TEXT,
    ADD COLUMN IF NOT EXISTS is_cover            BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS status              TEXT NOT NULL DEFAULT 'ready',
    ADD COLUMN IF NOT EXISTS watermark_template_id BIGINT REFERENCES watermark_templates (id),
    ADD COLUMN IF NOT EXISTS created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    ADD COLUMN IF NOT EXISTS updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    ADD COLUMN IF NOT EXISTS deleted_at          TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS purge_after         TIMESTAMPTZ;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'listing_images_status_check'
    ) THEN
        ALTER TABLE listing_images
            ADD CONSTRAINT listing_images_status_check
            CHECK (status IN (
                'pending_upload', 'validating', 'processing', 'ready',
                'replacing', 'soft_deleted', 'purging', 'hard_deleted', 'failed'
            ));
    END IF;
END $$;

-- Backfill cover: lowest sort_order per listing
UPDATE listing_images li
SET is_cover = TRUE
WHERE li.deleted_at IS NULL
  AND li.is_cover = FALSE
  AND li.id = (
      SELECT li2.id
      FROM listing_images li2
      WHERE li2.listing_id = li.listing_id
        AND li2.deleted_at IS NULL
      ORDER BY li2.sort_order, li2.id
      LIMIT 1
  );

CREATE UNIQUE INDEX IF NOT EXISTS ux_listing_images_one_cover
    ON listing_images (listing_id)
    WHERE is_cover AND deleted_at IS NULL;

CREATE UNIQUE INDEX IF NOT EXISTS ux_listing_images_image_id
    ON listing_images (image_id)
    WHERE image_id IS NOT NULL AND deleted_at IS NULL;

CREATE INDEX IF NOT EXISTS ix_listing_images_listing_sort
    ON listing_images (listing_id, sort_order, id)
    WHERE deleted_at IS NULL;

CREATE INDEX IF NOT EXISTS ix_listing_images_purge
    ON listing_images (purge_after)
    WHERE status IN ('soft_deleted', 'purging') AND purge_after IS NOT NULL;

-- ---------------------------------------------------------------------------
-- Account-scoped upload sessions (wizard / direct-to-R2 intent)
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS media_upload_sessions (
    id                TEXT PRIMARY KEY,
    account_id        BIGINT NOT NULL REFERENCES accounts (id) ON DELETE CASCADE,
    listing_id        BIGINT REFERENCES listings (id) ON DELETE SET NULL,
    storage_key_tmp   TEXT NOT NULL,
    content_type      TEXT NOT NULL,
    max_bytes         BIGINT NOT NULL,
    status            TEXT NOT NULL DEFAULT 'pending_upload'
                      CHECK (status IN (
                          'pending_upload', 'validating', 'processing',
                          'ready', 'failed', 'expired'
                      )),
    idempotency_key   TEXT,
    failure_code      TEXT,
    result_image_id   TEXT,
    result_storage_key TEXT,
    result_delivery_url TEXT,
    width             INT,
    height            INT,
    byte_size         BIGINT,
    mime_type         TEXT,
    checksum_sha256   TEXT,
    expires_at        TIMESTAMPTZ NOT NULL,
    created_at        TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at        TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    completed_at      TIMESTAMPTZ
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_media_upload_sessions_idempotency
    ON media_upload_sessions (account_id, idempotency_key)
    WHERE idempotency_key IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_media_upload_sessions_account
    ON media_upload_sessions (account_id, created_at DESC);

CREATE INDEX IF NOT EXISTS ix_media_upload_sessions_expires
    ON media_upload_sessions (expires_at)
    WHERE status = 'pending_upload';

-- Soft-delete filter for existing GetImages queries
-- (application should filter deleted_at; keep url column for CDN delivery)
