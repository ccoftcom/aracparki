SELECT
    l.id,
    l.ad_no AS AdNo,
    l.title,
    c.name AS Category,
    b.name AS Brand,
    l.model_name AS ModelName,
    l.primary_intent AS PrimaryIntent,
    l.condition AS Condition,
    l.model_year AS ModelYear,
    l.hours,
    l.tons,
    l.capacity_kg AS CapacityKg,
    l.horsepower,
    city.name AS City,
    d.name AS District,
    l.price,
    l.currency AS Currency,
    l.price_unit AS PriceUnit,
    l.cover_image_url AS CoverImageUrl,
    s.seller_type AS SellerType,
    CASE WHEN ca.id IS NOT NULL AND ca.status = 'approved' THEN TRUE ELSE s.is_verified END AS IsVerified,
    l.listed_at AS ListedAt,
    l.status AS Status,
    l.rejection_reason AS RejectionReason
FROM listings l
JOIN categories c ON c.id = l.category_id
JOIN brands b ON b.id = l.brand_id
JOIN cities city ON city.id = l.city_id
JOIN districts d ON d.id = l.district_id
JOIN sellers s ON s.id = l.seller_id
LEFT JOIN corporate_accounts ca ON ca.id = l.corporate_account_id
WHERE s.account_id = @AccountId
  AND l.status IN ('pending_review', 'published', 'rejected', 'archived')
ORDER BY COALESCE(l.submitted_at, l.listed_at) DESC, l.id DESC
LIMIT @Take;
