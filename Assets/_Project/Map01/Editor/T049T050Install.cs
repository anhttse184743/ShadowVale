using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ShadowVale.Map01.Editor
{
    /// <summary>
    /// Installs the director-approved round 2 (26/11/2026): T049 Nam arm motion replaces NamMotionV3, and the T050 sleeve weights
    /// (Blender working copy Nam_AoBaBa_TayAo_T050_Vai2_2026-11-26.blend) are copied onto the in-game body mesh. Only bone weights
    /// change: vertices, bind poses and bone order of BoundActive/Nam_AoBaBa_Character_Game.asset stay as they are. The weights come
    /// from an FBX export of that blend, matched to the game mesh vertex by vertex; the same export of the pre-T050 T038 blend must
    /// reproduce the current game weights, which proves the match. Both files are backed up outside Assets before writing.
    /// </summary>
    public static class T049T050Install
    {
        const string MotionGame = "Assets/_Project/Map01/Resources/Characters/NamMotionV3.asset";
        const string MotionNew = T049NamArmFix.Output;
        const string BodyGame = "Assets/_Project/Art/Characters/NamAoBaBa_20261111/BoundActive/Nam_AoBaBa_Character_Game.asset";
        const string Stage = "Assets/_Project/Art/Characters/NamAoBaBa_20261111/T050";
        const string FbxNew = Stage + "/Nam_AoBaBa_T050_Vai2.fbx", FbxOld = Stage + "/Nam_AoBaBa_T038_goc.fbx";
        const string Prefab = "Assets/_Project/Prefabs/Player/Player.prefab";
        const string BackupMotion = @"D:\DO_AN_GAME\chuyen_dong\GiaoViec_Codex\T049_Nam_TayK54Dao_v3\claude\sao_luu_truoc_khi_cai";
        const string BackupBody = @"D:\DO_AN_GAME\chuyen_dong\GiaoViec_Codex\T050_Nam_TayAo_TrongLuong\vong2\sao_luu_truoc_khi_cai";
        static string Report => Environment.GetEnvironmentVariable("T049_INSTALL_REPORT") ?? "Temp/T049T050Install.txt";

        public static void DryRunBatch() => Batch(false);
        public static void RunBatch() => Batch(true);

        static void Batch(bool write)
        {
            var log = new StringBuilder();
            int code = 0;
            try { Install(write, log); }
            catch (Exception e) { log.AppendLine("FAILED: " + e); code = 1; }
            File.WriteAllText(Report, log.ToString());
            Debug.Log("[T049T050Install]\n" + log);
            EditorApplication.Exit(code);
        }

        static void Install(bool write, StringBuilder log)
        {
            // --- T049 motion ---
            var gameMotion = AssetDatabase.LoadAssetAtPath<NamMotionData>(MotionGame);
            var newMotion = AssetDatabase.LoadAssetAtPath<NamMotionData>(MotionNew);
            if (!gameMotion || !newMotion) throw new Exception("Missing motion asset");
            log.AppendLine($"motion: game {gameMotion.bones.Length} tracks, T049 {newMotion.bones.Length} tracks");

            // --- T050 weights ---
            foreach (var p in new[] { FbxNew, FbxOld })
            {
                var imp = (ModelImporter)AssetImporter.GetAtPath(p) ?? throw new Exception("Missing staged " + p);
                if (!imp.isReadable || imp.materialImportMode != ModelImporterMaterialImportMode.None || imp.importAnimation || imp.animationType != ModelImporterAnimationType.Generic)
                {
                    imp.isReadable = true; imp.materialImportMode = ModelImporterMaterialImportMode.None; imp.importAnimation = false;
                    imp.animationType = ModelImporterAnimationType.Generic; imp.SaveAndReimport();
                }
            }
            var body = AssetDatabase.LoadAssetAtPath<Mesh>(BodyGame) ?? throw new Exception("Missing " + BodyGame);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
            var smr = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r => r.sharedMesh == body) ?? throw new Exception("Player.prefab has no renderer on the body mesh");
            var gameBones = smr.bones.Select(b => b ? b.name : null).ToArray();
            log.AppendLine($"game body: {body.vertexCount} verts, {gameBones.Length} bones on renderer '{smr.name}', bindposes {body.bindposes.Length}");

            var oldW = Mapped(FbxOld, body, gameBones, log, "T038 (pre-T050)");
            var newW = Mapped(FbxNew, body, gameBones, log, "T050 Vai2");
            var cur = body.boneWeights;
            float proof = Diff(cur, oldW, gameBones, out int proofCount);
            float change = Diff(cur, newW, gameBones, out int changeCount);
            log.AppendLine($"proof (T038 export vs game weights): {proofCount} verts differ > .02, max {proof:F3}");
            log.AppendLine($"change (T050 vs game weights): {changeCount} verts differ > .02, max {change:F3}");
            if (proofCount > body.vertexCount / 200) throw new Exception("Match is not proven: the pre-T050 export does not reproduce the game weights");

            if (!write) { log.AppendLine("dry run: nothing written"); return; }

            Backup(MotionGame, BackupMotion, log); Backup(BodyGame, BackupBody, log);
            EditorUtility.CopySerialized(newMotion, gameMotion);
            gameMotion.name = "NamMotionV3";
            EditorUtility.SetDirty(gameMotion);
            body.boneWeights = newW;
            EditorUtility.SetDirty(body);
            AssetDatabase.SaveAssets();
            AssetDatabase.DeleteAsset(Stage);
            log.AppendLine("written: NamMotionV3 <- T049, body weights <- T050 Vai2; staging folder removed");
        }

        static void Backup(string asset, string dir, StringBuilder log)
        {
            Directory.CreateDirectory(dir);
            foreach (var f in new[] { asset, asset + ".meta" })
            {
                var dst = Path.Combine(dir, Path.GetFileName(f));
                if (File.Exists(dst)) { log.AppendLine("backup kept (exists): " + dst); continue; }
                File.Copy(f, dst); log.AppendLine("backup: " + dst);
            }
        }

        /// <summary>Weights of the FBX body, moved onto the game mesh's vertex order and bone indices.</summary>
        static BoneWeight[] Mapped(string fbx, Mesh game, string[] gameBones, StringBuilder log, string label)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            var r = model.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(x => x.sharedMesh && x.sharedMesh.vertexCount > 10000)
                    ?? throw new Exception("No body renderer in " + fbx);
            var m = r.sharedMesh;
            var idx = new int[r.bones.Length];
            var missing = new List<string>();
            for (int i = 0; i < idx.Length; i++) { idx[i] = Array.IndexOf(gameBones, r.bones[i].name); if (idx[i] < 0) missing.Add(r.bones[i].name); }

            var sv = m.vertices; var sn = m.normals; var su = m.uv; var sw = m.boneWeights;
            var gv = game.vertices; var gn = game.normals; var gu = game.uv;
            // Spatial hash of the source vertices.
            const float cell = .002f;
            var grid = new Dictionary<Vector3Int, List<int>>();
            for (int i = 0; i < sv.Length; i++)
            {
                var k = Vector3Int.FloorToInt(sv[i] / cell);
                if (!grid.TryGetValue(k, out var l)) grid[k] = l = new List<int>();
                l.Add(i);
            }
            var outW = new BoneWeight[gv.Length];
            float maxD = 0; int far = 0, usedMissing = 0;
            for (int i = 0; i < gv.Length; i++)
            {
                var k = Vector3Int.FloorToInt(gv[i] / cell);
                int best = -1; float bestCost = float.MaxValue, bestD = 0;
                for (int x = -1; x <= 1; x++) for (int y = -1; y <= 1; y++) for (int z = -1; z <= 1; z++)
                    if (grid.TryGetValue(k + new Vector3Int(x, y, z), out var l))
                        foreach (var j in l)
                        {
                            float d = (sv[j] - gv[i]).magnitude;
                            float cost = d * 1000f + (1 - Vector3.Dot(sn[j], gn[i])) + (su.Length > 0 && gu.Length > 0 ? (su[j] - gu[i]).magnitude : 0);
                            if (cost < bestCost) { bestCost = cost; best = j; bestD = d; }
                        }
                if (best < 0) { far++; outW[i] = game.boneWeights[i]; continue; }
                maxD = Mathf.Max(maxD, bestD);
                var w = sw[best];
                outW[i] = Remap(w, idx, ref usedMissing);
            }
            log.AppendLine($"{label}: {sv.Length} verts, {r.bones.Length} bones (missing in game: {string.Join(",", missing)}); unmatched {far}; max match distance {maxD * 1000:F3} mm; weights on missing bones {usedMissing}");
            return outW;
        }

        static BoneWeight Remap(BoneWeight w, int[] idx, ref int usedMissing)
        {
            var pairs = new List<(int, float)>();
            var src = new[] { (w.boneIndex0, w.weight0), (w.boneIndex1, w.weight1), (w.boneIndex2, w.weight2), (w.boneIndex3, w.weight3) };
            foreach (var (b, v) in src)
            {
                if (v <= 0) continue;
                if (idx[b] < 0) { usedMissing++; continue; }
                pairs.Add((idx[b], v));
            }
            pairs = pairs.OrderByDescending(p => p.Item2).ToList();
            float s = pairs.Sum(p => p.Item2);
            var o = new BoneWeight();
            for (int n = 0; n < pairs.Count; n++)
            {
                float v = pairs[n].Item2 / s; int b = pairs[n].Item1;
                if (n == 0) { o.boneIndex0 = b; o.weight0 = v; } else if (n == 1) { o.boneIndex1 = b; o.weight1 = v; }
                else if (n == 2) { o.boneIndex2 = b; o.weight2 = v; } else { o.boneIndex3 = b; o.weight3 = v; }
            }
            return o;
        }

        static float Diff(BoneWeight[] a, BoneWeight[] b, string[] bones, out int count)
        {
            float max = 0; count = 0;
            for (int i = 0; i < a.Length; i++)
            {
                var da = Dict(a[i]); var db = Dict(b[i]);
                float d = da.Keys.Union(db.Keys).Max(k => Mathf.Abs(da.GetValueOrDefault(k) - db.GetValueOrDefault(k)));
                max = Mathf.Max(max, d); if (d > .02f) count++;
            }
            return max;
        }

        static Dictionary<int, float> Dict(BoneWeight w)
        {
            var d = new Dictionary<int, float>();
            void Add(int b, float v) { if (v > 0) d[b] = d.GetValueOrDefault(b) + v; }
            Add(w.boneIndex0, w.weight0); Add(w.boneIndex1, w.weight1); Add(w.boneIndex2, w.weight2); Add(w.boneIndex3, w.weight3);
            return d;
        }
    }
}
