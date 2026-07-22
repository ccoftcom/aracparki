SELECT
    city.id,
    city.name,
    city.slug,
    COALESCE(cnt.listing_count, 0)::int AS ListingCount
FROM cities city
LEFT JOIN (
    SELECT city_id, COUNT(*)::int AS listing_count
    FROM listings
    WHERE status = 'published'
    GROUP BY city_id
) cnt ON cnt.city_id = city.id
WHERE city.is_popular = TRUE
ORDER BY city.sort_order, city.name;
