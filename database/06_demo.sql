-- Demo sellers + listings (equipment identity: brand/model/condition)
BEGIN;

INSERT INTO sellers (id, display_name, seller_type, is_verified, phone) VALUES
 (1, 'Bayi Makine 1', 'dealer', true, '02165550101'),
 (2, 'Sahibi Makine 2', 'owner', true, '03125550102'),
 (3, 'Bayi Makine 3', 'dealer', true, '02325550103'),
 (4, 'Bayi Makine 4', 'dealer', true, '02245550104'),
 (5, 'Sahibi Makine 5', 'owner', false, '02425550105'),
 (6, 'Bayi Makine 6', 'dealer', true, '02625550106'),
 (7, 'Bayi Makine 7', 'dealer', true, '03425550107'),
 (8, 'Sahibi Makine 8', 'owner', true, '03325550108'),
 (9, 'Bayi Makine 9', 'dealer', true, '03225550109'),
 (10, 'Sahibi Makine 10', 'owner', false, '03245550110'),
 (11, 'Bayi Makine 11', 'dealer', true, '03525550111'),
 (12, 'Bayi Makine 12', 'dealer', true, '04125550112')
ON CONFLICT (id) DO UPDATE SET
  display_name=EXCLUDED.display_name, seller_type=EXCLUDED.seller_type,
  is_verified=EXCLUDED.is_verified, phone=EXCLUDED.phone;

-- 1 Caterpillar 320D
INSERT INTO listings (
  id, ad_no, title, description, category_id, brand_id, model_id, model_name, serial_no,
  city_id, district_id, seller_id, primary_intent, intents, condition,
  model_year, hours, tons, capacity_kg, horsepower, price, price_unit, includes_operator, specs,
  cover_image_url, status, listed_at
)
SELECT
  1, 'AP-100001',
  'Caterpillar 320D · 2019 · 22t · 7200h',
  'Düzenli bakımlı paletli ekskavatör. Kırıcı tesisatlı, klimalı kabin. Konum: İstanbul / Tuzla.',
  1, 1, 1, '320D', 'CAT0320DXXXX0001',
  city.id, dist.id, 1, 'satilik', ARRAY['satilik']::text[], 'used',
  2019, 7200, 22, NULL, 162, 4850000, NULL, false,
  '{"undercarriage":"steel_track","tail_swing":"standard","breaker_circuit":true,"ac_cabin":true,"bucket_volume_m3":1.0}'::jsonb,
  '/uploads/listings/demo/placeholder.jpg',
  'published', TIMESTAMPTZ '2026-07-11T10:00:00+03:00'
FROM cities city
JOIN districts dist ON dist.name = 'Tuzla' AND dist.city_id = city.id
WHERE city.name = 'İstanbul'
ON CONFLICT (id) DO UPDATE SET
  ad_no=EXCLUDED.ad_no, title=EXCLUDED.title, description=EXCLUDED.description,
  category_id=EXCLUDED.category_id, brand_id=EXCLUDED.brand_id, model_id=EXCLUDED.model_id,
  model_name=EXCLUDED.model_name, serial_no=EXCLUDED.serial_no,
  city_id=EXCLUDED.city_id, district_id=EXCLUDED.district_id, seller_id=EXCLUDED.seller_id,
  primary_intent=EXCLUDED.primary_intent, intents=EXCLUDED.intents, condition=EXCLUDED.condition,
  model_year=EXCLUDED.model_year, hours=EXCLUDED.hours, tons=EXCLUDED.tons, capacity_kg=EXCLUDED.capacity_kg,
  horsepower=EXCLUDED.horsepower, price=EXCLUDED.price, price_unit=EXCLUDED.price_unit,
  includes_operator=EXCLUDED.includes_operator, specs=EXCLUDED.specs,
  cover_image_url=EXCLUDED.cover_image_url, status=EXCLUDED.status, listed_at=EXCLUDED.listed_at;

DELETE FROM listing_attachments WHERE listing_id = 1;
INSERT INTO listing_attachments (listing_id, attachment_id) VALUES (1, 1), (1, 2);
DELETE FROM listing_images WHERE listing_id = 1;
INSERT INTO listing_images (listing_id, url, sort_order) VALUES
 (1, '/uploads/listings/demo/placeholder.jpg', 0);

