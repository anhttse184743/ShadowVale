# ShadowVale — Game Client (Unity)

Tactical stealth-survival game with quantum-inspired enemy squad coordination.
SEP490 Research-Based Learning capstone, FPT University.

## Requirements baseline

The authoritative scope is `ShadowVale_RBL_Capstone_Proposal.docx`. See [Requirements baseline and Idea comparison](Docs/SRS/RequirementsBaseline.md) for the normalized scope, source references and implementation gaps.

Target a playable **2–3 map demo** with **5–6 weapons** (the functional requirements also specify 5–6 weapon classes). Extra maps and a larger arsenal are stretch work. Required game systems include six-state enemy FSM with Retreat, silent takedowns, slot inventory, probabilistic loot, durability/repair, usage-based skills, Safe Camp and full-state JSON save/load. The squad coordinator, interchangeable solvers, content platform and anonymized telemetry remain required project deliverables.

The current task prioritizes **Map 1** with placeholder characters and defers web implementation. This scheduling choice does not remove the platform or research requirements. `Assets/_Project/Map01` is an isolated prototype: it is not yet integrated with the shared content/coordinator/save modules and must not be presented as satisfying the full proposal. Read [Map 1 status](Docs/Map01_Forest.md) before evaluating it.

| | |
|---|---|
| **Unity** | **6000.3.24f1** (Unity 6.3 LTS) — current version in ProjectSettings/ProjectVersion.txt; coordinate version changes across the team |
| Template | Universal 3D (URP 17.3). "2.5D" = 3D models + orthographic camera locked at (30, 45, 0) |
| Input | Input System 1.20 (`_Project/Settings/Input/ShadowVale.inputactions`) |
| Navigation | AI Navigation 2.0 (NavMesh for locomotion; tactical NavGraph of 20–100 nodes for the QUBO) |
| JSON | Newtonsoft (`com.unity.nuget.newtonsoft-json`), snake_case on the wire |
| Tests | Unity Test Framework 1.6 — `Assets/Tests/EditMode`, `Assets/Tests/PlayMode` |

Planned sibling services (stack specified by the official proposal; their repositories were not audited here): `shadowvale-backend` (Python FastAPI + PostgreSQL), `shadowvale-web` (React), `shadowvale-solver` (Python sidecar on `127.0.0.1:8001`).

## Getting started

```bash
git lfs install
git clone <this-repo> ShadowVale
```

Open the folder with Unity Hub using **6000.3.24f1**. First import takes a few minutes. Open `Assets/_Project/Scenes/00_Boot.unity` and press Play: the console must log `[Bootstrap] content bundle 1.0.0 loaded from fallback`.

## Layout

```
Assets/
  _Project/            everything the team makes (feature folders, see Scripts/)
    Scripts/<Module>/  one asmdef per module: Core, Data, Content, Networking, Persistence,
                       Telemetry, AI, Gameplay, UI, Debugging, Bootstrap, Editor
    Scenes/            00_Boot, 01_MainMenu, 02_SafeCamp, Maps/, Sandbox/ (not in build)
    Settings/          Rendering (URP), Input, Physics
  StreamingAssets/Content/   fallback_bundle.json + content.schema.json (offline fallback)
  ThirdParty/          asset packs — do not edit; every pack listed in ATTRIBUTIONS.md
  Tests/               EditMode / PlayMode
Docs/                  SRS, Architecture, ContentSchema, Experiments, Meeting-Notes (not imported by Unity)
```

Assembly dependency rule: `Data` has no UnityEngine reference; `AI` never references `Gameplay`
(it reads game state through interfaces in `AI/Contracts`). See [the requirements baseline](Docs/SRS/RequirementsBaseline.md); detailed architecture documentation remains a project deliverable.

## Conventions

- Namespaces `ShadowVale.<Module>.<Feature>`; one class per file; prefabs `Enemy_Grunt`; scenes `NN_Name` / `MapNN_Name`.
- Content ids in JSON are `snake_case` strings, never ordinal numbers.
- **Always commit `.meta` files.** Never edit anything under `Assets/ThirdParty`.
- Branches: `feature/<pkg>-<short-description>`, e.g. `feature/pkg3-fsm-patrol`. PRs into `develop`.
- Balance numbers (damage, HP, loot weights…) live in the content bundle, never in C#.

## Command line

```powershell
# compile + create/refresh scenes and build settings
& "C:\Program Files\Unity\Hub\Editor\6000.3.24f1\Editor\Unity.exe" -batchmode -quit -projectPath . -executeMethod ShadowVale.Editor.ProjectScaffold.Run -logFile scaffold.log

# EditMode tests
& "C:\Program Files\Unity\Hub\Editor\6000.3.24f1\Editor\Unity.exe" -batchmode -projectPath . -runTests -testPlatform EditMode -testResults TestResults.xml -logFile tests.log
```

## Layers

`Player`, `Enemy`, `Cover`, `Obstacle`, `Interactable`, `Projectile`, `NoiseTrigger`, `VisionBlocker`
(indices 6–13). `VisionBlocker` is separate from `Obstacle` on purpose: line-of-sight and pathfinding use different sets.
