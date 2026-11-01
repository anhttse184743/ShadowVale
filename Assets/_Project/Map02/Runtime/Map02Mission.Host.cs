using System.Collections.Generic;
using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>
    /// Map 2's side of <see cref="IMapTwoHost"/>: what the shared character code in Map 1's assembly reads from
    /// this mission. Members whose names already match are implemented by the mission itself; the rest are here.
    /// </summary>
    public sealed partial class Map02Mission : IMapTwoHost
    {
        bool IMapTwoHost.Failed => World.story.failed;
        IBinoculars IMapTwoHost.Binoculars => Chapter;
        List<ForestPoint> IMapTwoHost.CharacterWorkpoints => CharacterWorkpoints;
        bool IMapTwoHost.ScoutEscortActive => Chapter?.ScoutEscortActive == true;
        IReadOnlyList<IMapTwoGuard> IMapTwoHost.Enemies => Chapter.Enemies;

        int IMapTwoHost.Phase => Chapter.State.phase;
        string IMapTwoHost.PhaseName => Map02ChapterState.PhaseName(Chapter.State.phase);
        string IMapTwoHost.ReservedMedicine => Chapter.ReservedMedicine;
        bool IMapTwoHost.BinocularsUp => Chapter.Binoculars;
        string IMapTwoHost.TrackedTaskId => Chapter.TrackedTask?.id;
        string IMapTwoHost.TrackedTaskTitle => Chapter.TrackedTask?.title;
        string IMapTwoHost.Instruction => Chapter.Instruction;

        Transform IMapTwoHost.Companion(out string role)
        {
            var person = Chapter.Companion;
            role = person != null ? person.role : null;
            return person != null ? person.transform : null;
        }

        bool IMapTwoHost.TrackedObjective(out Vector3 standAt, out Vector3 position, out string prompt)
        {
            var goal = TrackedObjective;
            if (goal == null) { standAt = position = Vector3.zero; prompt = null; return false; }
            position = goal.transform.position;
            standAt = goal.standPoint != null ? goal.standPoint.position : position;
            prompt = goal.prompt;
            return true;
        }
    }
}
