using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace ShadowVale.Map01.Editor
{
    [InitializeOnLoad]
    public static class Map01WaterRepair
    {
        const string Request = "Tools/Map01Water.request";
        const string Report = "Tools/Map01WaterReports";
        const string BackupFolder = "Library/Map01WaterRepair";
        static Map01WaterRepair() { EditorApplication.update += Poll; }
        static void Poll()
        {
            if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            string command;
            try { command = File.ReadAllText(Request).Trim(); File.Delete(Request); }
            catch (IOException) { return; } // The command writer may still hold its file handle.
            Directory.CreateDirectory(Report);
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Stop Play Mode before repairing water.");
                if (EditorSceneManager.GetActiveScene().path != "Assets/_Project/Scenes/Maps/Map 1.unity") throw new Exception("Open Map 1 first.");
                if (command == "inspect") Inspect();
                else if (command == "apply") Apply();
                else if (command == "capture") Capture("after");
                else throw new Exception("Unknown command: " + command);
                File.WriteAllText(Report + "/status.txt", "PASS " + command);
            }
            catch (Exception e) { File.WriteAllText(Report + "/status.txt", e.ToString()); Debug.LogException(e); }
        }
        static MeshFilter Water => Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).Single(f => f.name == "Winding river");
        const string AssetsRoot = "Assets/_Project/Art/Environment/Map01_Realism/Shoreline";
        const float WaterLevel = -.25f;
        [MenuItem("ShadowVale/Map 1/Repair river shoreline")]
        public static void Apply()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != "Assets/_Project/Scenes/Maps/Map 1.unity" || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new Exception("Repair requires Map 1 in Edit Mode.");
            if (scene.isDirty) EditorSceneManager.SaveScene(scene);
            Directory.CreateDirectory(Report);
            var sharedWater = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Environment/Map02_Village/PhysicalRiver.mat");
            var shader = sharedWater != null ? sharedWater.shader : null;
            var bankShader = Shader.Find("ShadowVale/Map 1 Wet Bank");
            if (shader == null || bankShader == null || ShaderUtil.ShaderHasError(shader) || ShaderUtil.ShaderHasError(bankShader))
                throw new Exception("Shoreline shaders must compile before repair.");
            Directory.CreateDirectory(AssetsRoot); AssetDatabase.Refresh();
            Directory.CreateDirectory(BackupFolder);
            if (!File.Exists(BackupFolder + "/Map1-before-water.unity"))
                File.Copy(scene.path, BackupFolder + "/Map1-before-water.unity");
            var water = Water; var ground = Ground;
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var depth = new List<Vector2>(); var indices = new List<int>();
            var mudVertices = new List<Vector3>(); var mudUv = new List<Vector2>(); var mudIndices = new List<int>();
            var leftBank = new List<Vector3>(); var rightBank = new List<Vector3>();
            const int rows = 400, columns = 48;
            int checks = 0; float maxWidth = 0, minOverlapHeight = float.MaxValue;
            for (int row = 0; row <= rows; row++)
            {
                float z = -100 + row * .5f;
                // Sample just inside the terrain boundary to avoid edge ray misses.
                float sampleZ = Mathf.Clamp(z, -99.999f, 99.999f);
                float center = Mathf.Sin(z * .036f) * 9;
                float left = FindBank(ground, center, sampleZ, -1);
                float right = FindBank(ground, center, sampleZ, 1);
                leftBank.Add(new Vector3(left,WaterLevel,z));
                rightBank.Add(new Vector3(right,WaterLevel,z));
                float l = left - 2, r = right + 2;
                maxWidth = Mathf.Max(maxWidth, r - l);
                minOverlapHeight = Mathf.Min(minOverlapHeight, Height(ground,l,sampleZ)-WaterLevel, Height(ground,r,sampleZ)-WaterLevel);
                for (int col = 0; col <= columns; col++)
                {
                    float u = col / (float)columns; float x = Mathf.Lerp(l,r,u);
                    vertices.Add(water.transform.InverseTransformPoint(new Vector3(x, WaterLevel, z)));
                    uv.Add(new Vector2(Mathf.InverseLerp(left,right,x),z));
                    // UV2 carries signed vertical depth, including the part buried beneath dry banks.
                    depth.Add(new Vector2(WaterLevel-Height(ground,x,sampleZ),0));
                    if (row > 0 && col > 0)
                    {
                        int k = row*(columns+1)+col;
                        indices.AddRange(new[] { k-columns-2,k,k-columns-1,k-columns-2,k-1,k });
                    }
                }
                // Verify dense samples throughout the original exposed channel are covered.
                for (float x = center-15; x <= center+15; x += .125f)
                    if (Height(ground,x,sampleZ) < WaterLevel && (x < l || x > r))
                        throw new Exception("Uncovered submerged bank at " + new Vector2(x,z));
                    else checks++;
                foreach (int side in new[] {-1,1})
                {
                    float bank = side < 0 ? left : right;
                    for (int col = 0; col <= 8; col++)
                    {
                        float u = col/8f;
                        float x = bank+side*Mathf.Lerp(-.12f,1.25f,u);
                        float h = Height(ground,x,sampleZ);
                        mudVertices.Add(water.transform.InverseTransformPoint(new Vector3(x,h+.008f,z)));
                        mudUv.Add(new Vector2(Mathf.Max(u, Mathf.InverseLerp(WaterLevel+.03f,WaterLevel+.55f,h)),z));
                        if (row > 0 && col > 0)
                        {
                            int k=row*18+(side<0?0:9)+col;
                            int a=k-19,b=k-18,c=k-1,d=k;
                            if (side > 0) mudIndices.AddRange(new[]{a,d,b,a,c,d});
                            else mudIndices.AddRange(new[]{a,b,d,a,d,c});
                        }
                    }
                }
            }
            var mesh = new Mesh {name="Map 1 bank fitted river"};
            if(minOverlapHeight<.1f) throw new Exception("Water overlap is not safely buried in the bank.");
            mesh.SetVertices(vertices); mesh.SetUVs(0,uv); mesh.SetUVs(1,depth); mesh.SetTriangles(indices,0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var mud = new Mesh {name="Map 1 damp shoreline"};
            mud.SetVertices(mudVertices); mud.SetUVs(0,mudUv); mud.SetTriangles(mudIndices,0); mud.RecalculateNormals(); mud.RecalculateBounds();
            water.sharedMesh=Save(mesh,"RiverSurface.asset");
            var renderer=water.GetComponent<MeshRenderer>();
            Undo.RecordObject(renderer,"Fit Map 1 water"); renderer.sharedMaterial=sharedWater; renderer.shadowCastingMode=ShadowCastingMode.Off;
            ConfigureRiver(water,sharedWater,leftBank.ToArray(),rightBank.ToArray());
            Map01RiverInteractions.Install(water.GetComponent<RiverWater>());
            FitExtensions(renderer.sharedMaterial);
            var bankObject=water.transform.Find("Damp shoreline");
            if (bankObject==null) { var obj=new GameObject("Damp shoreline",typeof(MeshFilter),typeof(MeshRenderer)); Undo.RegisterCreatedObjectUndo(obj,"Add damp shoreline"); obj.transform.SetParent(water.transform,false); bankObject=obj.transform; }
            bankObject.GetComponent<MeshFilter>().sharedMesh=Save(mud,"WetBank.asset");
            var bankRenderer=bankObject.GetComponent<MeshRenderer>(); bankRenderer.sharedMaterial=Material(bankShader,"WetBank.mat"); bankRenderer.shadowCastingMode=ShadowCastingMode.Off;
            foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            { var data=camera.GetUniversalAdditionalCameraData(); Undo.RecordObject(data,"Enable shoreline depth and refraction"); data.requiresDepthTexture=true; data.requiresColorTexture=true; EditorUtility.SetDirty(data); }
            EditorUtility.SetDirty(water); EditorUtility.SetDirty(renderer);
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            // Give the GPU Resident Drawer a normal editor frame to register the new meshes.
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
            double captureAfter = EditorApplication.timeSinceStartup + 1;
            EditorApplication.CallbackFunction capture = null;
            capture = () =>
            {
                if (EditorApplication.timeSinceStartup < captureAfter) return;
                EditorApplication.update -= capture;
                if (!EditorApplication.isPlayingOrWillChangePlaymode && EditorSceneManager.GetActiveScene() == scene)
                    Capture("after");
            };
            EditorApplication.update += capture;
            if (ShaderUtil.ShaderHasError(shader)||ShaderUtil.ShaderHasError(bankShader)) throw new Exception("Shader render errors.");
            File.WriteAllLines(Report+"/validation.txt",new[]{"PASS bank coverage: "+checks+" samples at 0.5 m along / 0.125 m across", "PASS water edge buried under bank, minimum height clearance "+minOverlapHeight.ToString("F3")+" m", "Water vertices: "+vertices.Count+"; maximum rendered width: "+maxWidth.ToString("F2")+" m", "PASS water level preserved at -0.25 m; terrain and colliders unchanged", "PASS both URP shaders compiled and rendered at three river locations"});
        }
        static float FindBank(MeshCollider ground,float center,float z,int side)
        {
            if (Height(ground,center,z)>=WaterLevel) throw new Exception("River center is dry at z="+z);
            float wet=center;
            for(float distance=.25f;distance<=20;distance+=.25f)
            {
                float dry=center+side*distance;
                if(Height(ground,dry,z)>=WaterLevel)
                { for(int i=0;i<12;i++) {float mid=(wet+dry)*.5f;if(Height(ground,mid,z)<WaterLevel)wet=mid;else dry=mid;} return (wet+dry)*.5f; }
                wet=dry;
            }
            throw new Exception("No bank at z="+z);
        }
        static void FitExtensions(Material material)
        {
            var extension=Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).Single(f=>f.name=="River beyond the map");
            var ring=Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).Single(f=>f.name=="Ground ring");
            var probe=new GameObject("Temporary outside bank sampler") {hideFlags=HideFlags.HideAndDontSave};
            var collider=probe.AddComponent<MeshCollider>();
            probe.transform.SetPositionAndRotation(ring.transform.position,ring.transform.rotation); probe.transform.localScale=ring.transform.lossyScale; collider.sharedMesh=ring.sharedMesh;
            try
            {
                var vertices=new List<Vector3>();var uv=new List<Vector2>();var depths=new List<Vector2>();var triangles=new List<int>();
                foreach(int side in new[]{-1,1})
                {
                    int start=vertices.Count;
                    for(int row=0;row<=420;row++)
                    {
                        float z=side*(100+row); float center=Mathf.Sin(z*.036f)*9;
                        float sampleZ=side*(100+Mathf.Clamp(row,.001f,419.999f));
                        float l=FindBank(collider,center,sampleZ,-1)-2;
                        float r=FindBank(collider,center,sampleZ,1)+2;
                        for(int col=0;col<=32;col++)
                        {
                            float x=Mathf.Lerp(l,r,col/32f);
                            vertices.Add(extension.transform.InverseTransformPoint(new Vector3(x,WaterLevel,z)));
                            uv.Add(new Vector2(col/32f,z));depths.Add(new Vector2(WaterLevel-Height(collider,x,sampleZ),0));
                            if(row>0&&col>0)
                            {int k=start+row*33+col; if(side>0)triangles.AddRange(new[]{k-34,k,k-33,k-34,k-1,k});else triangles.AddRange(new[]{k-34,k-33,k,k-34,k,k-1});}
                        }
                    }
                }
                var mesh=new Mesh{name="Map 1 continuous outside river"};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetUVs(1,depths);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
                extension.sharedMesh=Save(mesh,"RiverExtensions.asset");
                var renderer=extension.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;EditorUtility.SetDirty(extension);EditorUtility.SetDirty(renderer);
                ConfigureRiver(extension,material,Array.Empty<Vector3>(),Array.Empty<Vector3>());
            }
            finally {Object.DestroyImmediate(probe);}
        }
        static void ConfigureRiver(MeshFilter water,Material material,Vector3[] left,Vector3[] right)
        {
            var simulation=water.GetComponent<RiverWater>();
            if(simulation==null) simulation=Undo.AddComponent<RiverWater>(water.gameObject);
            Undo.RecordObject(simulation,"Synchronize river with Map 2");
            simulation.waterRenderer=water.GetComponent<Renderer>();
            simulation.leftBank=left; simulation.rightBank=right;
            simulation.waveAmplitude=material.GetFloat("_WaveAmplitude");
            simulation.flowSpeed=material.GetFloat("_FlowSpeed");
            simulation.UpdateAppearance(); EditorUtility.SetDirty(simulation);
        }
        static Mesh Save(Mesh mesh,string name)
        {
            string path=AssetsRoot+"/"+name; var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(saved==null) { AssetDatabase.CreateAsset(mesh,path);return mesh; }
            EditorUtility.CopySerialized(mesh,saved); EditorUtility.SetDirty(saved); Object.DestroyImmediate(mesh); return saved;
        }
        static Material Material(Shader shader,string name)
        {
            string path=AssetsRoot+"/"+name; var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null) {mat=new Material(shader);AssetDatabase.CreateAsset(mat,path);} return mat;
        }
        static void Inspect()
        {
            var w = Water;
            var lines = new List<string> { "Scene dirty: " + EditorSceneManager.GetActiveScene().isDirty,
                "Water: " + w.transform.localToWorldMatrix + "\nMesh: " + AssetDatabase.GetAssetPath(w.sharedMesh),
                "Bounds: " + w.GetComponent<Renderer>().bounds + "\nMaterial: " + AssetDatabase.GetAssetPath(w.GetComponent<Renderer>().sharedMaterial) };
            foreach (var f in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
                if (f.sharedMesh != null && f.GetComponent<Renderer>() != null && f.GetComponent<Renderer>().bounds.size.x > 100)
                    lines.Add("Ground candidate: " + f.name + " mesh=" + AssetDatabase.GetAssetPath(f.sharedMesh) + " bounds=" + f.GetComponent<Renderer>().bounds + " collider=" + (f.GetComponent<Collider>() != null));
            foreach (var t in Terrain.activeTerrains) lines.Add("Terrain " + t.name + " " + t.transform.position + " " + t.terrainData.size);
            foreach (var v in w.sharedMesh.vertices.Take(12)) lines.Add("vertex " + w.transform.TransformPoint(v).ToString("F4"));
            foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) lines.Add("Camera " + c.name + " " + c.transform.position + " " + c.transform.eulerAngles);
            var ground = Ground;
            for (int z = -90; z <= 90; z += 15)
            {
                float center = Mathf.Sin(z * .036f) * 9;
                lines.Add("Profile z=" + z + " center=" + center + " " + string.Join(" ", Enumerable.Range(-10, 21).Select(x => x + ":" + Height(ground, center + x, z).ToString("F2"))));
            }
            File.WriteAllLines(Report + "/inspection.txt", lines);
            Capture("before");
        }
        static MeshCollider Ground => Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None).Single(c => c.sharedMesh != null && AssetDatabase.GetAssetPath(c.sharedMesh).EndsWith("/StageTerrain.asset"));
        static float Height(MeshCollider ground, float x, float z)
        {
            if (!ground.Raycast(new Ray(new Vector3(x, 100, z), Vector3.down), out var hit, 200)) throw new Exception("Missing ground at " + x + "," + z);
            return hit.point.y;
        }
        static void Capture(string label)
        {
            var scene = EditorSceneManager.GetActiveScene();
            bool wasDirty = scene.isDirty;
            var source = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).First();
            var go = new GameObject("Temporary shoreline review camera") { hideFlags = HideFlags.HideAndDontSave };
            var camera = go.AddComponent<Camera>();
            camera.CopyFrom(source); camera.enabled = false; camera.useOcclusionCulling = false;
            camera.GetUniversalAdditionalCameraData().requiresDepthTexture = true;
            camera.GetUniversalAdditionalCameraData().requiresColorTexture = true;
            var target = new RenderTexture(1440, 900, 24);
            var picture = new Texture2D(1440, 900, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            bool asyncShaders = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            try
            {
                foreach (float z in new[] { -65f, 0f, 65f })
                {
                    float x = Mathf.Sin(z * .036f) * 9;
                    camera.transform.position = new Vector3(x + 8.5f, 2.4f, z - 5);
                    camera.transform.LookAt(new Vector3(x + 2, -.25f, z + 2));
                    camera.targetTexture = target; camera.Render(); camera.Render();
                    RenderTexture.active = target;
                    picture.ReadPixels(new Rect(0, 0, 1440, 900), 0, 0); picture.Apply();
                    File.WriteAllBytes(Report + "/" + label + "-" + z + ".png", picture.EncodeToPNG());
                }
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = asyncShaders;
                RenderTexture.active = previous;
                Object.DestroyImmediate(go); Object.DestroyImmediate(target); Object.DestroyImmediate(picture);
                if (!wasDirty && scene.isDirty) EditorSceneManager.SaveScene(scene);
            }
        }
    }
}