-- 2 Hidromek HMK 102B
INSERT INTO listings (
  id, ad_no, title, description, category_id, brand_id, model_id, model_name, serial_no,
  city_id, district_id, seller_id, primary_intent, intents, condition,
  model_year, hours, tons, capacity_kg, horsepower, price, price_unit, includes_operator, specs,
  cover_image_url, status, listed_at
)
SELECT
  2, 'AP-100002',
  'Hidromek HMK 102B · 2021 · 8.5t · 4100h',
  'Bakımlı beko loder. 4x4, klimalı. Konum: Ankara / Sincan.',
  2, 7, 8, 'HMK 102B', 'HMK102B000002',
  city.id, dist.id, 2, 'satilik', ARRAY['satilik']::text[], 'used',
  2021, 4100, 8.5, NULL, 100, 2750000, NULL, false,
  '{"four_wd":true,"extendable_dipper":false,"ac_cabin":true}'::jsonb,
  '/uploads/listings/demo/placeholder.jpg',
  'published', TIMESTAMPTZ '2026-07-12T10:00:00+03:00'
FROM cities city
JOIN districts dist ON dist.name = 'Sincan' AND dist.city_id = city.id
WHERE city.name = 'Ankara'
ON CONFLICT (id) DO UPDATE SET
  ad_no=EXCLUDED.ad_no, title=EXCLUDED.title, description=EXCLUDED.description,
  category_id=EXCLUDED.category_id, brand_id=EXCLUDED.brand_id, model_id=EXCLUDED.model_id,
  model_name=EXCLUDED.model_name, serial_no=EXCLUDED.serial_no,
  city_id=EXCLUDED.city_id, district_id=EXCLUDED.district_id, seller_id=EXCLUDED.seller_id,
  primary_intent=EXCLUDED.primary_intent, intents=EXCLUDED.intents, condition=EXCLUDED.condition,
  model_year=EXCLUDED.model_year, hours=EXCLUDED.hours, tons=EXCLUDED.tons, capacity_kg=EXCLUDED.capacity_kg,
  horsepower=EXCLUDED.horsepower, price=EXCLUDED.price, price_unit=EXCLUDED.price_unit,
  includes_operator=EXCLUDED.includes_operator, specs=EXCLUDED.specs,
  cover_image_url=EXCLUDED.cover_image_url, status=EXCLUDED.status, listed_at=EXCLUDED.listed_at;

DELETE FROM listing_images WHERE listing_id = 2;
INSERT INTO listing_images (listing_id, url, sort_order) VALUES
 (2, '/uploads/listings/demo/placeholder.jpg', 0);

-- 3 Komatsu PC210 — kiralık
INSERT INTO listings (
  id, ad_no, title, description, category_id, brand_id, model_id, model_name, serial_no,
  city_id, district_id, seller_id, primary_intent, intents, condition,
  model_year, hours, tons, capacity_kg, horsepower, price, price_unit, includes_operator, specs,
  cover_image_url, status, listed_at
)
SELECT
  3, 'AP-100003',
  'Komatsu PC210 · 2020 · 22t · 5800h',
  'Operatörlü kiralık paletli ekskavatör. Konum: İzmir / Aliağa.',
  1, 2, 4, 'PC210', 'KMTPC21000003',
  city.id, dist.id, 3, 'kiralik', ARRAY['kiralik']::text[], 'used',
  2020, 5800, 22, NULL, 123, 18000, 'day', true,
  '{"undercarriage":"steel_track","tail_swing":"standard","breaker_circuit":true,"ac_cabin":true}'::jsonb,
  '/uploads/listings/demo/placeholder.jpg',
  'published', TIMESTAMPTZ '2026-07-13T10:00:00+03:00'
