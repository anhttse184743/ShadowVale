# Map 1 — soft leaves and birds

Sources: Bamboo Creaking in Wind by Yoyodaman234, https://freesound.org/people/Yoyodaman234/sounds/335891/ ; Birds Chirping.wav by Lizardhood, https://freesound.org/people/Lizardhood/sounds/427040/ . Both pages explicitly license the recording CC0 1.0, https://creativecommons.org/publicdomain/zero/1.0/ . Saved page snapshots and SHA-256 values are included. Downloaded the public HQ previews; no paid generation or paid assets.

The birds are generic environmental texture; the recording does not establish a Vietnamese species/location. No such authenticity claim is made.

Processing: mono 48 kHz PCM16; high/low pass filtering; restrained RMS, compressor on wind, 3-second overlapped wind seam; three 5-second faded bird excerpts. See sources.json for exact hashes, source URLs, sample lengths and measured levels. Source masters are included.

Rebuild: Python + numpy + ffmpeg. Set SVL_FFMPEG to the executable and optionally SVL_GAME_ROOT, then run build_audio.py. This rebuilds audio only and preserves existing Unity metadata GUIDs.

Runtime: Map01Mission attaches Map01Ambience only in the Map 1 scene. Assets load asynchronously at scene start. Wind volume 0.20, bird volume 0.13 over the quiet mastered WAVs. Birds start after 10 seconds, then every 18–34 seconds, with no immediate clip repeat and modest stereo pan. New calls wait during cinematics/speech. Mix ramps down to 28% for DialogueVoice speech and 50% in cinematics, ramps back gently. Time scale, mission pause, menu and application suspension freeze playback and the bird timer. Disabling pauses; re-enabling resumes. ReleaseForNextMap stops both sources; all sources remain children of the Map 1 mission, never the carried player. Ambient sources never emit AI gameplay noise.

Map 2 audio, voices, game objectives and save payloads are unchanged. Existing saves acquire the ambience when Map 1 starts; no save migration is required.
