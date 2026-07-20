SELECT d.id, d.name, d.city_id AS CityId, c.name AS CityName
FROM districts d
JOIN cities c ON c.id = d.city_id
WHERE d.city_id = ANY (@CityIds)
ORDER BY c.name, d.name;