FROM cities city
JOIN districts dist ON dist.name = 'Aliağa' AND dist.city_id = city.id
WHERE city.name = 'İzmir'
ON CONFLICT (id) DO UPDATE SET
  ad_no=EXCLUDED.ad_no, title=EXCLUDED.title, description=EXCLUDED.description,
  category_id=EXCLUDED.category_id, brand_id=EXCLUDED.brand_id, model_id=EXCLUDED.model_id,
  model_name=EXCLUDED.model_name, serial_no=EXCLUDED.serial_no,
  city_id=EXCLUDED.city_id, district_id=EXCLUDED.district_id, seller_id=EXCLUDED.seller_id,
  primary_intent=EXCLUDED.primary_intent, intents=EXCLUDED.intents, condition=EXCLUDED.condition,
  model_year=EXCLUDED.model_year, hours=EXCLUDED.hours, tons=EXCLUDED.tons, capacity_kg=EXCLUDED.capacity_kg,
  horsepower=EXCLUDED.horsepower, price=EXCLUDED.price, price_unit=EXCLUDED.price_unit,
  includes_operator=EXCLUDED.includes_operator, specs=EXCLUDED.specs,
  cover_image_url=EXCLUDED.cover_image_url, status=EXCLUDED.status, listed_at=EXCLUDED.listed_at;

DELETE FROM listing_attachments WHERE listing_id = 3;
INSERT INTO listing_attachments (listing_id, attachment_id) VALUES (3, 1), (3, 2);
DELETE FROM listing_images WHERE listing_id = 3;
INSERT INTO listing_images (listing_id, url, sort_order) VALUES
 (3, '/uploads/listings/demo/placeholder.jpg', 0);

-- 4 Volvo L120H
INSERT INTO listings (
  id, ad_no, title, description, category_id, brand_id, model_id, model_name, serial_no,
  city_id, district_id, seller_id, primary_intent, intents, condition,
  model_year, hours, tons, capacity_kg, horsepower, price, price_unit, includes_operator, specs,
  cover_image_url, status, listed_at
)
SELECT
  4, 'AP-100004',
  'Volvo L120H · 2018 · 19t · 9200h',
  'Lastikli yükleyici. Konum: Bursa / Nilüfer.',
  3, 5, 11, 'L120H', 'VOLVL120H0004',
  city.id, dist.id, 4, 'satilik', ARRAY['satilik']::text[], 'used',
  2018, 9200, 19, NULL, 276, 4200000, NULL, false,
  '{"bucket_volume_m3":3.5,"articulated":true}'::jsonb,
  '/uploads/listings/demo/placeholder.jpg',
  'published', TIMESTAMPTZ '2026-07-10T10:00:00+03:00'
FROM cities city
JOIN districts dist ON dist.name = 'Nilüfer' AND dist.city_id = city.id
WHERE city.name = 'Bursa'
ON CONFLICT (id) DO UPDATE SET
  ad_no=EXCLUDED.ad_no, title=EXCLUDED.title, description=EXCLUDED.description,
  category_id=EXCLUDED.category_id, brand_id=EXCLUDED.brand_id, model_id=EXCLUDED.model_id,
  model_name=EXCLUDED.model_name, serial_no=EXCLUDED.serial_no,
  city_id=EXCLUDED.city_id, district_id=EXCLUDED.district_id, seller_id=EXCLUDED.seller_id,
  primary_intent=EXCLUDED.primary_intent, intents=EXCLUDED.intents, condition=EXCLUDED.condition,
  model_year=EXCLUDED.model_year, hours=EXCLUDED.hours, tons=EXCLUDED.tons, capacity_kg=EXCLUDED.capacity_kg,
  horsepower=EXCLUDED.horsepower, price=EXCLUDED.price, price_unit=EXCLUDED.price_unit,
  includes_operator=EXCLUDED.includes_operator, specs=EXCLUDED.specs,
  cover_image_url=EXCLUDED.cover_image_url, status=EXCLUDED.status, listed_at=EXCLUDED.listed_at;

DELETE FROM listing_images WHERE listing_id = 4;
INSERT INTO listing_images (listing_id, url, sort_order) VALUES
 (4, '/uploads/listings/demo/placeholder.jpg', 0);

-- 5 JCB 3CX
INSERT INTO listings (
  id, ad_no, title, description, category_id, brand_id, model_id, model_name, serial_no,
  city_id, district_id, seller_id, primary_intent, intents, condition,
  model_year, hours, tons, capacity_kg, horsepower, price, price_unit, includes_operator, specs,
  cover_image_url, status, listed_at
)
SELECT
  5, 'AP-100005',
  'JCB 3CX · 2022 · 8t · 2100h',
  'Az saatli beko loder. Konum: Antalya / Kepez.',
  2, 6, 9, '3CX', 'JCB3CX0000005',
  city.id, dist.id, 5, 'satilik', ARRAY['satilik']::text[], 'used',
  2022, 2100, 8, NULL, 109, 3100000, NULL, false,
  '{"four_wd":true,"extendable_dipper":true,"ac_cabin":true}'::jsonb,
  '/uploads/listings/demo/placeholder.jpg',
  'published', TIMESTAMPTZ '2026-07-14T10:00:00+03:00'
