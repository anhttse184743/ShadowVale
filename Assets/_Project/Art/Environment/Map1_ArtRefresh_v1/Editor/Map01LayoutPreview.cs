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

namespace ShadowVale.EnvironmentReview
{
    /// <summary>Unapproved camp masses in a separate, non-gameplay review scene.</summary>
    public static class Map01LayoutPreview
    {
        const string Root = Map01EnvironmentReview.Root;
        const string Work = Map01EnvironmentReview.Work;
        const string ScenePath = Root + "/Map1_LayoutReview.unity";
        static readonly string[] Codes = { "C1", "C2", "C3" };
        static readonly Vector3[] Fronts = { new Vector3(-.8f,0,.6f), new Vector3(-.5f,0,-.866f), Vector3.back };
        static readonly Dictionary<string, Color> Colors = new Dictionary<string, Color>
        {
            {"Wall",new Color(.38f,.30f,.20f)}, {"Floor",new Color(.52f,.43f,.30f)},
            {"Roof",new Color(.48f,.40f,.24f)}, {"Frame",new Color(.20f,.25f,.22f)},
            {"Supply",new Color(.49f,.33f,.13f)}, {"Radio",new Color(.18f,.40f,.43f)},
            {"Power",new Color(.30f,.49f,.37f)}, {"Command",new Color(.48f,.30f,.40f)},
            {"Rest",new Color(.39f,.44f,.29f)}, {"Goal",new Color(.91f,.63f,.19f)},
            {"Paper",new Color(.79f,.76f,.61f)}, {"Ground",new Color(.19f,.24f,.18f)},
            {"Figure",new Color(.50f,.53f,.50f)}
        };
        [Serializable] public class Placement
        {
            public string code; public Vector3 position, entranceDirection, bounds;
            public int triangles, renderers; public string state;
        }
        [Serializable] public class Report
        {
            public string scene, unityVersion, gpu, status;
            public int width = 1920, height = 1080;
            public Placement[] placements;
        }

