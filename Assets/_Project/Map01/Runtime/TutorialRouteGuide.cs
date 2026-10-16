using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace ShadowVale.Map01
{
    /// <summary>Temporary ground chevrons for the current lesson. Never edits the level or its navigation.</summary>
    [RequireComponent(typeof(TutorialDirector))]
    public sealed class TutorialRouteGuide : MonoBehaviour
    {
        private const float Spacing = 1.8f, VisibleDistance = 48;
        private static readonly int Tint = Shader.PropertyToID("_Tint");
        private readonly List<MeshRenderer> arrows = new();
        private readonly List<Mesh> arrowMeshes = new();
        private readonly List<float> distances = new();
        private readonly List<Collider> floors = new();
        private MaterialPropertyBlock properties;
        private NavMeshPath path;
        private TutorialDirector director;
        private Map01Mission mission;
        private Transform root;
        private Material material;
        private Mesh mesh;
        private Vector3[] baseVertices, groundVertices;
        private Vector3 previousFrom, previousTarget;
        private TutorialStep previousStep;
        private float nextRepath;
        private bool wasVisible;
        public int VisibleCount { get; private set; }
        public Vector3 Target { get; private set; }
        public bool HasPath { get; private set; }

        private void Start()
        {
            director = GetComponent<TutorialDirector>(); mission = GetComponent<Map01Mission>();
            properties = new MaterialPropertyBlock(); path = new NavMeshPath();
            root = new GameObject("Tutorial route chevrons").transform; root.SetParent(transform, false);
            material = new Material(Resources.Load<Shader>("TutorialRoute")) { name = "Tutorial route light", hideFlags = HideFlags.DontSave };
            mesh = MakeChevron();
            baseVertices = mesh.vertices; groundVertices = new Vector3[baseVertices.Length];
            // Only real walking surfaces receive arrows, never bushes, characters, roofs or water.
            foreach (var collider in FindObjectsByType<Collider>(FindObjectsSortMode.None)) {
                string n = collider.name;
                if (collider is TerrainCollider || n == "Continuous hills — 220 x 200" || n == "Bank access ramp"
                    || n.IndexOf("deck", System.StringComparison.OrdinalIgnoreCase) >= 0) floors.Add(collider);
            }
        }
        private bool PickTarget(out Vector3 target)
        {
            if (!director.TryGetObjectiveTarget(out target, out var label)) return false;
            // Movement practice needs a direction even while standing at the current lesson's start.
            if (!director.AwaitingArrival && director.Step <= TutorialStep.Crouch) {
                int next = (int)director.Step + 1;
                if (next < director.lessonStarts.Length) target = director.lessonStarts[next].position;
            }
            if (!director.AwaitingArrival && director.Step == TutorialStep.StoneKnife && label != "BỤI CÂY — ẨN NẤP" && director.knifeGuard.Alive)
                target = director.knifeGuard.transform.position - director.knifeGuard.transform.forward * 1.5f;
            if (!director.AwaitingArrival && director.Step == TutorialStep.Shoot && director.shootingGuard.Alive)
                target = director.shootingGuard.transform.position;
            if (!director.AwaitingArrival && director.Step == TutorialStep.Squad) {
                float nearest = float.MaxValue;
                foreach (var enemy in director.squad) {
                    if (enemy == null || !enemy.Alive) continue;
                    float distance = (mission.player.position - enemy.transform.position).sqrMagnitude;
                    if (distance < nearest) { target = enemy.transform.position; nearest = distance; }
                }
            }
            return true;
        }
        private void LateUpdate()
        {
            if (mission == null || !mission.IsInitialized) return;
            bool visible = !mission.Stopped && !ForestMenu.Visible && !mission.InventoryOpen && !mission.MapOpen
                && director.Progress < 1 && PickTarget(out _);
            root.gameObject.SetActive(visible);
            if (!visible) { VisibleCount = 0; wasVisible = false; return; }
            PickTarget(out var target); Target = target;
            var from = mission.player.position;
            if (!wasVisible || director.Step != previousStep || (target - previousTarget).sqrMagnitude > .1f
                || Time.unscaledTime >= nextRepath && (from - previousFrom).sqrMagnitude > .25f) {
                Repath(from, target); previousFrom = from; previousTarget = target; previousStep = director.Step;
                nextRepath = Time.unscaledTime + .3f;
            }
            wasVisible = true; VisibleCount = distances.Count;
            for (int i = 0; i < distances.Count; i++) {
                // A light travels from Hùng towards the goal; adjacent chevrons light one after another.
                float trail = Mathf.Repeat(Time.unscaledTime * 6 - distances[i], Mathf.Min(9, distances.Count * Spacing));
                float light = Mathf.Pow(Mathf.Clamp01(1 - trail / 3), 2);
                properties.SetColor(Tint, Color.Lerp(new Color(.82f, .67f, .32f, .9f), new Color(1.8f, 1.5f, .78f, 1), light));
                arrows[i].SetPropertyBlock(properties);
            }
        }
        private void Repath(Vector3 from, Vector3 target)
        {
            distances.Clear(); HasPath = false;
            if (NavMesh.SamplePosition(from, out var start, 3, NavMesh.AllAreas)
                && NavMesh.SamplePosition(target, out var end, 3, NavMesh.AllAreas)
                && NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path)
                && path.status == NavMeshPathStatus.PathComplete && path.corners.Length >= 2) {
                HasPath = true;
                var corners = path.corners; float walked = 0, next = 1.6f;
                for (int i = 1; i < corners.Length && next <= VisibleDistance; i++) {
                    float length = Vector3.Distance(corners[i - 1], corners[i]);
                    while (length > .001f && next <= walked + length && next <= VisibleDistance) {
                        var p = Vector3.Lerp(corners[i - 1], corners[i], (next - walked) / length);
                        // Leave room around the goal for the actor or the crate itself.
                        if (Vector3.ProjectOnPlane(p - target, Vector3.up).magnitude > 1.3f
                            && Ground(p, out var hit)) {
                            var forward = Vector3.ProjectOnPlane(corners[i] - corners[i - 1], hit.normal).normalized;
                            if (forward.sqrMagnitude > .1f) {
                                var arrow = GetArrow(distances.Count);
                                arrow.transform.SetPositionAndRotation(hit.point + hit.normal * .055f, Quaternion.LookRotation(forward, hit.normal));
                                ConformArrow(arrow.transform, arrowMeshes[distances.Count]);
                                arrow.gameObject.SetActive(true); distances.Add(next);
                            }
                        }
                        next += Spacing;
                    }
                    walked += length;
                }
            }
            for (int i = distances.Count; i < arrows.Count; i++) arrows[i].gameObject.SetActive(false);
        }
        private bool Ground(Vector3 p, out RaycastHit best)
        {
            best = default; float nearest = float.MaxValue;
            var ray = new Ray(p + Vector3.up * 2.5f, Vector3.down);
            foreach (var floor in floors) {
                if (floor == null || !floor.enabled || !floor.gameObject.activeInHierarchy || !floor.Raycast(ray, out var hit, 5)) continue;
                float d = Mathf.Abs(hit.point.y - p.y);
                if (hit.normal.y > .55f && d < nearest) { best = hit; nearest = d; }
            }
            return nearest < 1.8f;
        }
        private MeshRenderer GetArrow(int index)
        {
            if (index < arrows.Count) return arrows[index];
            var go = new GameObject("Route > " + index, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(root, false);
            var surfaceMesh = Instantiate(mesh); surfaceMesh.name = "Grounded route chevron " + index;
            go.GetComponent<MeshFilter>().sharedMesh = surfaceMesh; arrowMeshes.Add(surfaceMesh);
            var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            arrows.Add(renderer); return renderer;
        }
        private void ConformArrow(Transform arrow, Mesh surface)
        {
            // A small grid lets the glow and frame follow slopes and the wooden ramp together.
            for (int i = 0; i < baseVertices.Length; i++) {
                var world = arrow.TransformPoint(baseVertices[i]);
                groundVertices[i] = Ground(world, out var hit) ? arrow.InverseTransformPoint(hit.point + hit.normal * .045f) : baseVertices[i];
            }
            surface.vertices = groundVertices; surface.RecalculateBounds();
        }
        private static Mesh MakeChevron()
        {
            var vertices = new Vector3[9]; var uv = new Vector2[9]; var triangles = new List<int>();
            for (int z = 0; z < 3; z++) for (int x = 0; x < 3; x++) {
                int i = z * 3 + x; uv[i] = new Vector2(x / 2f, z / 2f);
                vertices[i] = new Vector3((uv[i].x - .5f) * 1.9f, 0, (uv[i].y - .5f) * 1.8f);
                if (x < 2 && z < 2) triangles.AddRange(new[] { i, i + 3, i + 4, i, i + 4, i + 1 });
            }
            // The shader draws one continuous pointed shape, its inset core, frame and soft halo.
            var result = new Mesh { name = "Ground chevron", hideFlags = HideFlags.DontSave };
            result.vertices = vertices; result.uv = uv; result.SetTriangles(triangles, 0); result.RecalculateBounds(); return result;
        }
        private void OnDisable() { if (root != null) root.gameObject.SetActive(false); wasVisible = false; VisibleCount = 0; }
        private void OnDestroy()
        {
            if (root != null) Destroy(root.gameObject);
            if (material != null) Destroy(material);
            foreach (var arrowMesh in arrowMeshes) if (arrowMesh != null) Destroy(arrowMesh);
            if (mesh != null) Destroy(mesh);
        }
    }
}