FROM cities city
JOIN districts dist ON dist.name = 'Kepez' AND dist.city_id = city.id
WHERE city.name = 'Antalya'
ON CONFLICT (id) DO UPDATE SET
  ad_no=EXCLUDED.ad_no, title=EXCLUDED.title, description=EXCLUDED.description,
  category_id=EXCLUDED.category_id, brand_id=EXCLUDED.brand_id, model_id=EXCLUDED.model_id,
  model_name=EXCLUDED.model_name, serial_no=EXCLUDED.serial_no,
  city_id=EXCLUDED.city_id, district_id=EXCLUDED.district_id, seller_id=EXCLUDED.seller_id,
  primary_intent=EXCLUDED.primary_intent, intents=EXCLUDED.intents, condition=EXCLUDED.condition,
  model_year=EXCLUDED.model_year, hours=EXCLUDED.hours, tons=EXCLUDED.tons, capacity_kg=EXCLUDED.capacity_kg,
  horsepower=EXCLUDED.horsepower, price=EXCLUDED.price, price_unit=EXCLUDED.price_unit,
  includes_operator=EXCLUDED.includes_operator, specs=EXCLUDED.specs,
  cover_image_url=EXCLUDED.cover_image_url, status=EXCLUDED.status, listed_at=EXCLUDED.listed_at;

DELETE FROM listing_images WHERE listing_id = 5;
INSERT INTO listing_images (listing_id, url, sort_order) VALUES
 (5, '/uploads/listings/demo/placeholder.jpg', 0);

-- 6 Toyota forklift
INSERT INTO listings (
  id, ad_no, title, description, category_id, brand_id, model_id, model_name, serial_no,
  city_id, district_id, seller_id, primary_intent, intents, condition,
  model_year, hours, tons, capacity_kg, horsepower, price, price_unit, includes_operator, specs,
  cover_image_url, status, listed_at
)
SELECT
  6, 'AP-100006',
  'Toyota 8FBE15 · 2019 · 1500kg · 3200h',
  'Elektrikli forklift. Konum: Kocaeli / Gebze.',
  4, 15, 12, '8FBE15', 'TYT8FBE150006',
  city.id, dist.id, 6, 'satilik', ARRAY['satilik']::text[], 'used',
  2019, 3200, 2.5, 1500, 0, 890000, NULL, false,
  '{"fuel":"electric","mast_type":"duplex","lift_height_m":4.5}'::jsonb,
  '/uploads/listings/demo/placeholder.jpg',
  'published', TIMESTAMPTZ '2026-07-09T10:00:00+03:00'
FROM cities city
JOIN districts dist ON dist.name = 'Gebze' AND dist.city_id = city.id
WHERE city.name = 'Kocaeli'
ON CONFLICT (id) DO UPDATE SET
  ad_no=EXCLUDED.ad_no, title=EXCLUDED.title, description=EXCLUDED.description,
  category_id=EXCLUDED.category_id, brand_id=EXCLUDED.brand_id, model_id=EXCLUDED.model_id,
  model_name=EXCLUDED.model_name, serial_no=EXCLUDED.serial_no,
  city_id=EXCLUDED.city_id, district_id=EXCLUDED.district_id, seller_id=EXCLUDED.seller_id,
  primary_intent=EXCLUDED.primary_intent, intents=EXCLUDED.intents, condition=EXCLUDED.condition,
  model_year=EXCLUDED.model_year, hours=EXCLUDED.hours, tons=EXCLUDED.tons, capacity_kg=EXCLUDED.capacity_kg,
  horsepower=EXCLUDED.horsepower, price=EXCLUDED.price, price_unit=EXCLUDED.price_unit,
  includes_operator=EXCLUDED.includes_operator, specs=EXCLUDED.specs,
  cover_image_url=EXCLUDED.cover_image_url, status=EXCLUDED.status, listed_at=EXCLUDED.listed_at;

