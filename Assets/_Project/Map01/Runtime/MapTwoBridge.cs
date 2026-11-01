using System;
using System.Collections;
using System.Collections.Generic;
using ShadowVale.AI.Perception;
using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>
    /// What the shared character code (HUD, bag, menu, throws) needs from Map 2's mission. Map 2 lives in its own
    /// assembly on top of this one, so Map 1 sees it only through this contract; Map02Mission implements it.
    /// </summary>
    public interface IMapTwoHost
    {
        GameObject gameObject { get; }
        bool Ready { get; }
        bool Paused { get; }
        bool Cinematic { get; }
        /// <summary>The chapter's story has failed (World.story.failed).</summary>
        bool Failed { get; }
        /// <summary>0 none, 1 inventory, 2 journal, 3 map, 4 bench.</summary>
        int Panel { get; }
        Transform Player { get; }
        Camera GameCamera { get; }
        IBinoculars Binoculars { get; }
        bool InputBlocked { get; }
        ForestSettings CharacterSettings { get; }
        ForestBundle CharacterContent { get; }
        float Stamina { get; }
        string HudMessage { get; }
        bool HudMessageVisible { get; }
        List<ForestPoint> CharacterWorkpoints { get; }
        Material CharacterTrail { get; }
        bool CharacterGearLocked { get; }
        bool ScoutEscortActive { get; }
        /// <summary>The chapter's guards (may hold destroyed or dead ones, as the chapter's own list does).</summary>
        IReadOnlyList<IMapTwoGuard> Enemies { get; }
        void Say(string text);
        void SetCharacterPanel(int panel);
        void DropCharacterItem(string id, int count, float condition);
        int CharacterNoise(Vector3 point, float radius, NoiseKind kind);
        (int killed, int wounded) CharacterExplosion(Vector3 point);

        // The HUD (Map01Hud.MapTwo, Map01Hud.Objective).
        int Phase { get; }
        string PhaseName { get; }
        string ReservedMedicine { get; }
        bool BinocularsUp { get; }
        string TrackedTaskId { get; }
        string TrackedTaskTitle { get; }
        string ObjectiveText { get; }
        string Instruction { get; }
        /// <summary>The person following Nam, or null; <paramref name="role"/> is what the HUD calls them.</summary>
        Transform Companion(out string role);
        /// <summary>The pinned objective: where to stand, where it is, and its prompt.</summary>
        bool TrackedObjective(out Vector3 standAt, out Vector3 position, out string prompt);
        Vector3 CharacterSteerPoint { get; }
        string InteractionPrompt { get; }
        void DrawCharacterMap(Rect area, bool mini, GUIStyle label);
        void DrawCharacterObjectives(Rect area);
        void DrawCharacterPin(Vector2 at);
        void DrawCharacterAwareness(float width, float height, GUIStyle label);

        // The pause menu (ForestMenu).
        void SetPaused(bool value);
        bool CloseGameplayPanel();
        bool CanSave();
        string SaveBlockReason();
        bool SaveSlot(int slot, out string error);
        bool AutoSaveOnExit(out string error);
    }

    /// <summary>A Map 2 guard as Map 1's rifle grip and throws see him; Map02ChapterEnemy implements it.</summary>
    public interface IMapTwoGuard
    {
        GameObject gameObject { get; }
        Transform transform { get; }
        bool Dead { get; }
        bool Alerted { get; }
    }

    /// <summary>The sampan crossing into Map 2, started by Map01Extraction; Map02Arrival implements it.</summary>
    public interface IMapTwoArrival
    {
        void TakeParty(Map01Extraction extraction, Map01Mission mission);
        IEnumerator LoadVillage();
    }

    /// <summary>
    /// Static entry points Map 2 registers on load (Map02Bridge), so Map 1 can find, start or hand over to Map 2
    /// without referencing its assembly.
    /// </summary>
    public static class MapTwoBridge
    {
        /// <summary>Half the side of Map 2's square map, in metres (Map02Navigation.HalfSize).</summary>
        public const float MapHalfSize = 110;

        /// <summary>The scene's Map02Mission, or null.</summary>
        public static Func<Component> FindMission;
        /// <summary>Whether the scene runs Map 2's free exploration (a Map02Exploration exists).</summary>
        public static Func<bool> ExplorationOpen;
        /// <summary>Starts Map 2 from a save slot entry.</summary>
        public static Action<ForestSaveSlots.Entry> BeginGame;
        /// <summary>Adds the arrival component to the boat.</summary>
        public static Func<GameObject, IMapTwoArrival> AddArrival;

        /// <summary>The Map 2 mission on <paramref name="owner"/>, or null.</summary>
        public static Component MissionOn(GameObject owner) => owner.GetComponent<IMapTwoHost>() as Component;

        /// <summary>The host behind a component found by <see cref="MissionOn"/>, null once it is destroyed.</summary>
        public static IMapTwoHost Host(Component mission) => mission != null ? (IMapTwoHost)mission : null;

        /// <summary>Unity's null check for a guard seen through its interface (a destroyed guard is null).</summary>
        public static bool Gone(IMapTwoGuard guard) => guard as UnityEngine.Object == null;
    }
}
