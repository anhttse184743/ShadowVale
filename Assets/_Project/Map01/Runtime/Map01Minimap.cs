using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>North-up scene survey, captured once; actors and objectives are live overlays.</summary>
    public sealed class Map01Minimap : MonoBehaviour
    {
        private Map01Mission mission;
        private Map01ObjectiveGuide guide;
        private Map01Quest quest;
        private Map01Scouting scouting;
        private Texture2D chart;
        private Texture2D arrow, circle, diamond, triangle, ring, square;
        private Texture2D disc, rim; // The round corner map's backing and frame, smooth-edged.
        private GUIStyle northLabel;
        private readonly List<Map01EnemyController> visibleEnemies = new List<Map01EnemyController>();
        private float nextSense;
        private Vector2 origin;
        private float span;
        public bool Ready => chart != null;
        public int VisibleEnemyCount => visibleEnemies.Count;

        private IEnumerator Start()
        {
            mission = GetComponent<Map01Mission>();
            guide = GetComponent<Map01ObjectiveGuide>();
            quest = GetComponent<Map01Quest>();
            scouting = GetComponent<Map01Scouting>();
            yield return null;
            if (!mission.IsInitialized) yield break;
            span = Mathf.Max(1, Mathf.Max(mission.mapMax.x - mission.mapMin.x, mission.mapMax.y - mission.mapMin.y));
            origin = (mission.mapMin + mission.mapMax) * .5f - Vector2.one * span * .5f;
            arrow = Symbol(0); circle = Symbol(1); diamond = Symbol(2); triangle = Symbol(3); ring = Symbol(4); square = Symbol(5);
            disc = Disc(256, 0); rim = Disc(256, .955f);
            CaptureChart();
        }

        private void CaptureChart()
        {
            var root = new GameObject("Minimap survey camera");
            var camera = root.AddComponent<Camera>();
            var rt = RenderTexture.GetTemporary(1024, 1024, 24);
            var previous = RenderTexture.active;
            try {
                camera.enabled = false;
                camera.orthographic = true; camera.orthographicSize = span * .5f;
                camera.aspect = 1; camera.nearClipPlane = .1f; camera.farClipPlane = 1200;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.23f, .25f, .15f);
                camera.cullingMask = ~LayerMask.GetMask("Player", "Enemy", "UI");
                camera.allowHDR = false; camera.allowMSAA = false;
                root.transform.SetPositionAndRotation(new Vector3(origin.x + span / 2, 600, origin.y + span / 2), Quaternion.Euler(90, 0, 0));
                camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                chart = new Texture2D(1024, 1024, TextureFormat.RGB24, false) { name = "Field map survey", wrapMode = TextureWrapMode.Clamp };
                chart.ReadPixels(new Rect(0, 0, 1024, 1024), 0, 0);
                var pixels = chart.GetPixels32();
                for (int i = 0; i < pixels.Length; i++) {
                    Color c = pixels[i];
                    float value = Mathf.Clamp01(c.grayscale * 1.4f);
                    var ink = Color.Lerp(new Color(.14f, .17f, .10f), new Color(.64f, .59f, .38f), value);
                    if (c.b > c.r * 1.15f && c.g > c.r * 1.05f) ink = Color.Lerp(ink, new Color(.23f, .38f, .34f), .6f);
                    pixels[i] = ink;
                }
                chart.SetPixels32(pixels); chart.Apply(false, true);
            } finally {
                camera.targetTexture = null; RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt); Destroy(root);
            }
        }

        private void Update()
        {
            if (mission == null || !mission.IsInitialized || Time.unscaledTime < nextSense) return;
            nextSense = Time.unscaledTime + .25f;
            visibleEnemies.Clear();
            foreach (var enemy in mission.Enemies) {
                if (enemy == null || !enemy.Alive) continue;
                Vector3 delta = enemy.transform.position - mission.player.position;
                if (delta.sqrMagnitude > 30 * 30 || Vector3.Angle(mission.gameCamera.transform.forward, delta) > 65) continue;
                var eye = mission.player.position + Vector3.up * 1.5f;
                var target = enemy.transform.position + Vector3.up * 1.2f;
                if (Physics.Linecast(eye, target, out var hit, mission.ObstructionMask | LayerMask.GetMask("Enemy"), QueryTriggerInteraction.Ignore)
                    && hit.transform != enemy.transform && !hit.transform.IsChildOf(enemy.transform)) continue;
                visibleEnemies.Add(enemy);
            }
        }

        public static Vector2 Project(Vector3 point, Rect world, Rect area) => new Vector2(
            area.x + (point.x - world.x) / world.width * area.width,
            area.yMax - (point.z - world.y) / world.height * area.height);

        /// <summary>
        /// The world square on show: the whole survey when expanded, else 90 m centred on Nam —
        /// even at the edge of the survey. Clamping the window there instead left the map still
        /// and sent his arrow wandering off-centre.
        /// </summary>
        public Rect View(bool expanded)
        {
            if (expanded) return new Rect(origin.x, origin.y, span, span);
            float view = Mathf.Min(span, 90);
            return new Rect(mission.player.position.x - view / 2, mission.player.position.z - view / 2, view, view);
        }

        /// <summary>
        /// The corner map is round (the disc inscribed in <paramref name="area"/>); the expanded map
        /// stays square. On the round map, markers out of the circle are hidden — the objective
        /// instead sits on the rim, pointing the way.
        /// </summary>
        public void Draw(Rect area, bool expanded, GUIStyle label)
        {
            if (!Ready) { GUI.Label(area, "Đang vẽ bản đồ…", label); return; }
            var world = View(expanded);
            var disk = expanded ? (Vector2?)null : area.center;
            float radius = area.width / 2;
            var old = GUI.color;
            if (disk != null) {
                GUI.color = new Color(.02f, .03f, .02f, .55f); // A dark halo keeps the rim readable over bright ground.
                GUI.DrawTexture(new Rect(area.x - 6, area.y - 6, area.width + 12, area.height + 12), disc);
            }
            DrawChart(area, world, disk);
            GUI.color = new Color(.85f, .79f, .52f, .16f);
            for (int i = 1; i < 6; i++) {
                float x = area.x + area.width * i / 6, y = area.y + area.height * i / 6;
                float halfX = disk == null ? area.height / 2 : Chord(radius, x - area.center.x);
                float halfY = disk == null ? area.width / 2 : Chord(radius, y - area.center.y);
                GUI.DrawTexture(new Rect(x, area.center.y - halfX, 1, halfX * 2), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(area.center.x - halfY, y, halfY * 2, 1), Texture2D.whiteTexture);
            }
            GUI.color = old;
            DrawCamps(area, world, label, disk);
            foreach (var enemy in visibleEnemies) if (enemy != null && enemy.Alive)
                Mark(area, world, enemy.transform.position, triangle, new Color(.85f, .22f, .13f), 15, false, disk);
            if (mission.hung != null) Mark(area, world, mission.hung.position, circle, new Color(.3f, .67f, .84f), 13, false, disk);
            if (guide != null && guide.HasTarget) {
                Vector2 target = Mark(area, world, guide.Target, diamond, new Color(1, .77f, .26f), 20, true, disk);
                var at = target + new Vector2(0, 24);
                if (disk != null && (at - area.center).magnitude > radius - 28) at = area.center + (at - area.center).normalized * (radius - 28);
                GUI.Label(new Rect(Mathf.Clamp(at.x - 22, area.x, area.xMax - 72), Mathf.Clamp(at.y - 12, area.y + 30, area.yMax - 25), 72, 24), Mathf.RoundToInt(guide.Distance) + " m", label);
            }
            var player = Project(mission.player.position, world, area);
            var matrix = Map01Hud.RotateGui(player, mission.player.eulerAngles.y);
            GUI.color = new Color(.98f, .93f, .72f);
            GUI.DrawTexture(new Rect(player.x - 11, player.y - 11, 22, 22), arrow);
            GUI.matrix = matrix; GUI.color = old;
            if (disk == null) { GUI.Label(new Rect(area.x + 7, area.y + 4, 90, 25), "BẮC ↑", label); return; }
            // The rim hides the chart's stepped edge (DrawInDisc draws it in thin strips).
            GUI.color = new Color(.78f, .66f, .38f, .95f);
            GUI.DrawTexture(new Rect(area.x - 3, area.y - 3, area.width + 6, area.height + 6), rim);
            GUI.color = old;
            if (northLabel == null) northLabel = new GUIStyle(label) { alignment = TextAnchor.UpperCenter, wordWrap = false };
            GUI.Label(new Rect(area.center.x - 40, area.y + 6, 80, 24), "BẮC", northLabel);
        }

        /// <summary>Half the chord of a circle of <paramref name="radius"/> at <paramref name="offset"/> from its centre.</summary>
        private static float Chord(float radius, float offset) => Mathf.Sqrt(Mathf.Max(0, radius * radius - offset * offset));

        /// <summary>
        /// The part of the survey inside <paramref name="world"/>; beyond it, plain ground. With a
        /// <paramref name="disk"/> centre, only what falls inside the circle inscribed in the area.
        /// </summary>
        private void DrawChart(Rect area, Rect world, Vector2? disk)
        {
            var old = GUI.color;
            GUI.color = new Color(.14f, .17f, .10f); // The survey's darkest ink.
            GUI.DrawTexture(area, disk == null ? Texture2D.whiteTexture : disc);
            GUI.color = old;
            float x0 = Mathf.Max(world.xMin, origin.x), x1 = Mathf.Min(world.xMax, origin.x + span);
            float z0 = Mathf.Max(world.yMin, origin.y), z1 = Mathf.Min(world.yMax, origin.y + span);
            if (x1 <= x0 || z1 <= z0) return;
            Vector2 min = Project(new Vector3(x0, 0, z1), world, area), max = Project(new Vector3(x1, 0, z0), world, area);
            var target = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            var uv = new Rect((x0 - origin.x) / span, (z0 - origin.y) / span, (x1 - x0) / span, (z1 - z0) / span);
            if (disk == null) GUI.DrawTextureWithTexCoords(target, chart, uv);
            else DrawInDisc(target, chart, uv, disk.Value, area.width / 2, 2);
        }

        /// <summary>
        /// Draws <paramref name="uv"/> of <paramref name="texture"/> into <paramref name="target"/>,
        /// clipped to a circle. IMGUI has no masks, so it goes in horizontal strips of
        /// <paramref name="strip"/> pixels, each cut to the circle's chord; the corner map's rim
        /// covers the small steps this leaves along the edge.
        /// </summary>
        private static void DrawInDisc(Rect target, Texture texture, Rect uv, Vector2 center, float radius, float strip)
        {
            float top = Mathf.Max(target.yMin, center.y - radius), bottom = Mathf.Min(target.yMax, center.y + radius);
            for (float y = top; y < bottom; y += strip)
            {
                float y1 = Mathf.Min(y + strip, bottom);
                float half = Chord(radius, (y + y1) / 2 - center.y);
                float x0 = Mathf.Max(target.xMin, center.x - half), x1 = Mathf.Min(target.xMax, center.x + half);
                if (x1 <= x0) continue;
                // Screen y grows downwards, texture v upwards: the target's top shows uv.yMax.
                float u0 = uv.x + (x0 - target.x) / target.width * uv.width, u1 = uv.x + (x1 - target.x) / target.width * uv.width;
                float vTop = uv.yMax - (y - target.y) / target.height * uv.height, vBottom = uv.yMax - (y1 - target.y) / target.height * uv.height;
                GUI.DrawTextureWithTexCoords(new Rect(x0, y, x1 - x0, y1 - y), texture, new Rect(u0, vBottom, u1 - u0, vTop - vBottom));
            }
        }

        /// <summary>
        /// While scouting, each camp not yet logged is only a rough circle (offset from the real
        /// spot, see Map01Scouting); a logged camp — and every camp once the scouting report is
        /// in — is marked where it really is.
        /// </summary>
        private void DrawCamps(Rect area, Rect world, GUIStyle label, Vector2? disk)
        {
            if (scouting == null) return;
            var old = GUI.color;
            foreach (var camp in scouting.Camps)
            {
                if (camp.Found || quest.Stage > Map01Quest.ScoutStage)
                {
                    var p = Mark(area, world, camp.Center, square, new Color(.78f, .2f, .12f), 14, false, disk);
                    if (Shows(area, p, disk, 14)) GUI.Label(new Rect(p.x + 9, p.y - 12, 60, 24), "DT" + camp.Number, label);
                }
                else if (quest.Stage == Map01Quest.ScoutStage)
                {
                    var c = Project(camp.ZoneCenter, world, area);
                    float size = scouting.ZoneRadius * 2 / world.width * area.width;
                    var zone = new Rect(c.x - size / 2, c.y - size / 2, size, size);
                    if (!zone.Overlaps(area)) continue;
                    if (disk != null) {
                        // Cut to the round map like the chart itself.
                        GUI.color = new Color(1f, .72f, .25f, .22f);
                        DrawInDisc(zone, circle, new Rect(0, 0, 1, 1), disk.Value, area.width / 2, 3);
                        GUI.color = new Color(1f, .72f, .25f, .85f);
                        DrawInDisc(zone, ring, new Rect(0, 0, 1, 1), disk.Value, area.width / 2, 3);
                        continue;
                    }
                    GUI.BeginGroup(area); // Clip the circle to the map frame.
                    GUI.color = new Color(1f, .72f, .25f, .22f);
                    GUI.DrawTexture(new Rect(zone.x - area.x, zone.y - area.y, size, size), circle);
                    GUI.color = new Color(1f, .72f, .25f, .85f);
                    GUI.DrawTexture(new Rect(zone.x - area.x, zone.y - area.y, size, size), ring);
                    GUI.EndGroup();
                }
            }
            GUI.color = old;
        }

        /// <summary>A marker of <paramref name="size"/> at <paramref name="p"/> fits on the map: inside
        /// the area, or with a <paramref name="disk"/> centre, inside its circle.</summary>
        private static bool Shows(Rect area, Vector2 p, Vector2? disk, float size) =>
            disk == null ? area.Contains(p) : (p - disk.Value).magnitude <= area.width / 2 - size / 2;

        private static Vector2 Mark(Rect area, Rect world, Vector3 position, Texture2D icon, Color color, float size, bool clamp, Vector2? disk)
        {
            var p = Project(position, world, area);
            if (!clamp && !Shows(area, p, disk, size)) return p;
            if (disk != null) {
                // Pinned to the rim, in the direction of the objective.
                float reach = area.width / 2 - size / 2 - 4;
                if ((p - disk.Value).magnitude > reach) p = disk.Value + (p - disk.Value).normalized * reach;
            }
            p.x = Mathf.Clamp(p.x, area.x + size / 2, area.xMax - size / 2);
            p.y = Mathf.Clamp(p.y, area.y + size / 2, area.yMax - size / 2);
            var old = GUI.color; GUI.color = Color.black;
            GUI.DrawTexture(new Rect(p.x - size / 2 - 1, p.y - size / 2 - 1, size + 2, size + 2), icon);
            GUI.color = color; GUI.DrawTexture(new Rect(p.x - size / 2, p.y - size / 2, size, size), icon); GUI.color = old;
            return p;
        }

        private static Texture2D Symbol(int kind)
        {
            var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[1024];
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++) {
                float u = (x + .5f) / 16 - 1, v = (y + .5f) / 16 - 1;
                float r = Mathf.Sqrt(u * u + v * v);
                bool on = kind == 4 ? r < .98f && r > .88f : kind == 5 ? Mathf.Abs(u) < .7f && Mathf.Abs(v) < .7f
                    : kind == 1 ? u * u + v * v < .7f : kind == 2 ? Mathf.Abs(u) + Mathf.Abs(v) < .95f && Mathf.Abs(u) + Mathf.Abs(v) > .55f
                    : kind == 3 ? v > -.7f && Mathf.Abs(u) < (.9f - v) * .5f && (v < -.4f || Mathf.Abs(u) > (.6f - v) * .5f)
                    : Mathf.Abs(u) < (.95f - v) * .48f && v > -.85f + (1 - Mathf.Abs(u)) * .5f;
                pixels[y * 32 + x] = on ? Color.white : Color.clear;
            }
            texture.SetPixels(pixels); texture.Apply(); return texture;
        }

        /// <summary>A white disc <paramref name="size"/> pixels across with a one-pixel soft edge; a
        /// ring from <paramref name="inner"/> (0..1 of the radius) outwards when that is above 0.</summary>
        private static Texture2D Disc(int size, float inner)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            float r = size / 2f;
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) {
                float d = new Vector2(x + .5f - r, y + .5f - r).magnitude / r;
                float a = Mathf.Clamp01((1 - d) * r);
                if (inner > 0) a *= Mathf.Clamp01((d - inner) * r);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            texture.SetPixels32(pixels); texture.Apply(); return texture;
        }

        private void OnDestroy()
        {
            foreach (var texture in new[] { chart, arrow, circle, diamond, triangle, ring, square, disc, rim }) if (texture != null) Destroy(texture);
        }
    }
}
