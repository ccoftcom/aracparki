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
    l.price_unit AS PriceUnit,
    l.cover_image_url AS CoverImageUrl,
    s.seller_type AS SellerType,
    s.is_verified AS IsVerified,
    l.listed_at AS ListedAt
FROM listings l
JOIN categories c ON c.id = l.category_id
JOIN brands b ON b.id = l.brand_id
JOIN cities city ON city.id = l.city_id
JOIN districts d ON d.id = l.district_id
JOIN sellers s ON s.id = l.seller_id
WHERE l.status = 'published'
  AND (@Intent = 'all' OR l.intents @> ARRAY[@Intent]::text[])
  AND (@CategoryId IS NULL OR l.category_id = @CategoryId)
  AND (@Category IS NULL OR c.name = @Category OR c.slug = @Category)
  AND (@BrandId IS NULL OR l.brand_id = @BrandId)
  AND (@ModelId IS NULL OR l.model_id = @ModelId)
  AND (NOT @HasCityFilter OR l.city_id = ANY (@CityIds))
  AND (@City IS NULL OR city.name = @City)
  AND (NOT @HasDistrictFilter OR l.district_id = ANY (@DistrictIds))
  AND (@Condition IS NULL OR l.condition = @Condition)
  AND (@SellerType IS NULL OR s.seller_type = @SellerType)
  AND (@YearMin IS NULL OR l.model_year >= @YearMin)
  AND (@YearMax IS NULL OR l.model_year <= @YearMax)
  AND (@HoursMin IS NULL OR l.hours >= @HoursMin)
  AND (@HoursMax IS NULL OR l.hours <= @HoursMax)
  AND (@WeightMin IS NULL OR l.tons >= @WeightMin)
  AND (@WeightMax IS NULL OR l.tons <= @WeightMax)
  AND (@PriceMin IS NULL OR l.price >= @PriceMin)
  AND (@PriceMax IS NULL OR l.price <= @PriceMax)
  AND (@HorsepowerMin IS NULL OR l.horsepower >= @HorsepowerMin)
  AND (@HorsepowerMax IS NULL OR l.horsepower <= @HorsepowerMax)
  AND (@CapacityKgMin IS NULL OR COALESCE(l.capacity_kg, -1) >= @CapacityKgMin)
  AND (@CapacityKgMax IS NULL OR COALESCE(l.capacity_kg, 2147483647) <= @CapacityKgMax)
  AND (@IncludesOperator IS NULL OR l.includes_operator = @IncludesOperator)
  AND (@PriceUnit IS NULL OR l.price_unit = @PriceUnit)
  AND (NOT @VerifiedOnly OR s.is_verified = TRUE)
  AND (
        NOT @HasAttachments
        OR EXISTS (
          SELECT 1 FROM listing_attachments la
          WHERE la.listing_id = l.id
            AND la.attachment_id = ANY (@AttachmentIds)
        )
      )
  AND (@SpecsFilterJson IS NULL OR l.specs @> CAST(@SpecsFilterJson AS jsonb))
  AND (
        @SpecMinJson IS NULL
        OR NOT EXISTS (
          SELECT 1
          FROM jsonb_each_text(CAST(@SpecMinJson AS jsonb)) j
          WHERE COALESCE((l.specs ->> j.key)::numeric, -1) < j.value::numeric
        )
      )
  AND (
        @Query IS NULL
        OR l.title ILIKE ('%' || @Query || '%')
        OR l.ad_no ILIKE ('%' || @Query || '%')
        OR l.model_name ILIKE ('%' || @Query || '%')
        OR COALESCE(l.serial_no, '') ILIKE ('%' || @Query || '%')
        OR b.name ILIKE ('%' || @Query || '%')
      )
ORDER BY l.hours ASC, l.id ASC
LIMIT @Take OFFSET @Skip;
