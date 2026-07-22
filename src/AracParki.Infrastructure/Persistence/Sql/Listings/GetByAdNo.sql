SELECT
    l.id,
    l.ad_no AS AdNo,
    l.title,
    l.description,
    c.name AS Category,
    c.slug AS CategorySlug,
    c.id AS CategoryId,
    c.capacity_metric AS CapacityMetric,
    b.name AS Brand,
    l.model_name AS ModelName,
    l.serial_no AS SerialNo,
    l.primary_intent AS PrimaryIntent,
    l.intents,
    l.condition AS Condition,
    l.model_year AS ModelYear,
    l.hours,
    l.tons,
    l.capacity_kg AS CapacityKg,
    l.horsepower,
    city.name AS City,
    d.name AS District,
    n.name AS Neighborhood,
    l.price,
    l.rent_price AS RentPrice,
    l.currency AS Currency,
    l.price_unit AS PriceUnit,
    l.includes_operator AS IncludesOperator,
    l.specs::text AS SpecsJson,
    l.cover_image_url AS CoverImageUrl,
    COALESCE(
        NULLIF(BTRIM(ca.display_name), ''),
        NULLIF(BTRIM(ca.trade_name), ''),
        s.display_name
    ) AS SellerName,
    s.seller_type AS SellerType,
    CASE WHEN ca.id IS NOT NULL AND ca.status = 'approved' THEN TRUE ELSE s.is_verified END AS IsVerified,
    l.corporate_account_id AS CorporateAccountId,
    COALESCE(NULLIF(BTRIM(ca.display_name), ''), NULLIF(BTRIM(ca.trade_name), '')) AS CorporateDisplayName,
    CASE WHEN ca.status = 'approved' THEN ca.slug ELSE NULL END AS CorporateSlug,
    l.listed_at AS ListedAt,
    l.status AS Status,
    l.rejection_reason AS RejectionReason,
    l.submitted_at AS SubmittedAt,
    s.account_id AS OwnerAccountId
FROM listings l
JOIN categories c ON c.id = l.category_id
JOIN brands b ON b.id = l.brand_id
JOIN cities city ON city.id = l.city_id
JOIN districts d ON d.id = l.district_id
LEFT JOIN neighborhoods n ON n.id = l.neighborhood_id
JOIN sellers s ON s.id = l.seller_id
LEFT JOIN corporate_accounts ca ON ca.id = l.corporate_account_id
WHERE l.ad_no = @AdNo
  AND (
      l.status = 'published'
      OR (
          @IsAdmin = TRUE
          AND l.status IN ('pending_review', 'rejected', 'archived')
      )
      OR (
          @ViewerAccountId IS NOT NULL
          AND s.account_id = @ViewerAccountId
          AND l.status IN ('pending_review', 'rejected', 'archived')
      )
  )
LIMIT 1;
