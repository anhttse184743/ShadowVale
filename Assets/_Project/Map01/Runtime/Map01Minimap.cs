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

        public void Draw(Rect area, bool expanded, GUIStyle label)
        {
            if (!Ready) { GUI.Label(area, "Đang vẽ bản đồ…", label); return; }
            var world = View(expanded);
            DrawChart(area, world);
            var old = GUI.color;
            GUI.color = new Color(.85f, .79f, .52f, .16f);
            for (int i = 1; i < 6; i++) {
                GUI.DrawTexture(new Rect(area.x + area.width * i / 6, area.y, 1, area.height), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(area.x, area.y + area.height * i / 6, area.width, 1), Texture2D.whiteTexture);
            }
            GUI.color = old;
            DrawCamps(area, world, label);
            foreach (var enemy in visibleEnemies) if (enemy != null && enemy.Alive)
                Mark(area, world, enemy.transform.position, triangle, new Color(.85f, .22f, .13f), 15, false);
            if (mission.hung != null) Mark(area, world, mission.hung.position, circle, new Color(.3f, .67f, .84f), 13, false);
            if (guide != null && guide.HasTarget) {
                Vector2 target = Mark(area, world, guide.Target, diamond, new Color(1, .77f, .26f), 20, true);
                GUI.Label(new Rect(Mathf.Clamp(target.x - 22, area.x, area.xMax - 72), Mathf.Clamp(target.y + 12, area.y + 30, area.yMax - 25), 72, 24), Mathf.RoundToInt(guide.Distance) + " m", label);
            }
            var player = Project(mission.player.position, world, area);
            var matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(mission.player.eulerAngles.y, player);
            GUI.color = new Color(.98f, .93f, .72f);
            GUI.DrawTexture(new Rect(player.x - 11, player.y - 11, 22, 22), arrow);
            GUI.matrix = matrix; GUI.color = old;
            GUI.Label(new Rect(area.x + 7, area.y + 4, 90, 25), "BẮC ↑", label);
        }

        /// <summary>The part of the survey inside <paramref name="world"/>; beyond it, plain ground.</summary>
        private void DrawChart(Rect area, Rect world)
        {
            var old = GUI.color;
            GUI.color = new Color(.14f, .17f, .10f); // The survey's darkest ink.
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = old;
            float x0 = Mathf.Max(world.xMin, origin.x), x1 = Mathf.Min(world.xMax, origin.x + span);
            float z0 = Mathf.Max(world.yMin, origin.y), z1 = Mathf.Min(world.yMax, origin.y + span);
            if (x1 <= x0 || z1 <= z0) return;
            Vector2 min = Project(new Vector3(x0, 0, z1), world, area), max = Project(new Vector3(x1, 0, z0), world, area);
            GUI.DrawTextureWithTexCoords(Rect.MinMaxRect(min.x, min.y, max.x, max.y), chart,
                new Rect((x0 - origin.x) / span, (z0 - origin.y) / span, (x1 - x0) / span, (z1 - z0) / span));
        }

        /// <summary>
        /// While scouting, each camp not yet logged is only a rough circle (offset from the real
        /// spot, see Map01Scouting); a logged camp — and every camp once the scouting report is
        /// in — is marked where it really is.
        /// </summary>
        private void DrawCamps(Rect area, Rect world, GUIStyle label)
        {
            if (scouting == null) return;
            var old = GUI.color;
            foreach (var camp in scouting.Camps)
            {
                if (camp.Found || quest.Stage > Map01Quest.ScoutStage)
                {
                    var p = Mark(area, world, camp.Center, square, new Color(.78f, .2f, .12f), 14, false);
                    if (area.Contains(p)) GUI.Label(new Rect(p.x + 9, p.y - 12, 60, 24), "DT" + camp.Number, label);
                }
                else if (quest.Stage == Map01Quest.ScoutStage)
                {
                    var c = Project(camp.ZoneCenter, world, area);
                    float size = scouting.ZoneRadius * 2 / world.width * area.width;
                    var zone = new Rect(c.x - size / 2, c.y - size / 2, size, size);
                    if (!zone.Overlaps(area)) continue;
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

        private static Vector2 Mark(Rect area, Rect world, Vector3 position, Texture2D icon, Color color, float size, bool clamp)
        {
            var p = Project(position, world, area);
            if (!clamp && !area.Contains(p)) return p;
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

        private void OnDestroy()
        {
            foreach (var texture in new[] { chart, arrow, circle, diamond, triangle, ring, square }) if (texture != null) Destroy(texture);
        }
    }
}