DELETE FROM listing_attachments WHERE listing_id = 6;
INSERT INTO listing_attachments (listing_id, attachment_id) VALUES (6, 4);
DELETE FROM listing_images WHERE listing_id = 6;
INSERT INTO listing_images (listing_id, url, sort_order) VALUES
 (6, '/uploads/listings/demo/placeholder.jpg', 0);

-- 7 Liebherr crane — kiralık
INSERT INTO listings (
  id, ad_no, title, description, category_id, brand_id, model_id, model_name, serial_no,
  city_id, district_id, seller_id, primary_intent, intents, condition,
  model_year, hours, tons, capacity_kg, horsepower, price, price_unit, includes_operator, specs,
  cover_image_url, status, listed_at
)
SELECT
  7, 'AP-100007',
  'Liebherr LTM 1100 · 2017 · 100t · 11000h',
  'Mobil vinç, operatörlü kiralık. Konum: Gaziantep / Şehitkamil.',
  5, 9, 14, 'LTM 1100', 'LIEBLTM110007',
  city.id, dist.id, 7, 'kiralik', ARRAY['kiralik']::text[], 'used',
  2017, 11000, 100, NULL, 450, 45000, 'day', true,
  '{"crane_type":"mobile","boom_length_m":52}'::jsonb,
  '/uploads/listings/demo/placeholder.jpg',
  'published', TIMESTAMPTZ '2026-07-08T10:00:00+03:00'
FROM cities city
JOIN districts dist ON dist.name = 'Şehitkamil' AND dist.city_id = city.id
WHERE city.name = 'Gaziantep'
ON CONFLICT (id) DO UPDATE SET
  ad_no=EXCLUDED.ad_no, title=EXCLUDED.title, description=EXCLUDED.description,
  category_id=EXCLUDED.category_id, brand_id=EXCLUDED.brand_id, model_id=EXCLUDED.model_id,
  model_name=EXCLUDED.model_name, serial_no=EXCLUDED.serial_no,
  city_id=EXCLUDED.city_id, district_id=EXCLUDED.district_id, seller_id=EXCLUDED.seller_id,
  primary_intent=EXCLUDED.primary_intent, intents=EXCLUDED.intents, condition=EXCLUDED.condition,
  model_year=EXCLUDED.model_year, hours=EXCLUDED.hours, tons=EXCLUDED.tons, capacity_kg=EXCLUDED.capacity_kg,
  horsepower=EXCLUDED.horsepower, price=EXCLUDED.price, price_unit=EXCLUDED.price_unit,
  includes_operator=EXCLUDED.includes_operator, specs=EXCLUDED.specs,
  cover_image_url=EXCLUDED.cover_image_url, status=EXCLUDED.status, listed_at=EXCLUDED.listed_at;

DELETE FROM listing_images WHERE listing_id = 7;
INSERT INTO listing_images (listing_id, url, sort_order) VALUES
 (7, '/uploads/listings/demo/placeholder.jpg', 0);

-- 8 Cat D6T
INSERT INTO listings (
  id, ad_no, title, description, category_id, brand_id, model_id, model_name, serial_no,
  city_id, district_id, seller_id, primary_intent, intents, condition,
  model_year, hours, tons, capacity_kg, horsepower, price, price_unit, includes_operator, specs,
  cover_image_url, status, listed_at
)
SELECT
  8, 'AP-100008',
  'Caterpillar D6T · 2016 · 20t · 14000h',
  'Dozer. Konum: Konya / Selçuklu.',
  6, 1, 15, 'D6T', 'CAT00D6T000008',
  city.id, dist.id, 8, 'satilik', ARRAY['satilik']::text[], 'used',
  2016, 14000, 20, NULL, 215, 3900000, NULL, false,
  '{"lgp":false,"blade_width_m":3.4}'::jsonb,
  '/uploads/listings/demo/placeholder.jpg',
  'published', TIMESTAMPTZ '2026-07-07T10:00:00+03:00'
