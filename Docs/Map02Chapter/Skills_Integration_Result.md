# Map 2 skill integration

Map 2 now owns PlayerSkills lifecycle and uses the existing SkillBalance and shared character HUD/inventory.

- GunKilled awards Shooting only for a lethal player gun hit against a Map02ChapterEnemy in this chapter.
- Knife special melee awards Stealth only for an unalerted, living enemy approached from the rear within the skill-adjusted range and with an unobstructed line of sight. A corpse cannot be used again; the special action has a 0.9-second cooldown.
- Existing recoil and reload modifiers are supplied by PlayerSkills on Nam. Shared bench/field repair reads the same component and awards Engineering for a paid full repair.
- Entering Map 2 keeps the carried player skill component and campaign ID. Direct scene entry initializes a new campaign. Legacy snapshots receive a deterministic campaign ID on restoration.
- Each chapter phase is a skill mission baseline. Completion commits its temporary combat XP; failure rolls Shooting and Stealth back, while Engineering remains permanent. The career failure journal also prevents an older snapshot from recovering XP from an already failed attempt.
- Snapshot/restore retains XP, skill mission and campaign ID. Level-up messages use the Map 2 HUD.

Validation: Map02SkillsTests.MapTwoAwardsCombatSkillsAndRestoresCareerAcrossFailure passed in Unity, covering actual lethal gun traces, front/rear knife eligibility, corpse rejection, paid bench repair and duplicate repair rejection, shared field repair cap, snapshot restoration, death rollback and older-save handling. Results: Temp/map02-skills-tests.xml.
