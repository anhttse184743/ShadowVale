using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ShadowVale.Map01.Editor
{
    /// <summary>
    /// ShadowVale > Map 1 > Dựng địa hình bao quanh: the land beyond Map 1's edge, so looking out of
    /// the map shows forest and hills instead of the empty background. A ring of ground continues the
    /// playable hills exactly at the edge and rises into a closed horizon well past the camera's
    /// reach (300 m); the river runs on out through it; and a forest covers the near part of it — the
    /// map's own plant prefabs next to the edge, simple merged trees further out. Scenery only: the
    /// invisible boundary keeps Nam in, the distant meshes cast no shadows, and none of it is under
    /// the environment the NavMesh is baked from.
    /// Rebuilding replaces what an earlier build made.
    /// </summary>
    public static class Map01Surroundings
    {
        private const string MapPath = "Assets/_Project/Scenes/Maps/Map 1.unity";
        private const string Folder = "Assets/_Project/Scenes/Maps/Map 1/Surroundings";
        private const string RootName = "03 Surroundings • outside the playable map";
        private const string EnvironmentName = "Detailed layout — reference routes and hills";
        private const string GroundName = "Continuous hills — 220 x 200";
        private const string Plants = "Assets/_Project/Art/Environment/ForestV2/Prefabs/";
        private const float Reach = 420f;       // How far past the edge the ground goes.
        private const float ForestReach = 190f; // How far past the edge trees grow.
        private const float Chunk = 90f;        // Merged forest meshes cover squares this size.
        private static float minX, maxX, minZ, maxZ;
        private static float[,] play; // The playable ground's heights, one per metre.

        [MenuItem("ShadowVale/Map 1/Dựng địa hình bao quanh")]
        public static void BuildFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        public static void BuildAndExit()
        {
            try { Build(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        public static void Build()
        {
            var scene = EditorSceneManager.GetActiveScene().path == MapPath ? EditorSceneManager.GetActiveScene() : EditorSceneManager.OpenScene(MapPath);
            foreach (var old in scene.GetRootGameObjects().Where(r => r.name == RootName).ToArray()) Object.DestroyImmediate(old);
            if (AssetDatabase.IsValidFolder(Folder)) AssetDatabase.DeleteAsset(Folder);
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();

            var env = GameObject.Find(EnvironmentName)?.transform ?? throw new Exception("Map 1 has no " + EnvironmentName);
            ReadPlayableGround(env.Find(GroundName)?.GetComponent<MeshFilter>() ?? throw new Exception("Map 1 has no " + GroundName));
            var root = new GameObject(RootName).transform;
            var random = new System.Random(4242);

            var ground = Save(GroundRing(), "Ground ring");
            Scenery(root, "Ground ring", ground, new[] { GroundMaterial() });
            var water = Save(RiverOut(), "River beyond the map");
            Scenery(root, "River beyond the map", water, new[] { AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Environment/Map01Detailed/River water.mat") });
            int trees = Forest(root, random);

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Could not save Map 1.");
            int forestVertices = root.GetComponentsInChildren<MeshFilter>().Where(m => m.name.StartsWith("Distant forest")).Sum(m => m.sharedMesh.vertexCount);
            Debug.Log($"[Map1 surroundings] ground {ground.vertexCount} vertices, {trees} trees ({root.Find("Near forest").childCount} plant prefabs next to the edge), distant forest {forestVertices} vertices");
        }

        private static void ReadPlayableGround(MeshFilter filter)
        {
            var mesh = filter.sharedMesh;
            var b = mesh.bounds;
            minX = Mathf.Round(b.min.x); maxX = Mathf.Round(b.max.x); minZ = Mathf.Round(b.min.z); maxZ = Mathf.Round(b.max.z);
            int nx = (int)(maxX - minX) + 1, nz = (int)(maxZ - minZ) + 1;
            var vertices = mesh.vertices;
            if (vertices.Length != nx * nz) throw new Exception($"The playable ground is not the 1 m grid expected ({vertices.Length} vertices).");
            play = new float[nx, nz];
            foreach (var v in vertices) play[Mathf.RoundToInt(v.x - minX), Mathf.RoundToInt(v.z - minZ)] = v.y;
        }

        /// <summary>The playable ground's height at a point on or inside its edge.</summary>
        private static float PlayHeight(float x, float z)
        {
            float fx = Mathf.Clamp(x - minX, 0, play.GetLength(0) - 1.001f), fz = Mathf.Clamp(z - minZ, 0, play.GetLength(1) - 1.001f);
            int ix = (int)fx, iz = (int)fz; float tx = fx - ix, tz = fz - iz;
            return Mathf.Lerp(Mathf.Lerp(play[ix, iz], play[ix + 1, iz], tx), Mathf.Lerp(play[ix, iz + 1], play[ix + 1, iz + 1], tx), tz);
        }

        private static float River(float z) => Mathf.Sin(z * .036f) * 9; // As laid out in the map.

        /// <summary>How far outside the playable edge a point is (0 on or inside it).</summary>
        private static float Outside(float x, float z) =>
            Vector2.Distance(new Vector2(x, z), new Vector2(Mathf.Clamp(x, minX, maxX), Mathf.Clamp(z, minZ, maxZ)));

        /// <summary>
        /// Out from the edge: the edge's own height, rising into hills (about 45 m at the far rim),
        /// with a river valley running on where the river leaves the map to the north and south.
        /// </summary>
        private static float Height(float x, float z)
        {
            float edge = PlayHeight(Mathf.Clamp(x, minX, maxX), Mathf.Clamp(z, minZ, maxZ));
            float d = Outside(x, z);
            if (d <= 0) return edge;
            float rise = 30 * Mathf.SmoothStep(0, 1, d / 260f) + 6 * Mathf.SmoothStep(0, 1, d / 60f);
            float hills = (Mathf.PerlinNoise(x * .011f + 31, z * .011f + 17) - .45f) * 22 + (Mathf.PerlinNoise(x * .045f + 5, z * .045f + 9) - .5f) * 3.5f;
            float h = edge + (rise + hills * Mathf.SmoothStep(0, 1, d / 70f));
            if (z < minZ || z > maxZ)
            {
                float bank = Mathf.Abs(x - River(z));
                h = Mathf.Lerp(Mathf.Min(h, edge + 2), h, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(12, 70, bank))); // The valley.
                h = Mathf.Lerp(-1.7f, h, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(4.5f, 13, bank)));                // The channel.
            }
            return h;
        }

        /// <summary>Grid lines: every metre across the map (so the ring meets the playable ground
        /// vertex for vertex), then further apart the further out.</summary>
        private static List<float> Lines(float lo, float hi)
        {
            var outer = new List<float>();
            float step = 1, d = 0;
            while (d < Reach) { d += step; outer.Add(d); step = d < 6 ? 1 : d < 30 ? 2 : d < 80 ? 5 : d < 180 ? 10 : 20; }
            var lines = outer.Select(o => lo - o).Reverse().ToList();
            for (float v = lo; v <= hi + .01f; v += 1) lines.Add(v);
            lines.AddRange(outer.Select(o => hi + o));
            return lines;
        }

        private static Mesh GroundRing()
        {
            var xs = Lines(minX, maxX); var zs = Lines(minZ, maxZ);
            var index = new Dictionary<(int, int), int>();
            var vertices = new List<Vector3>(); var uvs = new List<Vector2>(); var triangles = new List<int>();
            int Vertex(int i, int j)
            {
                if (index.TryGetValue((i, j), out int n)) return n;
                float x = xs[i], z = zs[j];
                vertices.Add(new Vector3(x, Height(x, z), z)); uvs.Add(new Vector2(x, z) / 8f);
                return index[(i, j)] = vertices.Count - 1;
            }
            for (int i = 0; i + 1 < xs.Count; i++)
                for (int j = 0; j + 1 < zs.Count; j++)
                {
                    // Squares on the playable ground are left to it.
                    if (xs[i] >= minX && xs[i + 1] <= maxX && zs[j] >= minZ && zs[j + 1] <= maxZ) continue;
                    int a = Vertex(i, j), b = Vertex(i + 1, j), c = Vertex(i, j + 1), d = Vertex(i + 1, j + 1);
                    triangles.AddRange(new[] { a, c, b, b, c, d });
                }
            var mesh = new Mesh { name = "Ground ring", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uvs); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh RiverOut()
        {
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            foreach (var (from, to) in new[] { (minZ - Reach, minZ + 1), (maxZ - 1, maxZ + Reach) })
                for (float z = from; z < to; z += 4)
                {
                    int n = vertices.Count;
                    foreach (var p in new[] { new Vector2(River(z) - 6, z), new Vector2(River(z) + 6, z), new Vector2(River(z + 4) - 6, z + 4), new Vector2(River(z + 4) + 6, z + 4) })
                        vertices.Add(new Vector3(p.x, -.25f, p.y));
                    triangles.AddRange(new[] { n, n + 2, n + 1, n + 1, n + 2, n + 3 });
                }
            var mesh = new Mesh { name = "River beyond the map" };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
            mesh.SetUVs(0, vertices.Select(v => new Vector2(v.x, v.z) * .12f).ToList());
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// The playable ground's colour — its forest-floor green, with the same speckle (see the
        /// map's GroundTexture) — as a small seamless tile, so the ring does not read as a new surface.
        /// </summary>
        private static Material GroundMaterial()
        {
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGB24, true) { name = "Surrounding ground", wrapMode = TextureWrapMode.Repeat };
            float span = size / 4.65f; // Pixels per metre as on the playable ground.
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size;
                    // Four offset samples blended so the tile wraps without a seam.
                    float Noise(float a, float b) => Mathf.PerlinNoise(a * span * .6f + 80, b * span * .6f + 80);
                    float n = Mathf.Lerp(Mathf.Lerp(Noise(u, v), Noise(u - 1, v), u), Mathf.Lerp(Noise(u, v - 1), Noise(u - 1, v - 1), u), v);
                    tex.SetPixel(x, y, new Color(.23f, .27f, .12f) * (.78f + n * .4f));
                }
            tex.Apply();
            AssetDatabase.CreateAsset(tex, Folder + "/Surrounding ground.asset");
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Surrounding ground", enableInstancing = true };
            material.SetTexture("_BaseMap", tex);
            material.SetFloat("_Smoothness", .1f);
            // One tile per 8 m of UV (GroundRing): 256 px over 55 m, as on the map.
            material.SetTextureScale("_BaseMap", Vector2.one * (8f / span));
            AssetDatabase.CreateAsset(material, Folder + "/Surrounding ground.mat");
            return material;
        }

        private static Mesh Save(Mesh mesh, string name)
        {
            AssetDatabase.CreateAsset(mesh, $"{Folder}/{name}.asset");
            return mesh;
        }

        private static void Scenery(Transform root, string name, Mesh mesh, Material[] materials)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.Off; // Distant scenery: shadows are the costly part.
        }

        private const float NearBand = 28f; // Out to here, the map's own plant prefabs; beyond, simple trees.
        private const int VertexBudget = 400000;

        /// <summary>
        /// Trees on a jittered grid out to <see cref="ForestReach"/>, thinning outwards. Next to the
        /// edge, where Nam can look at them from a few metres, they are the map's own plant prefabs
        /// (with their LODs, as inside the map) with shrubs under them; further out, where the fog
        /// starts to take them, simple low-poly trees merged per <see cref="Chunk"/>-metre square.
        /// The plant models are far too detailed to merge by the thousand (about 12k vertices each).
        /// </summary>
        private static int Forest(Transform root, System.Random random)
        {
            float R(float a, float b) => Mathf.Lerp(a, b, (float)random.NextDouble());
            string[] trees = { "BroadleafTall_A", "BroadleafTall_B", "SpreadingTree", "BroadleafTall_A", "BroadleafTall_B", "SpreadingTree", "Sapling", "BambooClump" };
            string[] shrubs = { "CoverShrub_A", "CoverShrub_B", "Fern", "TallGrass" };
            var near = new GameObject("Near forest").transform;
            near.SetParent(root, false);
            void Plant(string name, Vector3 at, float yaw, float scale)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Plants + name + ".prefab") ?? throw new Exception("Missing plant " + name);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, near);
                go.transform.SetPositionAndRotation(at, Quaternion.Euler(0, yaw, 0));
                go.transform.localScale = Vector3.one * scale;
            }
            var leaves = new[] { Flat("Distant leaves", new Color(.15f, .25f, .09f)), Flat("Distant leaves dark", new Color(.11f, .19f, .07f)) };
            var bark = Flat("Distant bark", new Color(.22f, .16f, .10f));
            var chunkVertices = new Dictionary<(int, int), List<Vector3>[]>();
            var chunkTriangles = new Dictionary<(int, int), List<int>[]>();
            int count = 0;
            for (float x = minX - ForestReach; x <= maxX + ForestReach; x += 7)
                for (float z = minZ - ForestReach; z <= maxZ + ForestReach; z += 7)
                {
                    float px = x + R(-3, 3), pz = z + R(-3, 3);
                    float d = Outside(px, pz);
                    if (d < 4 || d > ForestReach) continue; // Clear of the invisible boundary; not past the forest.
                    if ((pz < minZ || pz > maxZ) && Mathf.Abs(px - River(pz)) < 13) continue;
                    if (random.NextDouble() > Mathf.Lerp(.9f, .5f, d / ForestReach)) continue; // Thinning out.
                    var at = new Vector3(px, Height(px, pz) - .05f, pz);
                    count++;
                    if (d < NearBand)
                    {
                        Plant(trees[random.Next(trees.Length)], at, R(0, 360), R(.8f, 1.25f));
                        for (int s = 0; s < 2; s++)
                        {
                            float sx = px + R(-3.5f, 3.5f), sz = pz + R(-3.5f, 3.5f);
                            if (Outside(sx, sz) >= 3 && random.NextDouble() < .7)
                                Plant(shrubs[random.Next(shrubs.Length)], new Vector3(sx, Height(sx, sz) - .05f, sz), R(0, 360), R(.75f, 1.2f));
                        }
                        continue;
                    }
                    var key = (Mathf.FloorToInt(px / Chunk), Mathf.FloorToInt(pz / Chunk));
                    if (!chunkVertices.ContainsKey(key))
                    {
                        chunkVertices[key] = new[] { new List<Vector3>(), new List<Vector3>(), new List<Vector3>() };
                        chunkTriangles[key] = new[] { new List<int>(), new List<int>(), new List<int>() };
                    }
                    int crown = random.Next(2);
                    LowPolyTree(chunkVertices[key][crown], chunkTriangles[key][crown], chunkVertices[key][2], chunkTriangles[key][2], at, R(.8f, 1.3f), R(0, 360), random);
                }
            int vertices = 0;
            foreach (var key in chunkVertices.Keys)
            {
                var mesh = new Mesh { name = $"Distant forest {key.Item1},{key.Item2}", indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(chunkVertices[key][0].Concat(chunkVertices[key][1]).Concat(chunkVertices[key][2]).ToList());
                mesh.subMeshCount = 3;
                int offset = 0;
                for (int s = 0; s < 3; s++)
                {
                    mesh.SetTriangles(chunkTriangles[key][s].Select(t => t + offset).ToList(), s);
                    offset += chunkVertices[key][s].Count;
                }
                mesh.RecalculateNormals(); mesh.RecalculateBounds();
                vertices += mesh.vertexCount;
                if (vertices > VertexBudget) throw new Exception($"Distant forest too heavy: over {VertexBudget} vertices.");
                Scenery(root, mesh.name, Save(mesh, mesh.name), new[] { leaves[0], leaves[1], bark });
            }
            return count;
        }

        private static Material Flat(string name, Color color)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, enableInstancing = true };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", .05f);
            AssetDatabase.CreateAsset(material, $"{Folder}/{name}.mat");
            return material;
        }

        private static readonly Vector3[] Icosahedron = IcosahedronVertices();
        private static readonly int[] IcosahedronFaces = {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1 };

        private static Vector3[] IcosahedronVertices()
        {
            float t = (1 + Mathf.Sqrt(5)) / 2;
            return new[] {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1) }
                .Select(v => v.normalized).ToArray();
        }

        /// <summary>A tapering five-sided trunk under one or two lumpy icosahedron crowns: about 30 vertices.</summary>
        private static void LowPolyTree(List<Vector3> crownVertices, List<int> crownTriangles, List<Vector3> trunkVertices, List<int> trunkTriangles,
            Vector3 at, float scale, float yaw, System.Random random)
        {
            float R(float a, float b) => Mathf.Lerp(a, b, (float)random.NextDouble());
            var turn = Quaternion.Euler(0, yaw, 0);
            float height = R(3, 5) * scale, radius = .22f * scale;
            int b = trunkVertices.Count;
            for (int k = 0; k < 5; k++)
            {
                float a = k * Mathf.PI * 2 / 5;
                var ring = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                trunkVertices.Add(at + ring * radius);
                trunkVertices.Add(at + ring * radius * .6f + Vector3.up * height);
            }
            for (int k = 0; k < 5; k++)
            {
                int a0 = b + k * 2, a1 = a0 + 1, b0 = b + (k + 1) % 5 * 2, b1 = b0 + 1;
                trunkTriangles.AddRange(new[] { a0, a1, b0, b0, a1, b1 });
            }
            int crowns = random.NextDouble() < .5 ? 2 : 1;
            for (int c = 0; c < crowns; c++)
            {
                float size = c == 0 ? 1 : .7f;
                float rx = R(2.2f, 3.6f) * scale * size, ry = R(1.8f, 3f) * scale * size;
                var centre = at + Vector3.up * (height + ry * .55f) + turn * new Vector3(c == 0 ? 0 : rx * .6f, c == 0 ? 0 : -ry * .3f, 0);
                int o = crownVertices.Count;
                foreach (var v in Icosahedron) crownVertices.Add(centre + turn * Vector3.Scale(v * R(.85f, 1.15f), new Vector3(rx, ry, rx)));
                foreach (int i in IcosahedronFaces) crownTriangles.Add(o + i);
            }
        }
    }
}
