-- Hardening after moderation rollout:
-- - status default → pending_review
-- - image_id unique only among live rows
-- - ad_no sequence (replace MAX+1 races)
-- Applied by startup migrator after 10_moderation.sql.

-- ---------------------------------------------------------------------------
-- listings.status default must match product rule (review before publish)
-- ---------------------------------------------------------------------------
ALTER TABLE listings
    ALTER COLUMN status SET DEFAULT 'pending_review';

-- ---------------------------------------------------------------------------
-- Soft-deleted images may retain image_id for purge; unique only when live
-- ---------------------------------------------------------------------------
DROP INDEX IF EXISTS ux_listing_images_image_id;

CREATE UNIQUE INDEX IF NOT EXISTS ux_listing_images_image_id
    ON listing_images (image_id)
    WHERE image_id IS NOT NULL AND deleted_at IS NULL;

-- ---------------------------------------------------------------------------
-- ad_no allocation via sequence
-- ---------------------------------------------------------------------------
CREATE SEQUENCE IF NOT EXISTS listing_ad_no_seq;

SELECT setval(
    'listing_ad_no_seq',
    GREATEST(
        COALESCE(
            (SELECT MAX(CAST(substring(ad_no FROM 4) AS BIGINT))
             FROM listings
             WHERE ad_no ~ '^AP-[0-9]+$'),
            100000
        ),
        100000
    ),
    true
);

-- ---------------------------------------------------------------------------
-- Rejection reason length (defense in depth; app also caps at 1000)
-- ---------------------------------------------------------------------------
ALTER TABLE listings
    DROP CONSTRAINT IF EXISTS listings_rejection_reason_len;

ALTER TABLE listings
    ADD CONSTRAINT listings_rejection_reason_len
        CHECK (rejection_reason IS NULL OR char_length(rejection_reason) <= 1000);
