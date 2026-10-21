using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace ShadowVale.Map01.Editor
{
    public static class C3BedAnimationBuilder
    {
        public const string Root="Assets/_Project/Art/C3_BedAnimation_v11";
        public const string Work="Docs/Reviews/C3_BedPistol_Unity_20261019";
        public const string DataPath="Assets/_Project/Map01/Resources/C3/BedAnimationV11.asset";
        const string Export="SourceArt/C3_BedPistol_20261018/UnityExport";
        static Transform Named(GameObject go,string name)=>go.GetComponentsInChildren<Transform>(true).Single(t=>t.name==name);
        static AnimationClip Import(string name,int frames,int fps)
        {
            string path=Root+"/"+name+".fbx";File.Copy(Export+"/"+name+".fbx",path,true);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var imp=(ModelImporter)AssetImporter.GetAtPath(path);
            imp.animationType=ModelImporterAnimationType.Generic;imp.avatarSetup=ModelImporterAvatarSetup.NoAvatar;
            imp.importAnimation=true;imp.importVisibility=false;imp.importCameras=false;imp.importLights=false;
            imp.optimizeGameObjects=false;imp.animationCompression=ModelImporterAnimationCompression.Off;
            imp.isReadable=true;imp.skinWeights=ModelImporterSkinWeights.Standard;imp.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
            var take=imp.defaultClipAnimations.First().takeName;
            // Blender exports frame 1 at 1/fps, not at t=0. Trim that leading hold
            // so source frame N is sampled at (N-1)/fps in the runtime.
            imp.clipAnimations=new[]{new ModelImporterClipAnimation{name=name,takeName=take,firstFrame=1,lastFrame=frames,
                keepOriginalOrientation=true,keepOriginalPositionXZ=true,keepOriginalPositionY=true,
                lockRootRotation=false,lockRootPositionXZ=false,lockRootHeightY=false,loopTime=false}};
            imp.SaveAndReimport();
            var clip=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview"));
            if(Mathf.Abs(clip.length-(frames-1f)/fps)>.025f)throw new Exception("Clip duration changed: "+name+" "+clip.length);
            return clip;
        }
        [Serializable] sealed class ExportReceipt{public string source_sha256;}
        [Serializable] sealed class WakeReceipt{public int wakePickupFrame;}
        static T Save<T>(T obj,string path)where T:Object
        {
            var previous=AssetDatabase.LoadAssetAtPath<T>(path);
            if(previous==null){AssetDatabase.CreateAsset(obj,path);return obj;}
            EditorUtility.CopySerialized(obj,previous);Object.DestroyImmediate(obj);EditorUtility.SetDirty(previous);return previous;
        }
        public static void ResolveMaterials(GameObject instance,string destination=Root)
        {
            var existing=AssetDatabase.FindAssets("t:Material",new[]{"Assets/_Project/Art/Characters","Assets/_Project/Art/Environment/Map01Items","Assets/_Project/Art/K54_v12",Root})
                .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<Material>).Where(m=>m!=null)
                .GroupBy(m=>m.name).ToDictionary(g=>g.Key,g=>g.First());
            foreach(var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                if(renderer.name=="Dich_Than")
                {
                    renderer.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Characters/Enemies/Materials/lính rig - Material.mat");
                    continue;
                }
                if(renderer.name=="Mesh_0.001")
                {
                    var player=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/Player.prefab");
                    renderer.sharedMaterials=player.GetComponentsInChildren<SkinnedMeshRenderer>(true).OrderByDescending(r=>r.sharedMesh.vertexCount).First().sharedMaterials;
                    continue;
                }
                renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>{
                    string n=m!=null?m.name:"Bed prop";
                    if((renderer.name.Contains("AK47")||renderer.name.StartsWith("Sung_DungCot"))&&existing.TryGetValue("Blued steel",out var rifleSteel))return rifleSteel;
                    if(existing.TryGetValue(n,out var e))return e;
                    string safe=new string(n.Select(c=>char.IsLetterOrDigit(c)?c:'_').ToArray());string path=destination+"/"+safe+".mat";
                    var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material!=null)return material;
                    var color=m!=null?m.color:new Color(.25f,.22f,.17f);
                    if(n.Contains("Remy hand"))color=new Color(.41f,.255f,.165f);
                    material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=n};material.color=color;
                    if(m!=null&&m.mainTexture!=null){material.mainTexture=m.mainTexture;material.color=Color.white;}
                    material.SetFloat("_Smoothness",n.Contains("Can")||n.Contains("steel") ? .4f : .25f);
                    AssetDatabase.CreateAsset(material,path);return material;
                }).ToArray();
                renderer.enabled=true;renderer.forceRenderingOff=false;
                if(renderer is SkinnedMeshRenderer skin){skin.updateWhenOffscreen=true;skin.localBounds=new Bounds(Vector3.zero,Vector3.one*500);}
            }
        }
        [MenuItem("ShadowVale/C3/Build approved bed animation v11")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play before import.");
            Directory.CreateDirectory(Root);Directory.CreateDirectory(Path.GetDirectoryName(DataPath));Directory.CreateDirectory(Work);
            var execution=Import("C3_BedPistol_v11",400,60);var sleep=Import("C3_Enemy_Sleep",132,30);var wake=Import("C3_Enemy_Wake",100,30);
            var pair=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/C3_BedPistol_v11.fbx"));
            var sleeper=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/C3_Enemy_Sleep.fbx"));
            try
            {
                foreach(var model in new[]{pair,sleeper}){var animator=model.GetComponent<Animator>();if(animator==null)animator=model.AddComponent<Animator>();animator.runtimeAnimatorController=null;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;ResolveMaterials(model);}
                using var pairPose=new Map01CinematicPose(pair.GetComponent<Animator>(),skinGrounding:false);
                using var sleepPose=new Map01CinematicPose(sleeper.GetComponent<Animator>(),skinGrounding:false);
                var data=ScriptableObject.CreateInstance<Map01BedAnimationData>();data.sourceHash=JsonUtility.FromJson<ExportReceipt>(File.ReadAllText(Export+"/export.json")).source_sha256;
                data.execution=execution;data.sleep=sleep;data.wake=wake;
                data.wakePickupFrame=JsonUtility.FromJson<WakeReceipt>(File.ReadAllText(Export+"/sleep_export.json")).wakePickupFrame;
                if(data.wakePickupFrame<1||data.wakePickupFrame>100)throw new Exception("Missing source rifle visibility hand-off.");
                data.namPath=AnimationUtility.CalculateTransformPath(Named(pair,"Nam_DatCho"),pair.transform);
                data.enemyPath=AnimationUtility.CalculateTransformPath(Named(pair,"Dich_Rig"),pair.transform);
                data.pistolPath=AnimationUtility.CalculateTransformPath(Named(pair,"K54_Suppressed_DemoRoot"),pair.transform);
                data.holsterPath=AnimationUtility.CalculateTransformPath(Named(pair,"DEMO_Holster_HipAnchor"),pair.transform);
                pairPose.Sample(execution,0);
                var hip=Named(pair,"Player_Rig").GetComponentsInChildren<Transform>().Single(t=>t.name=="Hips");
                var feet=Named(pair,"Player_Rig").GetComponentsInChildren<Transform>().Where(t=>t.name=="LeftFoot"||t.name=="RightFoot").ToArray();
                data.approach=pair.transform.InverseTransformPoint(feet.Select(t=>t.position).Aggregate(Vector3.zero,(a,b)=>a+b)/feet.Length);data.approach.y=0;
                var namBones=Named(pair,"Player_Rig").GetComponentsInChildren<Transform>();
                var up=namBones.Single(t=>t.name=="Neck").position-hip.position;
                var right=namBones.Single(t=>t.name=="RightArm").position-namBones.Single(t=>t.name=="LeftArm").position;
                var forward=Vector3.ProjectOnPlane(Vector3.Cross(right,up),Vector3.up);
                data.approachRotation=Quaternion.LookRotation(forward.sqrMagnitude>.001f?forward:Vector3.forward);
                pairPose.Sample(execution,data.Duration);
                data.exit=pair.transform.InverseTransformPoint(feet.Select(t=>t.position).Aggregate(Vector3.zero,(a,b)=>a+b)/feet.Length);data.exit.y=0;data.exitRotation=data.approachRotation;
                sleepPose.Sample(wake,wake.length);
                var enemyFeet=Named(sleeper,"Dich_Rig").GetComponentsInChildren<Transform>().Where(t=>t.name=="LeftFoot"||t.name=="RightFoot").ToArray();
                data.wakeExit=sleeper.transform.InverseTransformPoint(enemyFeet.Select(t=>t.position).Aggregate(Vector3.zero,(a,b)=>a+b)/enemyFeet.Length);data.wakeExit.y=0;
                sleepPose.Sample(sleep,0);
                // Explicit skin matrices avoid BakeMesh's editor-only scale/cache behaviour.
                var skin=Named(sleeper,"Dich_Than").GetComponent<SkinnedMeshRenderer>();
                var vertices=SkinWorldVertices(skin).Select(sleeper.transform.InverseTransformPoint).ToArray();
                var bounds=new Bounds(vertices[0],Vector3.zero);foreach(var v in vertices)bounds.Encapsulate(v);data.sleepingBounds=bounds;
                if(bounds.size.y<.3f||bounds.size.z<1f)throw new Exception("Sleep skin bounds are not in metres: "+bounds);
                pairPose.Sample(execution,0);
                pair.name="C3 Bed execution v11";sleeper.name="C3 Sleeping guard";
                data.pairPrefab=PrefabUtility.SaveAsPrefabAsset(pair,Root+"/C3_BedExecution.prefab");
                data.sleeperPrefab=PrefabUtility.SaveAsPrefabAsset(sleeper,Root+"/C3_SleepingGuard.prefab");
                Save(data,DataPath);AssetDatabase.SaveAssets();
                File.WriteAllText(Work+"/import.txt","source="+data.sourceHash+"\nduration="+execution.length+"\nsleep="+sleep.length+" wake="+wake.length+"\napproach="+data.approach+" exit="+data.exit+" wakeExit="+data.wakeExit+"\nsleepBounds="+data.sleepingBounds+"\n");
            }
            finally{Object.DestroyImmediate(pair);Object.DestroyImmediate(sleeper);}
            InspectBeds();Debug.Log("C3_BED_ANIMATION_BUILD_PASS");
        }
        public static Vector3[] SkinWorldVertices(SkinnedMeshRenderer skin)
        {
            var vertices=skin.sharedMesh.vertices;var weights=skin.sharedMesh.boneWeights;var bind=skin.sharedMesh.bindposes;
            var matrices=skin.bones.Select((b,i)=>b.localToWorldMatrix*bind[i]).ToArray();
            return vertices.Select((v,i)=>{
                var w=weights[i];return matrices[w.boneIndex0].MultiplyPoint3x4(v)*w.weight0+matrices[w.boneIndex1].MultiplyPoint3x4(v)*w.weight1
                    +matrices[w.boneIndex2].MultiplyPoint3x4(v)*w.weight2+matrices[w.boneIndex3].MultiplyPoint3x4(v)*w.weight3;
            }).ToArray();
        }
        public static void InspectBeds()
        {
            var scene=EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            var rows=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<Transform>(true))
                .Where(c=>(c.name.StartsWith("Berth ")||c.name.Contains("Canvas mattress"))&&AnimationUtility.CalculateTransformPath(c,null).Contains("C3"))
                .Select(c=>c.name+" path="+AnimationUtility.CalculateTransformPath(c,null)+" position="+c.position.ToString("F4")+" scale="+c.lossyScale+" euler="+c.eulerAngles+" up="+c.up+" right="+c.right);
            File.WriteAllLines(Work+"/beds.txt",rows);
            var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Environment/Map1_ArtRefresh_v1/Meshes/C3_MainGarrison_Art.fbx");
            var sourceMeshes=source.GetComponentsInChildren<MeshFilter>(true).GroupBy(m=>m.name).ToDictionary(g=>g.Key,g=>g.First().sharedMesh);
            var pillows=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<Transform>(true))
                .Where(m=>m.name.Contains("Stuffed pillow")&&AnimationUtility.CalculateTransformPath(m,null).Contains("C3")&&sourceMeshes.ContainsKey(m.name))
                .Select(m=>AnimationUtility.CalculateTransformPath(m,null)+" centre="+m.TransformPoint(sourceMeshes[m.name].bounds.center).ToString("F4"));
            File.WriteAllLines(Work+"/pillows.txt",pillows);
        }
        public static void Probe()
        {
            var d=AssetDatabase.LoadAssetAtPath<Map01BedAnimationData>(DataPath);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            var pair=Object.Instantiate(d.pairPrefab);var sleeping=Object.Instantiate(d.sleeperPrefab);sleeping.SetActive(false);
            using var pose=new Map01CinematicPose(pair.GetComponent<Animator>(),skinGrounding:false);
            var rows=new System.Collections.Generic.List<string>();
            File.WriteAllLines(Work+"/bindings.txt",AnimationUtility.GetCurveBindings(d.execution).Select(b=>b.path+" | "+b.propertyName+" | "+b.type));
            foreach(int f in new[]{1,155,195,239,400})
            {
                pose.Sample(d.execution,(f-1)/60f);
                foreach(var skin in pair.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var mesh=new Mesh();skin.BakeMesh(mesh);
                    var pts=mesh.vertices.Select(v=>skin.transform.TransformPoint(v)).ToArray();var bounds=new Bounds(pts[0],Vector3.zero);foreach(var p in pts)bounds.Encapsulate(p);
                    var weights=skin.sharedMesh.boneWeights;var bind=skin.sharedMesh.bindposes;var raw=skin.sharedMesh.vertices;var matrices=skin.bones.Select((b,i)=>b.localToWorldMatrix*bind[i]).ToArray();
                    Vector3 World(int i){var w=weights[i];return matrices[w.boneIndex0].MultiplyPoint3x4(raw[i])*w.weight0+matrices[w.boneIndex1].MultiplyPoint3x4(raw[i])*w.weight1+matrices[w.boneIndex2].MultiplyPoint3x4(raw[i])*w.weight2+matrices[w.boneIndex3].MultiplyPoint3x4(raw[i])*w.weight3;}
                    var cpu=new Bounds(World(0),Vector3.zero);for(int i=1;i<raw.Length;i++)cpu.Encapsulate(World(i));
                    rows.Add(f+" "+skin.name+" worldScale="+skin.transform.lossyScale.ToString("F5")+" mesh="+skin.sharedMesh.bounds+" bakedWorld="+bounds+" CPUWorld="+cpu+" bone0="+skin.bones[0].position.ToString("F5"));Object.DestroyImmediate(mesh);
                }
            }
            sleeping.SetActive(true);d.sleep.SampleAnimation(sleeping,0);
            foreach(var t in sleeping.GetComponentsInChildren<Transform>())if(t.name=="Dich_Rig"||t.name=="Dich_Than"||t.name=="Hips")rows.Add("SLEEP "+t.name+" local="+t.localPosition.ToString("F5")+" scale="+t.localScale.ToString("F5")+" world="+t.position.ToString("F5"));
            File.WriteAllLines(Work+"/probe.txt",rows);Object.DestroyImmediate(sleeping);
            var light=new GameObject("Preview daylight").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.5f;light.transform.rotation=Quaternion.Euler(48,-35,0);
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.45f,.45f,.45f);
            var camera=new GameObject("Unity review camera").AddComponent<Camera>();camera.backgroundColor=new Color(.16f,.18f,.17f);camera.clearFlags=CameraClearFlags.SolidColor;
            camera.transform.position=new Vector3(-1.85f,2.05f,-2.1f);camera.transform.LookAt(new Vector3(-.42f,.95f,-.65f));camera.fieldOfView=48;
            var rt=new RenderTexture(1920,1080,24);camera.targetTexture=rt;
            foreach(int f in new[]{1,155,195,239,400}){
                pose.Sample(d.execution,(f-1)/60f);camera.Render();RenderTexture.active=rt;
                var image=new Texture2D(1920,1080,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();File.WriteAllBytes(Work+"/Unity_Probe_"+f+".png",image.EncodeToPNG());Object.DestroyImmediate(image);
            }
            camera.targetTexture=null;RenderTexture.active=null;Object.DestroyImmediate(rt);Debug.Log("C3_BED_PROBE_COMPLETE");
        }
        public static void BuildAndProbe(){Build();Probe();}
    }
}
