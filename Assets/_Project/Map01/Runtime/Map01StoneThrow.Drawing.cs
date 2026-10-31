using System.Linq;
using System.Collections;
using System.Collections.Generic;
using ShadowVale.AI.Perception;
using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>
    /// The aim's drawing: the arc, the landing ring and the hearing rings of the guards who would hear it.
    /// </summary>
    public sealed partial class Map01StoneThrow
    {
        // ---- Drawing -----------------------------------------------------------------------

        private LineRenderer NewLine(string name, float width)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>();
            line.transform.SetParent(transform, false);
            line.sharedMaterial = mission.trailMaterial;
            line.useWorldSpace = true;
            line.startWidth = line.endWidth = width;
            line.numCornerVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private void Show(bool on)
        {
            if (arcLine != null) arcLine.enabled = on;
            if (landingRing != null) landingRing.enabled = on;
            if (killRing != null) killRing.enabled = on && Item == "grenade_f1";
            foreach (var ring in rings) ring.enabled = on && rings.IndexOf(ring) < RingsShown;
            if (!on) RingsShown = 0;
        }

        private void Draw()
        {
            if (arcLine == null) { arcLine = NewLine("Stone arc", .05f); landingRing = NewLine("Stone landing", .1f); landingRing.loop = true; }
            if (killRing == null) { killRing = NewLine("Grenade kill ring", .1f); killRing.loop = true; }
            arcLine.positionCount = arc.Count;
            for (int i = 0; i < arc.Count; i++) arcLine.SetPosition(i, arc[i]);
            Tint(arcLine, ArcColour);
            // Where it comes down: a stone's spot, the smoke cloud's extent, the F1's wound ring (and kill ring inside).
            bool grenade = Item == "grenade_f1", smoke = Item == "smoke_bomb";
            Circle(landingRing, Landing, grenade ? WoundRadius : smoke ? SmokeRadius : .75f, grenade || smoke);
            Tint(landingRing, smoke ? RingSmoke : grenade || WouldHear > 0 ? RingHears : ArcColour);
            killRing.enabled = grenade;
            if (grenade) { Circle(killRing, Landing, KillRadius, true); Tint(killRing, RingKill); }

            // A stone: each guard in sight shows the ring he would hear it land in.
            int shown = 0;
            Vector3 eye = mission.player.position + Vector3.up * 1.5f;
            foreach (var guard in mission.Enemies)
            {
                if (Item != "stone") break;
                if (shown >= MaxRings) break;
                if (guard == null || !guard.Alive) continue;
                Vector3 at = guard.transform.position;
                if (Vector3.Distance(at, mission.player.position) > ringsWithin) continue;
                if (Physics.Linecast(eye, at + Vector3.up * 1.2f, mission.ObstructionMask, QueryTriggerInteraction.Ignore)) continue;
                if (rings.Count <= shown) { var ring = NewLine("Guard hearing ring", .07f); ring.loop = true; rings.Add(ring); }
                Circle(rings[shown], at, HearingRadius, true);
                Tint(rings[shown], Vector3.Distance(at, Landing) <= HearingRadius ? RingHears : RingIdle);
                shown++;
            }
            if(mission.MapTwo!=null&&Item=="stone")foreach(var guard in mission.MapTwo.Chapter.Enemies)
            {
                if(shown>=MaxRings)break;if(guard==null||guard.Dead||!guard.gameObject.activeInHierarchy)continue;
                var at=guard.transform.position;if(Vector3.Distance(at,mission.player.position)>ringsWithin)continue;
                if(Map01Smoke.Blocks(eye,at+Vector3.up*1.2f))continue;
                if(Physics.Linecast(eye,at+Vector3.up*1.2f,out var hit,mission.ObstructionMask,QueryTriggerInteraction.Ignore)&&hit.transform!=guard.transform&&!hit.transform.IsChildOf(guard.transform))continue;
                if(rings.Count<=shown){var ring=NewLine("Guard hearing ring",.07f);ring.loop=true;rings.Add(ring);}
                Circle(rings[shown],at,HearingRadius,true);Tint(rings[shown],Vector3.Distance(at,Landing)<=HearingRadius?RingHears:RingIdle);shown++;
            }
            RingsShown = shown;
            Show(true);
        }

        /// <summary>The line's colour. The shared tracer material is URP Unlit, which ignores vertex colours: each line gets
        /// its own instance (made once, on first use) with the colour in it, so rings can differ (the F1's red kill ring).</summary>
        private static void Tint(LineRenderer line, Color colour)
        {
            line.startColor = line.endColor = colour;
            var m = line.material;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", colour);
            if (m.HasProperty("_Color")) m.SetColor("_Color", colour);
        }

        private void Circle(LineRenderer line, Vector3 centre, float radius, bool followGround)
        {
            line.positionCount = RingPoints;
            for (int i = 0; i < RingPoints; i++)
            {
                float a = i * Mathf.PI * 2f / RingPoints;
                var p = centre + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * radius;
                if (followGround && Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out var hit, 8f, mission.ObstructionMask, QueryTriggerInteraction.Ignore))
                    p.y = hit.point.y;
                line.SetPosition(i, p + Vector3.up * .08f);
            }
        }
    }
}
