using System.Collections.Generic;
using ShadowVale.Gameplay.Enemies.Boss;
using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>
    /// The commander's grenades, readable before they go off (2026-10-22; they used to arrive as a grey ball with
    /// nothing said). From the moment one leaves his hand:
    /// <list type="bullet">
    /// <item>its blast is marked on the ground round the spot it is thrown at: a red ring as wide as the blast (the
    /// colour of the kill ring Nam sees when he aims his own F1), and a second ring that grows from the middle and
    /// meets the first as it goes off;</item>
    /// <item>it is an F1 in the air, turning over with its fuse smoking, instead of the bare sphere.</item>
    /// </list>
    /// Where it is and how long is left, on the HUD, is Map01Hud.Grenade.cs. The thrower and the blast are the shared
    /// <see cref="Grenade"/>; nothing here changes when or where it goes off.
    /// </summary>
    public sealed class Map01GrenadeWarning : MonoBehaviour
    {
        private const int RingPoints = 48;
        /// <summary>The ring's red, as the F1's kill ring (Map01StoneThrow).</summary>
        public static readonly Color DangerRed = new Color(.95f, .2f, .12f, .95f);

        private sealed class Mark
        {
            public LineRenderer edge, fill;
            public Vector3[] ground;   // the edge ring laid on the ground once, at the throw
            public GameObject look;    // the F1 that follows the shared grenade's invisible sphere
            public Vector3 spin;      // the axis it turns over about in the air
        }

        private readonly Dictionary<Grenade, Mark> marks = new Dictionary<Grenade, Mark>();
        private readonly List<Grenade> gone = new List<Grenade>();
        private Map01Mission mission;

        /// <summary>The warning shown for <paramref name="grenade"/> (tests read its rings).</summary>
        public bool Marks(Grenade grenade, out LineRenderer edge, out LineRenderer fill)
        {
            edge = fill = null;
            if (grenade == null || !marks.TryGetValue(grenade, out var mark)) return false;
            edge = mark.edge; fill = mark.fill;
            return true;
        }

        private void Awake() => mission = GetComponent<Map01Mission>();

        private void LateUpdate()
        {
            if (mission == null) return;
            foreach (var grenade in Grenade.Live)
                if (grenade != null && !marks.ContainsKey(grenade)) marks[grenade] = Begin(grenade);
            gone.Clear();
            foreach (var pair in marks)
            {
                if (pair.Key == null || pair.Key.Exploded) { gone.Add(pair.Key); continue; }
                Follow(pair.Key, pair.Value);
            }
            foreach (var grenade in gone) { End(marks[grenade]); marks.Remove(grenade); }
        }

        private void OnDestroy()
        {
            foreach (var mark in marks.Values) End(mark);
            marks.Clear();
        }

        private Mark Begin(Grenade grenade)
        {
            var mark = new Mark { edge = Line("Grenade danger ring", .14f), fill = Line("Grenade fuse ring", .08f) };
            mark.ground = new Vector3[RingPoints];
            for (int i = 0; i < RingPoints; i++) mark.ground[i] = OnGround(grenade.Target, grenade.Radius, i);
            mark.edge.positionCount = RingPoints; mark.edge.SetPositions(mark.ground);
            mark.fill.positionCount = RingPoints;
            Tint(mark.edge, DangerRed); Tint(mark.fill, DangerRed);

            // The F1 in place of the sphere, on a root of its own: the shared grenade is scaled down to its size, which
            // would shrink the fuse smoke with it.
            var sphere = grenade.GetComponent<Renderer>();
            if (sphere != null) sphere.enabled = false;
            mark.look = Map01StoneThrow.CreateHeldVisual("grenade_f1", "Commander's grenade");
            mark.look.transform.SetPositionAndRotation(grenade.transform.position, Random.rotation);
            Map01StoneThrow.FuseSmoke(mark.look.transform);
            mark.spin = Random.onUnitSphere;
            // No caption line: the HUD's banner says it, and the caption box would cover the ground round Nam just
            // when he has to see the ring to get out of it.
            return mark;
        }

        private void Follow(Grenade grenade, Mark mark)
        {
            // Burning down: the inner ring reaches the edge as it goes off, and both beat faster towards the end.
            float burnt = grenade.TotalSeconds > 0 ? Mathf.Clamp01(1f - grenade.SecondsToBlast / grenade.TotalSeconds) : 1f;
            float beat = .55f + .45f * Mathf.Abs(Mathf.Sin(Time.time * Mathf.Lerp(5f, 16f, burnt)));
            for (int i = 0; i < RingPoints; i++)
                mark.fill.SetPosition(i, OnGround(grenade.Target, Mathf.Max(.25f, grenade.Radius * burnt), i));
            Tint(mark.edge, new Color(DangerRed.r, DangerRed.g, DangerRed.b, DangerRed.a * beat));
            Tint(mark.fill, new Color(DangerRed.r, DangerRed.g, DangerRed.b, .5f + .45f * burnt));

            if (mark.look == null) return;
            mark.look.transform.position = grenade.transform.position + Vector3.up * (grenade.Landed ? .06f : 0f);
            if (!grenade.Landed) mark.look.transform.Rotate(mark.spin, 540f * Time.deltaTime, Space.World);
        }

        private static void End(Mark mark)
        {
            foreach (var line in new[] { mark.edge, mark.fill })
                if (line != null) { Destroy(line.material); Destroy(line.gameObject); }
            if (mark.look != null) Destroy(mark.look);
        }

        private Vector3 OnGround(Vector3 centre, float radius, int i)
        {
            float a = i * Mathf.PI * 2f / RingPoints;
            var p = centre + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * radius;
            if (Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out var hit, 8f, mission.ObstructionMask, QueryTriggerInteraction.Ignore))
                p.y = hit.point.y;
            return p + Vector3.up * .08f;
        }

        private LineRenderer Line(string name, float width)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>();
            line.transform.SetParent(transform, false);
            line.sharedMaterial = mission.trailMaterial;
            line.useWorldSpace = true;
            line.loop = true;
            line.startWidth = line.endWidth = width;
            line.numCornerVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        /// <summary>As Map01StoneThrow's: the shared tracer material is URP Unlit and ignores vertex colours, so each
        /// line carries its colour in its own material instance.</summary>
        private static void Tint(LineRenderer line, Color colour)
        {
            line.startColor = line.endColor = colour;
            var m = line.material;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", colour);
            if (m.HasProperty("_Color")) m.SetColor("_Color", colour);
        }
    }
}
