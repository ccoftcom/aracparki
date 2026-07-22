-- SEO: city + corporate public slugs for path hubs and /satici/{slug}.

ALTER TABLE cities
    ADD COLUMN IF NOT EXISTS slug TEXT;

UPDATE cities
SET slug = trim(both '-' FROM regexp_replace(
        lower(
            translate(
                name,
                'İIıŞşĞğÜüÖöÇçÂâÊêÎîÔôÛû',
                'IiisSgGuUoOcCAaEeIiOoUu'
            )
        ),
        '[^a-z0-9]+',
        '-',
        'g'
    ))
WHERE slug IS NULL OR slug = '';

-- Ensure uniqueness if two names collide after slugify
UPDATE cities c
SET slug = c.slug || '-' || c.id::text
WHERE c.id IN (
    SELECT id
    FROM (
        SELECT id,
               ROW_NUMBER() OVER (PARTITION BY slug ORDER BY id) AS rn
        FROM cities
        WHERE slug IS NOT NULL
    ) d
    WHERE d.rn > 1
);

ALTER TABLE cities
    ALTER COLUMN slug SET NOT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS ux_cities_slug ON cities (slug);

ALTER TABLE corporate_accounts
    ADD COLUMN IF NOT EXISTS slug TEXT;

UPDATE corporate_accounts
SET slug = trim(both '-' FROM regexp_replace(
        lower(
            translate(
                display_name,
                'İIıŞşĞğÜüÖöÇçÂâÊêÎîÔôÛû',
                'IiisSgGuUoOcCAaEeIiOoUu'
            )
        ),
        '[^a-z0-9]+',
        '-',
        'g'
    ))
WHERE slug IS NULL OR slug = '';

UPDATE corporate_accounts
SET slug = 'satici-' || id::text
WHERE slug IS NULL OR slug = '';

UPDATE corporate_accounts c
SET slug = c.slug || '-' || c.id::text
WHERE c.id IN (
    SELECT id
    FROM (
        SELECT id,
               ROW_NUMBER() OVER (PARTITION BY slug ORDER BY id) AS rn
        FROM corporate_accounts
        WHERE slug IS NOT NULL
    ) d
    WHERE d.rn > 1
);

ALTER TABLE corporate_accounts
    ALTER COLUMN slug SET NOT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS ux_corporate_accounts_slug ON corporate_accounts (slug);

CREATE INDEX IF NOT EXISTS ix_corporate_accounts_approved_slug
    ON corporate_accounts (slug)
    WHERE status = 'approved';
