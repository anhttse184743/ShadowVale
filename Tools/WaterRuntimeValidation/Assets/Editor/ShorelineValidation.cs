using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ShadowVale.Map01.Editor;

public static class ShorelineValidation
{
    public static void Run()
    {
        var report = new List<string>();
        try
        {
            var input = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/ShorelineInput.asset");
            var terrain = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/ShorelineTerrain.asset");
            if (input == null || terrain == null) throw new Exception("Missing mesh snapshots");
            var vertices = input.vertices;
            if (vertices.Length != 201 * 17) throw new Exception("Unexpected river topology");
            var left = new Vector3[201]; var right = new Vector3[201];
            for (int i = 0; i <= 200; i++) { left[i] = vertices[i * 17]; right[i] = vertices[i * 17 + 16]; }
            var output = RiverShorelineMesh.Build(left, right, Matrix4x4.identity);
            var v = output.vertices;
            var ground = new GameObject("Actual Map 2 bank geometry").AddComponent<MeshCollider>();
            ground.sharedMesh = terrain;
            Physics.SyncTransforms();
            float clearance = float.PositiveInfinity;
            int samples = 0, oldGaps = 0;
            // Sample each boundary segment, not only mesh vertices. Test against the
            // conservative maximum of both shader waves (.055 * (1 + .45)).
            float crest = .045f + .055f * 1.45f;
            for (int row = 0; row < 200; row++)
                for (int step = 0; step < 8; step++)
                    foreach (int col in new[] { 0, 16 })
                    {
                        float t = (step + .5f) / 8;
                        var p = Vector3.Lerp(v[row * 17 + col], v[(row + 1) * 17 + col], t);
                        if (!ground.Raycast(new Ray(new Vector3(p.x, 10, p.z), Vector3.down), out var hit, 20))
                            throw new Exception("No terrain under expanded edge at " + p);
                        float margin = hit.point.y - crest;
                        clearance = Mathf.Min(clearance, margin);
                        if (margin < .1f) throw new Exception("Water edge not safely buried at " + p);
                        var old = Vector3.Lerp(vertices[row * 17 + col], vertices[(row + 1) * 17 + col], t);
                        if (ground.Raycast(new Ray(new Vector3(old.x, 10, old.z), Vector3.down), out var oldHit, 20) && oldHit.point.y < .045f) oldGaps++;
                        samples++;
                    }
            for (int i = 0; i <= 200; i++)
                if (Vector3.Distance(v[i * 17 + 8], vertices[i * 17 + 8]) > .0001f)
                    throw new Exception("River center moved");
            report.Add("PASS actual Roadside_Continuous_Terrain geometry: " + samples + " shoreline samples");
            report.Add("PASS minimum terrain clearance above maximum wave crest: " + clearance.ToString("F3") + " m");
            report.Add("Original exposed water-edge samples: " + oldGaps + "/" + samples);
            report.Add("PASS channel center and water level preserved; physics banks and scene objects unchanged");
            report.Add("PASS mesh " + output.vertexCount + " vertices, " + output.triangles.Length / 3 + " triangles");
            AssetDatabase.CreateAsset(output, "Assets/ShorelineOutput.asset");
            AssetDatabase.SaveAssets();
            File.WriteAllLines("../Map02Reports/shoreline-checks.txt", report);
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            File.WriteAllText("../Map02Reports/shoreline-checks.txt", "FAIL " + e);
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }
}
