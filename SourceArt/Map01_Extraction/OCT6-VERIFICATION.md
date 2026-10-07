# Short paddle stroke and seated covering fire — 2026-10-06

Published to G:/game/ShadowVale. Validation clone synchronized from the current
main project before testing, including the current character models. Runtime and
test file SHA-256 matches are recorded in oct6-published-sha256.json.

## Changes

- Hung's hands travel forward/back through a short 20 cm target range, always in
  front of his body; recovery lift is 3.5 cm. Chest twist is reduced to 5–9 degrees.
  The existing skeleton, character assets and terrain remain unchanged.
- Cover fire now applies health damage and plays real enemy deaths. Previously,
  the departure path emitted visual traces but never damaged its target.
- Aim selection uses shoulder height, avoiding the low seated chest point that
  incorrectly treated the jetty steps/deck as blocking every target.
- Nearby living guards pursue along NavMesh routes during the cinematic. Fallback
  pursuers move to the exposed bank instead of staying behind the jetty deck.
- Nam and the commander aim with both arms while remaining seated, acquire targets
  with a short blend, fire independent bursts with recoil/flash/tracer/audio, and
  lower their weapons when targets are gone. World geometry and passengers still
  block shots. No off-screen enemies are killed merely because the map ends.
- Cinematic damage does not consume player ammunition or award additional loot.

## Verification

Unity 6000.3.24f1: 5/5 ExtractionTests passed (oct6-tests.xml).

- Natural departure with fallback pursuers: Nam 3 shots, commander 3 shots, 2 kills.
- Existing living guards on the bank: Nam 3 shots, commander 3 shots, 2 kills;
  magazine remains 30 and neither cinematic kill creates loot.
- Extraction save/reload, holding Escape, completion after departure, standing
  calibration and held-aim restoration remain covered by the passing suite.
- Reviewed close views of the short rowing stroke and active seated fire, including
  tracer/flash frames. No scene or Map 2 file changed; git diff --check passes.

Videos: hung-short-stroke-oct6.mp4, seated-cover-fire-oct6.mp4,
map1-extraction-oct6.mp4. These are silent camera previews, not GUI/audio captures.
Screenshot sampling is slower than real-time rendering; repeated preview frames
preserve the scene timing (18-second departure) and do not add motion detail.
The hand/aim correction is runtime post-retargeting; source FBX takes are unchanged.
