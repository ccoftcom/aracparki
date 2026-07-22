SELECT
    l.ad_no AS AdNo,
    GREATEST(l.updated_at, l.listed_at) AS LastModified
FROM listings l
WHERE l.status = 'published'
ORDER BY l.id
LIMIT @Take OFFSET @Skip;