        [MenuItem("ShadowVale/Art Review/Build camp layout review copy")]
        public static void BuildReview()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Root+"/Meshes"); Directory.CreateDirectory(Root+"/Materials");
            Directory.CreateDirectory(Work+"/Previews");
            foreach (var code in Codes)
                File.Copy(Work+"/Exports/"+code+"_MissionBlockout.fbx", Root+"/Meshes/"+code+"_MissionBlockout.fbx", true);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var mats = new Dictionary<string, Material>();
            foreach (var pair in Colors)
            {
                var path=Root+"/Materials/BO_"+pair.Key+".mat";
                var m=AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m==null) { m=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m,path); }
                m.name="BO_"+pair.Key; m.SetColor("_BaseColor",pair.Value);m.SetFloat("_Smoothness",.15f);
                m.SetFloat("_Cull",0);m.enableInstancing=true;
                EditorUtility.SetDirty(m);AssetDatabase.SaveAssetIfDirty(m);mats.Add(m.name,m);
            }
            // Open the original read-only, then immediately create a separate scene asset.
            var scene=EditorSceneManager.OpenScene(Map01EnvironmentReview.MapPath);
            if (!EditorSceneManager.SaveScene(scene,ScenePath,true)) throw new Exception("Could not create review copy.");
            scene=EditorSceneManager.OpenScene(ScenePath);
            var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
            var records=new List<Placement>();
            var review=new GameObject("ART REVIEW ONLY / CAMP LAYOUTS PENDING APPROVAL");
            for (int i=0;i<Codes.Length;i++)
            {
                var code=Codes[i];var anchor=all.Single(t=>t.name==code+"_MissionAnchor");
                var old=all.Single(t=>t.name=="Enemy outpost "+(i+1));
                foreach (Transform child in old)
                    if (child.name.StartsWith("Compact field camp")) child.gameObject.SetActive(false);
                var path=Root+"/Meshes/"+code+"_MissionBlockout.fbx";
                var imp=(ModelImporter)AssetImporter.GetAtPath(path);
                imp.importCameras=false;imp.importLights=false;imp.importAnimation=false;
                imp.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;imp.SaveAndReimport();
                var wrapper=new GameObject(code+" / Proposed layout / Not approved");wrapper.transform.SetParent(review.transform);
                wrapper.transform.position=anchor.position;
                var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                model.transform.SetParent(wrapper.transform,false);
                var front=model.GetComponentsInChildren<Transform>(true).Single(t=>t.name.StartsWith("ENTRY_FORWARD",StringComparison.Ordinal));
                var dir=front.position-wrapper.transform.position;dir.y=0;
                // FromToRotation can flip the model upside down for an exact 180-degree turn.
                wrapper.transform.rotation=Quaternion.Euler(0,Vector3.SignedAngle(dir,Fronts[i],Vector3.up),0);
                foreach (var r in model.GetComponentsInChildren<MeshRenderer>(true))
                {
                    r.sharedMaterials=r.sharedMaterials.Select(m=>mats[m.name.Replace(" (Instance)","")]).ToArray();
                    r.shadowCastingMode=ShadowCastingMode.TwoSided;
                    if (r.name.StartsWith("Review ground")) r.gameObject.SetActive(false);
                }
                var visible=model.GetComponentsInChildren<Renderer>();
                var b=visible[0].bounds;foreach (var r in visible) b.Encapsulate(r.bounds);
                int tris=0;
                foreach(var mf in model.GetComponentsInChildren<MeshFilter>())
                    for(int s=0;s<mf.sharedMesh.subMeshCount;s++) tris+=(int)mf.sharedMesh.GetIndexCount(s)/3;
                records.Add(new Placement {code=code,position=anchor.position,entranceDirection=Fronts[i].normalized,
                    bounds=b.size,triangles=tris,renderers=visible.Length,state="Layout proposal only. Collision/navigation not baked."});
            }
            int hiddenPlants=0;
            foreach(var plant in all.Where(t=>t.GetComponent<LODGroup>()!=null))
            {
                var prefab=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(plant.gameObject);
                if(!prefab.Contains("/ForestV2/"))continue;
                bool tree=plant.name.Contains("Tree")||plant.name.Contains("Broadleaf")||plant.name.Contains("Bamboo");
                foreach(var record in records)
                {
                    var delta=plant.position-record.position;delta.y=0;
                    float forward=Vector3.Dot(delta,record.entranceDirection);
                    float side=Vector3.Cross(delta,record.entranceDirection).magnitude;
                    if(delta.magnitude<(tree?11f:6.8f) || (forward>0 && forward<17 && side<(tree?7:3)))
                    {plant.gameObject.SetActive(false);hiddenPlants++;break;}
                }
            }
            // Existing actors are hidden only for this static layout review; their AI/data are untouched.
            foreach(var actor in all.SelectMany(t=>t.GetComponents<Animator>()))
                foreach(var renderer in actor.GetComponentsInChildren<Renderer>(true))renderer.enabled=false;
            Debug.Log("Review-only vegetation clearing: "+hiddenPlants+" instances hidden. Final forest arrangement pending art pass.");
            // Lighting changes belong only to this scene, with no edits to shared pipeline or materials.
            var sun=all.SelectMany(t=>t.GetComponents<Light>()).First(l=>l.type==LightType.Directional);
            sun.intensity=1.65f;sun.color=new Color(1,.95f,.85f);sun.shadows=LightShadows.Soft;
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.61f,.68f,.73f);
            RenderSettings.ambientEquatorColor=new Color(.51f,.57f,.49f);
            RenderSettings.ambientGroundColor=new Color(.30f,.29f,.23f);
            var ambient=new SphericalHarmonicsL2();ambient.AddAmbientLight(new Color(.56f,.60f,.54f));RenderSettings.ambientProbe=ambient;
            foreach(var c in all.SelectMany(t=>t.GetComponents<Camera>())) c.enabled=false;
            var camera=new GameObject("Layout review camera / use ShadowVale Art Review menus").AddComponent<Camera>();
            camera.transform.SetParent(review.transform);camera.nearClipPlane=.1f;camera.farClipPlane=350;
            camera.clearFlags=CameraClearFlags.Skybox;camera.allowHDR=true;camera.aspect=16f/9f;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            camera.GetUniversalAdditionalCameraData().antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            // Open the review cameras' sightlines. This masking is only for layout inspection;
            // it is not a final vegetation distribution or a change to gameplay cover.
            for(int i=0;i<Codes.Length;i++)
            {
                SelectCamera(camera,review.transform.GetChild(i),Codes[i],false);
                var origin=records[i].position;var view=camera.transform.position-origin;view.y=0;
                float length=view.magnitude;view.Normalize();
                foreach(var plant in all.Where(t=>t.gameObject.activeInHierarchy&&t.GetComponent<LODGroup>()!=null))
                {
                    if(!PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(plant.gameObject).Contains("/ForestV2/"))continue;
                    var rs=plant.GetComponentsInChildren<Renderer>(true);if(rs.Length==0)continue;
                    var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);
                    var delta=b.center-origin;delta.y=0;float along=Vector3.Dot(delta,view);
                    float radius=Mathf.Max(b.extents.x,b.extents.z);
                    if(along>0&&along<length+radius&&Vector3.Cross(delta,view).magnitude<radius+4)
                        plant.gameObject.SetActive(false);
                }
            }
            for(int i=0;i<Codes.Length;i++)
            {
                var model=review.transform.GetChild(i);
                SelectCamera(camera,model,Codes[i],false);
                Capture(camera,Work+"/Previews/Unity_"+Codes[i]+"_MapContext.png");
                SetRoofs(model,false);SelectCamera(camera,model,Codes[i],true);
                Capture(camera,Work+"/Previews/Unity_"+Codes[i]+"_Cutaway.png");SetRoofs(model,true);
            }
            camera.orthographic=true;camera.orthographicSize=120;
            camera.transform.SetPositionAndRotation(new Vector3(0,220,0),Quaternion.Euler(90,0,0));
            bool fog=RenderSettings.fog;RenderSettings.fog=false;
            Capture(camera,Work+"/Previews/Unity_MapOverview.png");RenderSettings.fog=fog;
            SelectCamera(camera,review.transform.GetChild(0),"C1",false);
            EditorSceneManager.SaveScene(scene,ScenePath);
            File.WriteAllText(Work+"/Audit/layout_review.json",JsonUtility.ToJson(new Report
            {scene=ScenePath,unityVersion=Application.unityVersion,gpu=SystemInfo.graphicsDeviceName,
                status="Camp layout review pending user approval. Original gameplay scene not saved. No mission, AI, terrain or NavMesh changes.",placements=records.ToArray()},true));
            Debug.Log("MAP LAYOUT REVIEW COMPLETE");
        }
        static void SetRoofs(Transform model,bool active)
        {
            foreach(var t in model.GetComponentsInChildren<Transform>(true))
                if(t.name.StartsWith("ROOF /"))t.gameObject.SetActive(active);
        }
        static void SelectCamera(Camera camera,Transform model,string code,bool plan)
        {
            var all=model.GetComponentsInChildren<Transform>(true);
            var key=code+(plan?"_Plan":"_Overview");
            var eye=all.Single(t=>t.name=="SHOT_"+key+"_Eye");var target=all.Single(t=>t.name=="SHOT_"+key+"_Target");
            camera.orthographic=true;camera.orthographicSize=plan?7.0f:7.4f;
            var direction=target.position-eye.position;
            var up=plan?-Fronts[Array.IndexOf(Codes,code)]:Vector3.up;
            if(Mathf.Abs(Vector3.Dot(direction.normalized,up.normalized))>.99f)up=Vector3.forward;
            camera.transform.SetPositionAndRotation(eye.position,Quaternion.LookRotation(direction,up));
        }
        static void Capture(Camera camera,string path)
        {
            var prev=RenderTexture.active;var rt=RenderTexture.GetTemporary(1920,1080,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            var tex=new Texture2D(1920,1080,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=rt;for(int i=0;i<3;i++)camera.Render();RenderTexture.active=rt;
                tex.ReadPixels(new Rect(0,0,1920,1080),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());
            }
            finally{camera.targetTexture=null;RenderTexture.active=prev;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(tex);}
        }
        [MenuItem("ShadowVale/Art Review/View C1 layout")]
        public static void ViewC1()=>View("C1");
        [MenuItem("ShadowVale/Art Review/View C2 layout")]
        public static void ViewC2()=>View("C2");
        [MenuItem("ShadowVale/Art Review/View C3 layout")]
        public static void ViewC3()=>View("C3");
        static void View(string code)
        {
            if(EditorSceneManager.GetActiveScene().path!=ScenePath)
            {
                if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
                EditorSceneManager.OpenScene(ScenePath);
            }
            var review=Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .Single(t=>t.name.StartsWith(code+" / Proposed layout"));
            var camera=Object.FindObjectsByType<Camera>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .Single(c=>c.name.StartsWith("Layout review camera"));
            SelectCamera(camera,review,code,false);Selection.activeGameObject=review.gameObject;
            if(SceneView.lastActiveSceneView!=null)SceneView.lastActiveSceneView.AlignViewToObject(camera.transform);
            EditorApplication.ExecuteMenuItem("Window/General/Game");SceneView.RepaintAll();
        }
    }
}
