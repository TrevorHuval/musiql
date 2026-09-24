TRUNCATE catalog.recording_genre,
         catalog.release_group_genre,
         catalog.artist_genre,
         catalog.recording,
         catalog.release,
         catalog.release_group,
         catalog.genre,
         catalog.artist;

INSERT INTO catalog.artist (id, mbid, name, sort_name, begin_year, end_year)
SELECT id, mbid, name, sort_name, begin_year, end_year FROM staging.artist_out;

INSERT INTO catalog.genre (id, mbid, name)
SELECT id, mbid, name FROM staging.genre_out;

INSERT INTO catalog.release_group (id, mbid, name, artist_id, primary_type, first_release_year)
SELECT id, mbid, name, artist_id, primary_type, first_release_year FROM staging.release_group_out;

-- catalog.release is left empty: queries resolve tracks to albums through
-- release_group, and the table was ~1 GB of rows nothing reads.

INSERT INTO catalog.recording (id, mbid, name, artist_id, length_ms, release_group_id, first_release_year)
SELECT id, mbid, name, artist_id, length_ms, release_group_id, first_release_year FROM staging.recording_out;

INSERT INTO catalog.artist_genre (artist_id, genre_id, votes)
SELECT artist_id, genre_id, votes FROM staging.artist_genre_out;

INSERT INTO catalog.release_group_genre (release_group_id, genre_id, votes)
SELECT release_group_id, genre_id, votes FROM staging.release_group_genre_out;

INSERT INTO catalog.recording_genre (recording_id, genre_id, votes)
SELECT recording_id, genre_id, votes FROM staging.recording_genre_out;
