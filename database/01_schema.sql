-- Araç Parkı schema — heavy equipment marketplace (PostgreSQL)
CREATE EXTENSION IF NOT EXISTS pg_trgm;

-- ---------------------------------------------------------------------------
-- Category taxonomy
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS category_groups (
    id          SERIAL PRIMARY KEY,
    name        TEXT NOT NULL UNIQUE,
    slug        TEXT NOT NULL UNIQUE,
    sort_order  INT NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS categories (
    id          SERIAL PRIMARY KEY,
    group_id    INT NULL REFERENCES category_groups (id),
    name        TEXT NOT NULL UNIQUE,
    slug        TEXT NOT NULL UNIQUE,
    icon_key    TEXT NOT NULL,
    sort_order  INT NOT NULL DEFAULT 0,
    -- primary capacity metric for filters/labels: weight | capacity_kg | capacity_t
    capacity_metric TEXT NOT NULL DEFAULT 'weight'
        CHECK (capacity_metric IN ('weight', 'capacity_kg', 'capacity_t'))
);

-- ---------------------------------------------------------------------------
-- Brands / models
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS brands (
    id          SERIAL PRIMARY KEY,
    name        TEXT NOT NULL UNIQUE,
    slug        TEXT NOT NULL UNIQUE,
    sort_order  INT NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS category_brands (
    category_id INT NOT NULL REFERENCES categories (id) ON DELETE CASCADE,
    brand_id    INT NOT NULL REFERENCES brands (id) ON DELETE CASCADE,
    PRIMARY KEY (category_id, brand_id)
);

CREATE TABLE IF NOT EXISTS equipment_models (
    id                    SERIAL PRIMARY KEY,
    brand_id              INT NOT NULL REFERENCES brands (id),
    category_id           INT NOT NULL REFERENCES categories (id),
    name                  TEXT NOT NULL,
    slug                  TEXT NOT NULL,
    typical_weight_min_t  NUMERIC(8, 2) NULL,
    typical_weight_max_t  NUMERIC(8, 2) NULL,
    horsepower            INT NULL CHECK (horsepower IS NULL OR horsepower >= 0),
    capacity_kg           INT NULL CHECK (capacity_kg IS NULL OR capacity_kg >= 0),
    capacity_t            NUMERIC(10, 2) NULL CHECK (capacity_t IS NULL OR capacity_t >= 0),
    default_specs         JSONB NOT NULL DEFAULT '{}'::jsonb,
    UNIQUE (brand_id, category_id, slug)
);

-- ---------------------------------------------------------------------------
-- Category attributes + attachments
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS category_attributes (
    id             SERIAL PRIMARY KEY,
    category_id    INT NOT NULL REFERENCES categories (id) ON DELETE CASCADE,
    key            TEXT NOT NULL,
    label          TEXT NOT NULL,
    data_type      TEXT NOT NULL CHECK (data_type IN ('number', 'bool', 'enum', 'text')),
    unit           TEXT NULL,
    is_filterable  BOOLEAN NOT NULL DEFAULT FALSE,
    is_required    BOOLEAN NOT NULL DEFAULT FALSE,
    sort_order     INT NOT NULL DEFAULT 0,
    enum_options   JSONB NULL,
    UNIQUE (category_id, key)
);

CREATE TABLE IF NOT EXISTS attachments (
    id    SERIAL PRIMARY KEY,
    name  TEXT NOT NULL UNIQUE,
    slug  TEXT NOT NULL UNIQUE
);

CREATE TABLE IF NOT EXISTS category_attachments (
    category_id    INT NOT NULL REFERENCES categories (id) ON DELETE CASCADE,
    attachment_id  INT NOT NULL REFERENCES attachments (id) ON DELETE CASCADE,
    PRIMARY KEY (category_id, attachment_id)
);

-- ---------------------------------------------------------------------------
-- Geo (NVI)
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS cities (
    id           INTEGER PRIMARY KEY,
    name         TEXT NOT NULL UNIQUE,
    plate_code   INTEGER NOT NULL UNIQUE,
    is_popular   BOOLEAN NOT NULL DEFAULT FALSE,
    sort_order   INT NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS districts (
    id            INTEGER PRIMARY KEY,
    city_id       INTEGER NOT NULL REFERENCES cities (id),
    name          TEXT NOT NULL,
    identity_no   INTEGER NOT NULL,
    UNIQUE (city_id, name)
);

CREATE TABLE IF NOT EXISTS neighborhoods (
    id               INTEGER PRIMARY KEY,
    city_id          INTEGER NOT NULL REFERENCES cities (id),
    district_id      INTEGER NOT NULL REFERENCES districts (id),
    name             TEXT NOT NULL,
    component_name   TEXT NOT NULL,
    identity_no      INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS streets (
    id               INTEGER PRIMARY KEY,
    city_id          INTEGER NOT NULL REFERENCES cities (id),
    district_id      INTEGER NOT NULL REFERENCES districts (id),
    neighborhood_id  INTEGER NOT NULL REFERENCES neighborhoods (id),
    name             TEXT NOT NULL,
    component_name   TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS location_meta (
    key   TEXT PRIMARY KEY,
    value TEXT NOT NULL
);

-- ---------------------------------------------------------------------------
-- Sellers / listings
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sellers (
    id            BIGSERIAL PRIMARY KEY,
    display_name  TEXT NOT NULL,
    seller_type   TEXT NOT NULL CHECK (seller_type IN ('dealer', 'owner')),
    is_verified   BOOLEAN NOT NULL DEFAULT FALSE,
    phone         TEXT NOT NULL,
    created_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at    TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS listings (
    id                BIGSERIAL PRIMARY KEY,
    ad_no             TEXT NOT NULL UNIQUE,
    title             TEXT NOT NULL,
    description       TEXT NOT NULL,
    category_id       INT NOT NULL REFERENCES categories (id),
    brand_id          INT NOT NULL REFERENCES brands (id),
    model_id          INT NULL REFERENCES equipment_models (id),
    model_name        TEXT NOT NULL,
    serial_no         TEXT NULL,
    city_id           INT NOT NULL REFERENCES cities (id),
    district_id       INT NOT NULL REFERENCES districts (id),
    neighborhood_id   INT NULL REFERENCES neighborhoods (id),
    seller_id         BIGINT NOT NULL REFERENCES sellers (id),
    -- URL/SEO locale: satilik | kiralik (labels stay Turkish in UI)
    primary_intent    TEXT NOT NULL CHECK (primary_intent IN ('satilik', 'kiralik')),
    intents           TEXT[] NOT NULL,
    -- new | used (equipment condition codes — English for catalog interoperability)
    condition         TEXT NOT NULL DEFAULT 'used'
                      CHECK (condition IN ('new', 'used')),
    model_year        INT NOT NULL
                      CHECK (model_year >= 1950 AND model_year <= 2100),
    hours             INT NULL CHECK (hours IS NULL OR hours >= 0),
    tons              NUMERIC(8, 2) NOT NULL CHECK (tons > 0),
    capacity_kg       INT NULL,
    horsepower        INT NULL CHECK (horsepower IS NULL OR horsepower >= 0),
    price             NUMERIC(14, 2) NOT NULL CHECK (price > 0),
    rent_price        NUMERIC(14, 2) NULL CHECK (rent_price IS NULL OR rent_price > 0),
    currency          TEXT NOT NULL DEFAULT 'TRY'
                      CHECK (currency IN ('TRY', 'USD', 'EUR')),
    price_unit        TEXT NULL
                      CHECK (price_unit IS NULL OR price_unit IN ('day', 'week', 'month', 'hour')),
    includes_operator BOOLEAN NOT NULL DEFAULT FALSE,
    specs             JSONB NOT NULL DEFAULT '{}'::jsonb
                      CHECK (jsonb_typeof(specs) = 'object'),
    cover_image_url   TEXT NOT NULL,
    status            TEXT NOT NULL DEFAULT 'pending_review'
                      CHECK (status IN ('draft', 'pending_review', 'published', 'rejected', 'archived')),
    rejection_reason  TEXT NULL
                      CHECK (rejection_reason IS NULL OR char_length(rejection_reason) <= 1000),
    reviewed_at       TIMESTAMPTZ NULL,
    reviewed_by_account_id BIGINT NULL,
    submitted_at      TIMESTAMPTZ NULL,
    listed_at         TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_at        TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at        TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT listings_intents_consistent CHECK (
        cardinality(intents) >= 1
        AND intents <@ ARRAY['satilik', 'kiralik']::text[]
        AND primary_intent = ANY (intents)
    )
);

CREATE TABLE IF NOT EXISTS listing_images (
    id          BIGSERIAL PRIMARY KEY,
    listing_id  BIGINT NOT NULL REFERENCES listings (id) ON DELETE CASCADE,
    url         TEXT NOT NULL,
    sort_order  INT NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS listing_attachments (
    listing_id     BIGINT NOT NULL REFERENCES listings (id) ON DELETE CASCADE,
    attachment_id  INT NOT NULL REFERENCES attachments (id) ON DELETE CASCADE,
    PRIMARY KEY (listing_id, attachment_id)
);

-- ---------------------------------------------------------------------------
-- Indexes
-- ---------------------------------------------------------------------------
CREATE INDEX IF NOT EXISTS ix_listings_status_listed_at
    ON listings (status, listed_at DESC, id DESC);

CREATE INDEX IF NOT EXISTS ix_listings_category_status
    ON listings (category_id, status);

CREATE INDEX IF NOT EXISTS ix_listings_brand_category
    ON listings (brand_id, category_id, status);

CREATE INDEX IF NOT EXISTS ix_listings_city_status
    ON listings (city_id, status);

CREATE INDEX IF NOT EXISTS ix_listings_seller ON listings (seller_id);
CREATE INDEX IF NOT EXISTS ix_listings_model ON listings (model_id) WHERE model_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS ix_listings_district ON listings (district_id);
CREATE INDEX IF NOT EXISTS ix_listings_neighborhood
    ON listings (neighborhood_id)
    WHERE neighborhood_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_listings_intents_gin
    ON listings USING GIN (intents);

CREATE INDEX IF NOT EXISTS ix_listings_title_trgm
    ON listings USING GIN (title gin_trgm_ops);

CREATE INDEX IF NOT EXISTS ix_listings_ad_no_trgm
    ON listings USING GIN (ad_no gin_trgm_ops);

CREATE INDEX IF NOT EXISTS ix_listings_model_name_trgm
    ON listings USING GIN (model_name gin_trgm_ops);

CREATE INDEX IF NOT EXISTS ix_listings_serial_trgm
    ON listings USING GIN (serial_no gin_trgm_ops);

CREATE INDEX IF NOT EXISTS ix_listings_price ON listings (price);
CREATE INDEX IF NOT EXISTS ix_listings_hours ON listings (hours);
CREATE INDEX IF NOT EXISTS ix_listings_tons ON listings (tons);
CREATE INDEX IF NOT EXISTS ix_listings_year ON listings (model_year);
CREATE INDEX IF NOT EXISTS ix_listings_condition ON listings (condition, status);
CREATE INDEX IF NOT EXISTS ix_listings_specs_gin ON listings USING GIN (specs jsonb_path_ops);

CREATE UNIQUE INDEX IF NOT EXISTS ux_listings_serial_no
    ON listings (serial_no) WHERE serial_no IS NOT NULL AND serial_no <> '';

CREATE INDEX IF NOT EXISTS ix_equipment_models_brand_cat
    ON equipment_models (brand_id, category_id);

CREATE INDEX IF NOT EXISTS ix_category_attributes_cat
    ON category_attributes (category_id, sort_order);

CREATE INDEX IF NOT EXISTS ix_category_attachments_attachment
    ON category_attachments (attachment_id);

CREATE INDEX IF NOT EXISTS ix_districts_city ON districts (city_id);
CREATE INDEX IF NOT EXISTS ix_districts_name ON districts (city_id, name);
CREATE INDEX IF NOT EXISTS ix_neighborhoods_district ON neighborhoods (district_id);
CREATE INDEX IF NOT EXISTS ix_neighborhoods_city ON neighborhoods (city_id);
CREATE INDEX IF NOT EXISTS ix_neighborhoods_name ON neighborhoods (district_id, name);
CREATE INDEX IF NOT EXISTS ix_streets_neighborhood ON streets (neighborhood_id);
CREATE INDEX IF NOT EXISTS ix_streets_district ON streets (district_id);
CREATE INDEX IF NOT EXISTS ix_streets_name_trgm ON streets USING GIN (name gin_trgm_ops);
CREATE INDEX IF NOT EXISTS ix_listing_images_listing ON listing_images (listing_id, sort_order);

-- updated_at + listing referential consistency
CREATE OR REPLACE FUNCTION set_updated_at()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    NEW.updated_at := NOW();
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_sellers_updated_at ON sellers;
CREATE TRIGGER trg_sellers_updated_at
    BEFORE UPDATE ON sellers
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();

DROP TRIGGER IF EXISTS trg_listings_updated_at ON listings;
CREATE TRIGGER trg_listings_updated_at
    BEFORE UPDATE ON listings
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();

CREATE OR REPLACE FUNCTION listings_validate_refs()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
DECLARE
    dist_city INTEGER;
    model_brand INTEGER;
    model_category INTEGER;
BEGIN
    SELECT city_id INTO dist_city FROM districts WHERE id = NEW.district_id;
    IF dist_city IS NULL OR dist_city <> NEW.city_id THEN
        RAISE EXCEPTION 'listings.city_id (%) must match districts.city_id for district %',
            NEW.city_id, NEW.district_id;
    END IF;

    IF NEW.model_id IS NOT NULL THEN
        SELECT brand_id, category_id
          INTO model_brand, model_category
          FROM equipment_models
         WHERE id = NEW.model_id;

        IF model_brand IS NULL THEN
            RAISE EXCEPTION 'listings.model_id % not found', NEW.model_id;
        END IF;

        IF model_brand <> NEW.brand_id OR model_category <> NEW.category_id THEN
            RAISE EXCEPTION
                'listings.model_id % does not match brand_id/category_id',
                NEW.model_id;
        END IF;
    END IF;

    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_listings_validate_refs ON listings;
CREATE TRIGGER trg_listings_validate_refs
    BEFORE INSERT OR UPDATE OF city_id, district_id, model_id, brand_id, category_id
    ON listings
    FOR EACH ROW EXECUTE FUNCTION listings_validate_refs();

INSERT INTO location_meta (key, value) VALUES
    ('source', 'adres.nvi.gov.tr via melihozkara/il-ilce-mahalle-sokak-veritabani'),
    ('data_date', '2026-04-12'),
    ('case', 'title')
ON CONFLICT (key) DO UPDATE SET value = EXCLUDED.value;
