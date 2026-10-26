# Map 1 ambient wind + birds — 2026-10-26

Added a quiet continuous rustling wind bed and three short bird calls, randomized with 18–34-second spacing. No music or AI alert noise. Speech lowers the ambient mix; pause/menu/suspension pauses it; release to Map 2 stops it.

Unity 6000.3.24f1, EditMode with play-mode transitions, **3/3 passed**:
- Imported four clips at 48 kHz and confirmed asynchronous audio data finishes loading.
- Real playback: wind starts, speech reduces its volume, bird call plays, pause freezes both playback positions, resume advances both, transition stops both.
- Actual Map 1 scene initializes one ambience component and two sources; repeated installation does not duplicate them.

PCM masters measured wind RMS 0.0849 and birds 0.0989–0.0998 before runtime gains (0.20/0.13). Wind seam overlaps 3 seconds; bird edges fade. Preview renders the same normal-play gains. This is not an exhaustive whole-game replay or a claim of regional species authenticity.

Two initial test harness issues were corrected: a deliberately unconfigured mission logged initialization errors, and the automatically opened title menu paused the isolated audio fixture. Clip sample inspection was replaced with import/load verification because compressed-in-memory clips do not support GetData; original WAV levels are recorded in sources.json. Final tests contain no suppressed gameplay errors.

Sources and rebuild script: SourceArt/Audio/Map01_Ambience_20261026. Map 2 audio and all voice files untouched. Existing saves need no conversion. No paid assets/services.

## Shelter/volume revision

Wind gain reduced 0.20 → 0.15; birds 0.13 → 0.095 (about 25% quieter). The authored underground room now mutes birds and fades wind to zero, then pauses playback. Outdoor ground above the roof remains audible. No new assets or license changes.

Unity final 3/3 passed (Tests-shelter.xml), including real Map 1: enter room → wind/calls silent and stopped; move above roof → wind audible; re-enter → silent; leave floor footprint → audible. Existing playback, speech ducking and pause/resume tests still pass. The teleport test also suspends player locomotion while its CharacterController is disabled; no gameplay errors were suppressed.
