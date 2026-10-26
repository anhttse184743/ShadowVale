# Map 1 ambient wind + birds — 2026-10-26

Added a quiet continuous rustling wind bed and three short bird calls, randomized with 18–34-second spacing. No music or AI alert noise. Speech lowers the ambient mix; pause/menu/suspension pauses it; release to Map 2 stops it.

Unity 6000.3.24f1, EditMode with play-mode transitions, **3/3 passed**:
- Imported four clips at 48 kHz and confirmed asynchronous audio data finishes loading.
- Real playback: wind starts, speech reduces its volume, bird call plays, pause freezes both playback positions, resume advances both, transition stops both.
- Actual Map 1 scene initializes one ambience component and two sources; repeated installation does not duplicate them.

PCM masters measured wind RMS 0.0849 and birds 0.0989–0.0998 before runtime gains (0.20/0.13). Wind seam overlaps 3 seconds; bird edges fade. Preview renders the same normal-play gains. This is not an exhaustive whole-game replay or a claim of regional species authenticity.

Two initial test harness issues were corrected: a deliberately unconfigured mission logged initialization errors, and the automatically opened title menu paused the isolated audio fixture. Clip sample inspection was replaced with import/load verification because compressed-in-memory clips do not support GetData; original WAV levels are recorded in sources.json. Final tests contain no suppressed gameplay errors.

Sources and rebuild script: SourceArt/Audio/Map01_Ambience_20261026. Map 2 audio and all voice files untouched. Existing saves need no conversion. No paid assets/services.