FROM cities city
JOIN districts dist ON dist.name = 'Selçuklu' AND dist.city_id = city.id
WHERE city.name = 'Konya'
ON CONFLICT (id) DO UPDATE SET
  ad_no=EXCLUDED.ad_no, title=EXCLUDED.title, description=EXCLUDED.description,
  category_id=EXCLUDED.category_id, brand_id=EXCLUDED.brand_id, model_id=EXCLUDED.model_id,
  model_name=EXCLUDED.model_name, serial_no=EXCLUDED.serial_no,
  city_id=EXCLUDED.city_id, district_id=EXCLUDED.district_id, seller_id=EXCLUDED.seller_id,
  primary_intent=EXCLUDED.primary_intent, intents=EXCLUDED.intents, condition=EXCLUDED.condition,
  model_year=EXCLUDED.model_year, hours=EXCLUDED.hours, tons=EXCLUDED.tons, capacity_kg=EXCLUDED.capacity_kg,
  horsepower=EXCLUDED.horsepower, price=EXCLUDED.price, price_unit=EXCLUDED.price_unit,
  includes_operator=EXCLUDED.includes_operator, specs=EXCLUDED.specs,
  cover_image_url=EXCLUDED.cover_image_url, status=EXCLUDED.status, listed_at=EXCLUDED.listed_at;

DELETE FROM listing_attachments WHERE listing_id = 8;
INSERT INTO listing_attachments (listing_id, attachment_id) VALUES (8, 6);
DELETE FROM listing_images WHERE listing_id = 8;
INSERT INTO listing_images (listing_id, url, sort_order) VALUES
 (8, '/uploads/listings/demo/placeholder.jpg', 0);

-- 9 Case 580ST (beko — model free-text)
INSERT INTO listings (
  id, ad_no, title, description, category_id, brand_id, model_id, model_name, serial_no,
  city_id, district_id, seller_id, primary_intent, intents, condition,
  model_year, hours, tons, capacity_kg, horsepower, price, price_unit, includes_operator, specs,
  cover_image_url, status, listed_at
)
SELECT
  9, 'AP-100009',
  'Case 580ST · 2015 · 8t · 12500h',
  'Beko loder. Konum: Adana / Seyhan.',
  2, 12, NULL, '580ST', 'CASE580ST0009',
  city.id, dist.id, 9, 'satilik', ARRAY['satilik']::text[], 'used',
  2015, 12500, 8, NULL, 97, 1650000, NULL, false,
  '{"four_wd":true,"extendable_dipper":false,"ac_cabin":false}'::jsonb,
  '/uploads/listings/demo/placeholder.jpg',
  'published', TIMESTAMPTZ '2026-07-06T10:00:00+03:00'
FROM cities city
JOIN districts dist ON dist.name = 'Seyhan' AND dist.city_id = city.id
WHERE city.name = 'Adana'
ON CONFLICT (id) DO UPDATE SET
  ad_no=EXCLUDED.ad_no, title=EXCLUDED.title, description=EXCLUDED.description,
  category_id=EXCLUDED.category_id, brand_id=EXCLUDED.brand_id, model_id=EXCLUDED.model_id,
  model_name=EXCLUDED.model_name, serial_no=EXCLUDED.serial_no,
  city_id=EXCLUDED.city_id, district_id=EXCLUDED.district_id, seller_id=EXCLUDED.seller_id,
  primary_intent=EXCLUDED.primary_intent, intents=EXCLUDED.intents, condition=EXCLUDED.condition,
  model_year=EXCLUDED.model_year, hours=EXCLUDED.hours, tons=EXCLUDED.tons, capacity_kg=EXCLUDED.capacity_kg,
  horsepower=EXCLUDED.horsepower, price=EXCLUDED.price, price_unit=EXCLUDED.price_unit,
  includes_operator=EXCLUDED.includes_operator, specs=EXCLUDED.specs,
  cover_image_url=EXCLUDED.cover_image_url, status=EXCLUDED.status, listed_at=EXCLUDED.listed_at;

DELETE FROM listing_images WHERE listing_id = 9;
INSERT INTO listing_images (listing_id, url, sort_order) VALUES
 (9, '/uploads/listings/demo/placeholder.jpg', 0);

