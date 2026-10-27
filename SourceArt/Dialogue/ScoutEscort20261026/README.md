# Thành radio warning after rescuing Tâm

Cue: m2v3_scoutescort_warning_01. Source text is read verbatim from Map02Chapter.json.

Compared the existing Anh Thành radio cues (antenna, withdrawal, coordinates and sacrifice) and the installed CommanderB20261025 recipe. All use the approved built-in Minh Đức preset. Reused VieNeu 3.8.0 v3 Turbo, ONNX fp32, temperature 0.65; tempo 0.92, pitch 0.94; the same clean filters and 320–3200 Hz radio filters. No voice cloning, paid service or changes to existing recordings.

The new 48 kHz mono PCM16 radio file is installed in Assets/Resources/Dialogue/VI and registered in both runtime and source catalogs. Existing strict caption/radio matching now resolves the recording without a playback bypass. The rescue warning keeps its once-per-save trigger and allows player control.

reference-analysis.json records measured reference waveforms and hashes. integration.json records the new clip's duration, levels and hash. These are technical checks, not a claim of listening approval. Raw/clean/radio masters are retained here; build_voice.py reproduces the single cue. The isolated runtime/model cache lives under Tools/TamVoiceRuntime and is ignored by Git.
