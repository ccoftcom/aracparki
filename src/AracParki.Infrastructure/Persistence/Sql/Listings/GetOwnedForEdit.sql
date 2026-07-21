SELECT
    l.id,
    l.ad_no AS AdNo,
    l.title,
    l.description,
    l.category_id AS CategoryId,
    c.name AS CategoryName,
    c.capacity_metric AS CapacityMetric,
    c.group_id AS GroupId,
    g.name AS GroupName,
    l.brand_id AS BrandId,
    b.name AS BrandName,
    l.model_id AS ModelId,
    l.model_name AS ModelName,
    l.serial_no AS SerialNo,
    l.condition,
    l.model_year AS ModelYear,
    l.hours,
    l.tons,
    l.capacity_kg AS CapacityKg,
    l.horsepower,
    l.primary_intent AS PrimaryIntent,
    l.price,
    l.currency,
    l.price_unit AS PriceUnit,
    l.includes_operator AS IncludesOperator,
    l.seller_id AS SellerId,
    s.seller_type AS SellerType,
    l.city_id AS CityId,
    city.name AS CityName,
    l.district_id AS DistrictId,
    d.name AS DistrictName,
    l.neighborhood_id AS NeighborhoodId,
    n.name AS NeighborhoodName,
    l.specs::text AS SpecsJson,
    l.status,
    l.rejection_reason AS RejectionReason
FROM listings l
JOIN categories c ON c.id = l.category_id
JOIN category_groups g ON g.id = c.group_id
JOIN brands b ON b.id = l.brand_id
JOIN sellers s ON s.id = l.seller_id
JOIN cities city ON city.id = l.city_id
JOIN districts d ON d.id = l.district_id
LEFT JOIN neighborhoods n ON n.id = l.neighborhood_id
WHERE l.ad_no = @AdNo
  AND s.account_id = @AccountId
  AND l.status IN ('pending_review', 'rejected', 'published')
LIMIT 1;