-- 10 Bobcat E50 mini
INSERT INTO listings (
  id, ad_no, title, description, category_id, brand_id, model_id, model_name, serial_no,
  city_id, district_id, seller_id, primary_intent, intents, condition,
  model_year, hours, tons, capacity_kg, horsepower, price, price_unit, includes_operator, specs,
  cover_image_url, status, listed_at
)
SELECT
  10, 'AP-100010',
  'Bobcat E50 · 2023 · 5t · 900h',
  'Az saatli mini ekskavatör. Konum: Mersin / Yenişehir.',
  9, 8, 18, 'E35', 'BOBCE50000010',
  city.id, dist.id, 10, 'satilik', ARRAY['satilik']::text[], 'used',
  2023, 900, 5, NULL, 49, 2450000, NULL, false,
  '{"undercarriage":"rubber_track","tail_swing":"zero","breaker_circuit":true,"ac_cabin":true}'::jsonb,
  '/uploads/listings/demo/placeholder.jpg',
  'published', TIMESTAMPTZ '2026-07-15T10:00:00+03:00'
FROM cities city
JOIN districts dist ON dist.name = 'Yenişehir' AND dist.city_id = city.id
WHERE city.name = 'Mersin'
ON CONFLICT (id) DO UPDATE SET
  ad_no=EXCLUDED.ad_no, title=EXCLUDED.title, description=EXCLUDED.description,
  category_id=EXCLUDED.category_id, brand_id=EXCLUDED.brand_id, model_id=EXCLUDED.model_id,
  model_name=EXCLUDED.model_name, serial_no=EXCLUDED.serial_no,
  city_id=EXCLUDED.city_id, district_id=EXCLUDED.district_id, seller_id=EXCLUDED.seller_id,
  primary_intent=EXCLUDED.primary_intent, intents=EXCLUDED.intents, condition=EXCLUDED.condition,
  model_year=EXCLUDED.model_year, hours=EXCLUDED.hours, tons=EXCLUDED.tons, capacity_kg=EXCLUDED.capacity_kg,
  horsepower=EXCLUDED.horsepower, price=EXCLUDED.price, price_unit=EXCLUDED.price_unit,
  includes_operator=EXCLUDED.includes_operator, specs=EXCLUDED.specs,
  cover_image_url=EXCLUDED.cover_image_url, status=EXCLUDED.status, listed_at=EXCLUDED.listed_at;

-- Fix model name to E50 for listing 10 (catalog has E35 as proxy)
UPDATE listings SET model_name = 'E50', model_id = NULL WHERE id = 10;

DELETE FROM listing_attachments WHERE listing_id = 10;
INSERT INTO listing_attachments (listing_id, attachment_id) VALUES (10, 1), (10, 2);
DELETE FROM listing_images WHERE listing_id = 10;
INSERT INTO listing_images (listing_id, url, sort_order) VALUES
 (10, '/uploads/listings/demo/placeholder.jpg', 0);

-- 11 Manitou telehandler
INSERT INTO listings (
  id, ad_no, title, description, category_id, brand_id, model_id, model_name, serial_no,
  city_id, district_id, seller_id, primary_intent, intents, condition,
  model_year, hours, tons, capacity_kg, horsepower, price, price_unit, includes_operator, specs,
  cover_image_url, status, listed_at
)
SELECT
  11, 'AP-100011',
  'Manitou MT 1840 · 2020 · 4000kg · 4500h',
  'Telehandler. Konum: Kayseri / Melikgazi.',
  10, 14, 20, 'MT1840', 'MANMT18400011',
  city.id, dist.id, 11, 'satilik', ARRAY['satilik']::text[], 'used',
  2020, 4500, 11, 4000, 100, 2650000, NULL, false,
  '{"lift_height_m":17.5,"fuel":"diesel"}'::jsonb,
  '/uploads/listings/demo/placeholder.jpg',
  'published', TIMESTAMPTZ '2026-07-05T10:00:00+03:00'
