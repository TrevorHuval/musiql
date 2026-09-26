-- Recompute catalog.recording_popularity.score from its two sources.
--
-- ListenBrainz listener counts come from Last.fm-era listening (roughly
-- 2005-2013), so on their own they rank Nirvana above anything released since.
-- Deezer's rank is current but only fetched for the top tracks of popular
-- artists. Tracks with a Deezer rank therefore rank first, ordered by it, and
-- every other track follows in ListenBrainz order; listener counts stay well
-- under one million, so the offset keeps the two bands apart.

INSERT INTO catalog.recording_popularity (recording_id, score)
SELECT d.recording_id, 0
FROM catalog.recording_deezer d
WHERE NOT EXISTS (SELECT 1 FROM catalog.recording_popularity p WHERE p.recording_id = d.recording_id);

-- Only rows that have a listener count: a score is never reset from nothing.
UPDATE catalog.recording_popularity p
SET score = p.listeners
WHERE p.listeners IS NOT NULL AND p.score <> p.listeners
  AND NOT EXISTS (SELECT 1 FROM catalog.recording_deezer d WHERE d.recording_id = p.recording_id);

UPDATE catalog.recording_popularity p
SET score = 1000000 + d.deezer_rank
FROM catalog.recording_deezer d
WHERE d.recording_id = p.recording_id AND p.score <> 1000000 + d.deezer_rank;
