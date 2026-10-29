using ShadowVale.AI.Perception;
using System;
using System.Collections.Generic;
using System.Linq;
using ShadowVale.AI.Agents;
using ShadowVale.AI.Boss;
using ShadowVale.Gameplay.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace ShadowVale.Map01
{
    /// <summary>
    /// The C3 post's props built at start: the kẻng, the field telephone and its wire, and the Mosin cache.
    /// </summary>
    public sealed partial class Map01C3Mission
    {
        // ---- props -----------------------------------------------------------------------------------------------------

        private Material[] materials;
        /// <summary>A material of the approved C3 art by name (weathered timber, black steel, canvas), so the props match it.</summary>
        private Material Mat(string name, Color fallback)
        {
            materials ??= GameObject.Find("C3 / Mission art")?.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials)
                .Where(m => m != null).Distinct().ToArray() ?? Array.Empty<Material>();
            var found = materials.FirstOrDefault(m => m.name.StartsWith(name, StringComparison.Ordinal));
            if (found != null) return found;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            return new Material(shader) { color = fallback, name = name + " (stand-in)" };
        }
        private static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 scale, Material material, Quaternion? rotation = null, bool solid = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition; go.transform.localScale = scale; go.transform.localRotation = rotation ?? Quaternion.identity;
            if (!solid) Destroy(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }
        private ForestPoint Point(GameObject host, string id, ForestPointKind kind, string label, float radius)
        {
            var point = host.AddComponent<ForestPoint>();
            point.id = id; point.kind = kind; point.label = label; point.radius = radius; point.restockSeconds = 0f;
            point.items = Array.Empty<ForestIngredient>();
            mission.RegisterPoint(point);
            return point;
        }

        /// <summary>The kẻng: an iron bar hung from a timber frame inside the gate, the striker hanging beside it.</summary>
        private void BuildKieng()
        {
            var timber = Mat("WeatheredTimber", new Color(.36f, .27f, .18f));
            var steel = Mat("BlackSteel", new Color(.12f, .12f, .12f));
            var rope = Mat("Linen", new Color(.62f, .56f, .42f));
            var root = new GameObject("C3 kẻng").transform;
            root.position = At(Data.kieng.frame[0], Data.kieng.frame[1]);
            root.rotation = Quaternion.Euler(0, 0, 0);
            Part(PrimitiveType.Cube, "Kẻng post", root, new Vector3(-.5f, .95f, 0), new Vector3(.11f, 1.9f, .11f), timber, solid: true);
            Part(PrimitiveType.Cube, "Kẻng post", root, new Vector3(.5f, .95f, 0), new Vector3(.11f, 1.9f, .11f), timber, solid: true);
            Part(PrimitiveType.Cube, "Kẻng beam", root, new Vector3(0, 1.86f, 0), new Vector3(1.2f, .1f, .1f), timber);
            Part(PrimitiveType.Cylinder, "Kẻng rope", root, new Vector3(0, 1.66f, 0), new Vector3(.015f, .1f, .015f), rope);
            kiengSoundOrigin = Part(PrimitiveType.Cube, "Kẻng (iron bar)", root, new Vector3(0, 1.28f, 0), new Vector3(.07f, .56f, .07f), steel).transform;
            striker = Part(PrimitiveType.Cylinder, "Kẻng striker", root, new Vector3(.44f, 1.12f, .09f), new Vector3(.03f, .2f, .03f), steel, Quaternion.Euler(0, 0, 12));
            var use = new GameObject("Kẻng striker point");
            use.transform.position = At(Data.kieng.use[0], Data.kieng.use[1]);
            kiengPoint = Point(use, KiengId, ForestPointKind.Sabotage, "Tháo dùi kẻng", 1.6f);
        }

        /// <summary>The field telephone line: a pole behind the rear fence, the wire to the headquarters' roof and on
        /// along the north path towards the jetty posts.</summary>
        private void BuildPhone()
        {
            var timber = Mat("WeatheredTimber", new Color(.36f, .27f, .18f));
            var wireMat = Mat("BlackSteel", new Color(.08f, .08f, .08f));
            var root = new GameObject("C3 telephone pole").transform;
            root.position = At(Data.phone.pole[0], Data.phone.pole[1]);
            Part(PrimitiveType.Cylinder, "Pole", root, new Vector3(0, 2.8f, 0), new Vector3(.18f, 2.8f, .18f), timber, solid: true);
            Part(PrimitiveType.Cube, "Cross arm", root, new Vector3(0, 5.35f, 0), new Vector3(.9f, .08f, .08f), timber);
            poleTop = root.position + new Vector3(.32f, 5.4f, 0);
            var hq = new Vector3(Data.phone.hqEnd[0], Data.phone.hqEnd[1], Data.phone.hqEnd[2]);
            hqWire = Wire("Wire to headquarters", poleTop, hq, wireMat);
            var far = At(Data.phone.far[0], Data.phone.far[1]) + Vector3.up * 5f;
            Wire("Wire to the jetty posts", poleTop + new Vector3(-.64f, 0, 0), far, wireMat);
            var use = new GameObject("Telephone line point");
            use.transform.position = At(Data.phone.use[0], Data.phone.use[1]);
            phonePoint = Point(use, PhoneId, ForestPointKind.Sabotage, "Cắt dây điện thoại", 1.6f);
        }
        private static LineRenderer Wire(string name, Vector3 a, Vector3 b, Material material)
        {
            var go = new GameObject(name);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material; line.widthMultiplier = .025f; line.numCapVertices = 0;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Sag(line, a, b);
            return line;
        }
        private static void Sag(LineRenderer line, Vector3 a, Vector3 b, float sag = .6f)
        {
            const int n = 12;
            line.positionCount = n;
            for (int i = 0; i < n; i++) { float t = i / (n - 1f); line.SetPosition(i, Vector3.Lerp(a, b, t) + Vector3.down * sag * 4f * t * (1 - t)); }
        }

        /// <summary>The scouts' Mosin and five rounds on a canvas sheet behind a rock at the south-east shoulder of the
        /// hill (nest A), shown from the commander order on.</summary>
        private void BuildCache()
        {
            cacheRoot = new GameObject("C3 scouts' Mosin cache");
            var spot = At(Data.mosinCache[0], Data.mosinCache[1]);
            cacheRoot.transform.position = spot;
            var nest = At(Data.nestA[0], Data.nestA[1]);
            var tower = Guard("tower");
            var facing = Vector3.ProjectOnPlane((tower != null ? tower.transform.position : At(Data.anchor[0], Data.anchor[1])) - nest, Vector3.up);
            if (facing.sqrMagnitude > .01f) cacheRoot.transform.rotation = Quaternion.LookRotation(facing);
            // A rock to rest the rifle on, copied from the map's own boulders.
            var boulder = FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name.StartsWith("05_MossBoulder", StringComparison.Ordinal) || t.name.StartsWith("01_BroadBoulder", StringComparison.Ordinal));
            if (boulder != null)
            {
                var rock = Instantiate(boulder.gameObject, nest + facing.normalized * .9f, Quaternion.Euler(0, 37f, 0), cacheRoot.transform);
                rock.name = "Nest rock";
                var bounds = Bounds(rock);
                if (bounds.size.y > .01f) rock.transform.localScale *= .75f / bounds.size.y;
            }
            var canvas = Mat("OliveCanvas", new Color(.33f, .35f, .22f));
            Part(PrimitiveType.Cube, "Canvas sheet", cacheRoot.transform, new Vector3(0, .01f, 0), new Vector3(1.35f, .02f, .62f), canvas);
            var catalog = Map01ItemCatalog.Load();
            Lay(catalog, "rifle_mosin", new Vector3(0, .03f, -.08f));
            Lay(catalog, "ammo_sniper", new Vector3(.45f, .03f, .2f));
            cachePoint = Point(cacheRoot, CacheId, ForestPointKind.Loot, "Mosin trinh sát giấu — 5 viên", 1.6f);
            cachePoint.items = Map01Logistics.For(mission).ContainerLoot(CacheId);
            cacheRoot.SetActive(false);
        }
        private void Lay(Map01ItemCatalog catalog, string id, Vector3 local)
        {
            var model = catalog != null ? catalog.Model(id) : null;
            if (model == null) return;
            var shown = Instantiate(model, cacheRoot.transform, false);
            shown.name = id;
            shown.transform.localScale *= catalog.Scale(id);
            foreach (var c in shown.GetComponentsInChildren<Collider>()) Destroy(c);
            Map01Drops.LayFlat(shown, cacheRoot.transform.TransformPoint(local));
        }
        private static Bounds Bounds(GameObject go)
        {
            var b = new Bounds(go.transform.position, Vector3.zero); bool first = true;
            foreach (var r in go.GetComponentsInChildren<Renderer>()) { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }
            return b;
        }
    }
}
