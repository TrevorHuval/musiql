-- Recompute catalog.recording_popularity.score (0 to 1,000,000) from two signals.
--
-- Classic: ListenBrainz unique listeners. Its data is Last.fm-era listening
-- (roughly 2005-2013), so it knows Bohemian Rhapsody but not God's Plan.
-- Scaled by square root against the most-listened track (~320k) so the long
-- tail is not flattened to zero.
--
-- Current: Deezer's rank for the track, which reflects streaming now but
-- crowds most hits into 900k-1M and leans toward Deezer's (largely French)
-- audience. Raised to the fourth power to spread that crowd, then weighted by
-- the artist's Deezer fan count on a log scale, so a regional streaming spike
-- counts for less than a global act's catalogue.
--
-- The score is the sum: a track strong on both (Bohemian Rhapsody) beats one
-- strong on either, and a current hit with no Last.fm history still ranks.

INSERT INTO catalog.recording_popularity (recording_id, score)
SELECT d.recording_id, 0
FROM catalog.recording_deezer d
WHERE NOT EXISTS (SELECT 1 FROM catalog.recording_popularity p WHERE p.recording_id = d.recording_id);

UPDATE catalog.recording_popularity p
SET score = s.score
FROM (
    SELECT p2.recording_id,
           round(500000 * (
               coalesce(power(d.deezer_rank / 1000000.0, 4)
                        * power(least(log(greatest(d.artist_fans, 1)) / 7.4, 1.0), 2), 0)
             + coalesce(sqrt(least(p2.listeners, 320000) / 320000.0), 0)
           ))::int AS score
    FROM catalog.recording_popularity p2
    LEFT JOIN catalog.recording_deezer d ON d.recording_id = p2.recording_id
) s
WHERE s.recording_id = p.recording_id AND p.score <> s.score;
