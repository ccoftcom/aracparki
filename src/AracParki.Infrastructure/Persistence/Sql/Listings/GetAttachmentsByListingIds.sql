SELECT
    la.listing_id AS ListingId,
    a.id,
    a.name,
    a.slug
FROM listing_attachments la
JOIN attachments a ON a.id = la.attachment_id
WHERE la.listing_id = ANY(@ListingIds)
ORDER BY a.name;
