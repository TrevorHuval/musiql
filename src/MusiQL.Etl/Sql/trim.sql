-- Optional trim for small hosts: keep only artists with genre signal, either
-- on the artist itself or summed across the release groups they are credited
-- on. Everything downstream joins through staging.artist, so removing an
-- artist here drops its release groups, releases and recordings as well.
-- {min_votes} is substituted by the loader.

CREATE TABLE staging.genre_tag AS
SELECT DISTINCT t.id
FROM staging.tag t
JOIN staging.genre g ON lower(g.name) = lower(t.name);
CREATE UNIQUE INDEX ON staging.genre_tag (id);

CREATE TABLE staging.keep_artist AS
SELECT at.artist AS id
FROM staging.artist_tag at
JOIN staging.genre_tag gt ON gt.id = at.tag
GROUP BY at.artist
HAVING sum(at.count) >= {min_votes}
UNION
SELECT acn.artist
FROM staging.release_group_tag rt
JOIN staging.genre_tag gt ON gt.id = rt.tag
JOIN staging.release_group rg ON rg.id = rt.release_group
JOIN staging.artist_credit_name acn ON acn.artist_credit = rg.artist_credit
GROUP BY acn.artist
HAVING sum(rt.count) >= {min_votes};
CREATE UNIQUE INDEX ON staging.keep_artist (id);

DELETE FROM staging.artist a
WHERE NOT EXISTS (SELECT 1 FROM staging.keep_artist k WHERE k.id = a.id);