FROM cities city
JOIN districts dist ON dist.name = 'Melikgazi' AND dist.city_id = city.id
WHERE city.name = 'Kayseri'
ON CONFLICT (id) DO UPDATE SET
  ad_no=EXCLUDED.ad_no, title=EXCLUDED.title, description=EXCLUDED.description,
  category_id=EXCLUDED.category_id, brand_id=EXCLUDED.brand_id, model_id=EXCLUDED.model_id,
  model_name=EXCLUDED.model_name, serial_no=EXCLUDED.serial_no,
  city_id=EXCLUDED.city_id, district_id=EXCLUDED.district_id, seller_id=EXCLUDED.seller_id,
  primary_intent=EXCLUDED.primary_intent, intents=EXCLUDED.intents, condition=EXCLUDED.condition,
  model_year=EXCLUDED.model_year, hours=EXCLUDED.hours, tons=EXCLUDED.tons, capacity_kg=EXCLUDED.capacity_kg,
  horsepower=EXCLUDED.horsepower, price=EXCLUDED.price, price_unit=EXCLUDED.price_unit,
  includes_operator=EXCLUDED.includes_operator, specs=EXCLUDED.specs,
  cover_image_url=EXCLUDED.cover_image_url, status=EXCLUDED.status, listed_at=EXCLUDED.listed_at;

DELETE FROM listing_attachments WHERE listing_id = 11;
INSERT INTO listing_attachments (listing_id, attachment_id) VALUES (11, 4);
DELETE FROM listing_images WHERE listing_id = 11;
INSERT INTO listing_images (listing_id, url, sort_order) VALUES
 (11, '/uploads/listings/demo/placeholder.jpg', 0);

-- 12 Doosan DX225
INSERT INTO listings (
  id, ad_no, title, description, category_id, brand_id, model_id, model_name, serial_no,
  city_id, district_id, seller_id, primary_intent, intents, condition,
  model_year, hours, tons, capacity_kg, horsepower, price, price_unit, includes_operator, specs,
  cover_image_url, status, listed_at
)
SELECT
  12, 'AP-100012',
  'Doosan DX225 · 2019 · 22t · 6700h',
  'Paletli ekskavatör. Konum: Diyarbakır / Bağlar.',
  1, 10, NULL, 'DX225', 'DOOSX22500012',
  city.id, dist.id, 12, 'satilik', ARRAY['satilik']::text[], 'used',
  2019, 6700, 22, NULL, 163, 3950000, NULL, false,
  '{"undercarriage":"steel_track","tail_swing":"standard","breaker_circuit":false,"ac_cabin":true}'::jsonb,
  '/uploads/listings/demo/placeholder.jpg',
  'published', TIMESTAMPTZ '2026-07-04T10:00:00+03:00'
FROM cities city
JOIN districts dist ON dist.name = 'Bağlar' AND dist.city_id = city.id
WHERE city.name = 'Diyarbakır'
ON CONFLICT (id) DO UPDATE SET
  ad_no=EXCLUDED.ad_no, title=EXCLUDED.title, description=EXCLUDED.description,
  category_id=EXCLUDED.category_id, brand_id=EXCLUDED.brand_id, model_id=EXCLUDED.model_id,
  model_name=EXCLUDED.model_name, serial_no=EXCLUDED.serial_no,
  city_id=EXCLUDED.city_id, district_id=EXCLUDED.district_id, seller_id=EXCLUDED.seller_id,
  primary_intent=EXCLUDED.primary_intent, intents=EXCLUDED.intents, condition=EXCLUDED.condition,
  model_year=EXCLUDED.model_year, hours=EXCLUDED.hours, tons=EXCLUDED.tons, capacity_kg=EXCLUDED.capacity_kg,
  horsepower=EXCLUDED.horsepower, price=EXCLUDED.price, price_unit=EXCLUDED.price_unit,
  includes_operator=EXCLUDED.includes_operator, specs=EXCLUDED.specs,
  cover_image_url=EXCLUDED.cover_image_url, status=EXCLUDED.status, listed_at=EXCLUDED.listed_at;

DELETE FROM listing_images WHERE listing_id = 12;
INSERT INTO listing_images (listing_id, url, sort_order) VALUES
 (12, '/uploads/listings/demo/placeholder.jpg', 0);

SELECT setval(pg_get_serial_sequence('sellers','id'), GREATEST((SELECT MAX(id) FROM sellers),1));
SELECT setval(pg_get_serial_sequence('listings','id'), GREATEST((SELECT MAX(id) FROM listings),1));
SELECT setval(pg_get_serial_sequence('listing_images','id'), GREATEST((SELECT COALESCE(MAX(id),1) FROM listing_images),1));
SELECT setval(pg_get_serial_sequence('attachments','id'), GREATEST((SELECT MAX(id) FROM attachments),1));

COMMIT;
