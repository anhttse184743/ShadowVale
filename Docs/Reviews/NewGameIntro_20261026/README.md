# Bundled intro before new tutorial — 2026-10-26

Flow: title → Chơi mới → Chơi hướng dẫn → bundled 52.78-second intro (picture + original audio) → Map00_Tutorial. No tutorial gameplay runs during the movie. End or deliberate skip fades to black/loading before asynchronously loading the tutorial. Returning to a save, resuming, retrying tutorial steps, or choosing Bỏ qua hướng dẫn does not start the movie.

Player controls: Space pauses/resumes; hold Esc one second to skip. Input is isolated from the menu; closing the movie adds the existing input grace period before tutorial control. Native VideoPlayer prepares asynchronously, targets a 1920x1080 RenderTexture, draws with aspect-fit black bars, routes the embedded audio to one source, and releases player/audio/texture on end/skip/cancel. Errors return to the tutorial choice with a clear retry message; prepare/stall timeout avoids remaining indefinitely on black.

Video is bundled in StreamingAssets/Video, so the built game does not depend on G:/game/Capstone or an internet connection. Source and normalized derivative hashes are recorded separately in video-source.json. The user supplied this video; no new paid generation or third-party video was added. The original H.264 Main export produced a Unity timestamp warning; a bundled H.264 baseline derivative without B-frames is used instead, at the same dimensions/frame rate and with the original sequence and soundtrack. Original file is unchanged. Implementation API reference: https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Video.VideoPlayer.html .

Map 1 ambience: wind 0.15 → 0.12 and birds 0.095 → 0.075, about another 20% quieter. Existing shelter muting and dialogue ducking retained. No Map 2 audio changes.

Validation: Unity 6000.3.24f1 on Windows/D3D11, 7/7 targeted tests passed across three final runs:
- Actual normalized video decodes non-black 1920x1080 frames, exposes an AAC audio track routed to the audio source, freezes playback on pause, resumes and completes naturally exactly once. RenderTexture frame captured in actual-frame.png.
- Actual title → new game → tutorial choice leaves gameplay unloaded during intro; repeated skip requests lead to one initialized tutorial and no remaining video player.
- Injected decoder error returns to the tutorial choice and cleans up video/audio/texture.
- Existing skip-tutorial title flow still enters Map 1 without a movie.
- Three ambience import/playback/pause/shelter tests still pass at lower levels.

An initial combined run with the original H.264 Main export showed a timestamp warning and a native ReadFileCache/scene-loading crash. The original remains untouched; the bundled derivative uses H.264 baseline/no B-frames, and tutorial loading now uses LoadSceneAsync. Final playback and tutorial transition runs passed without that warning or crash. This does not establish the initial native crash's exact root cause or constitute an exhaustive whole-game replay.
