using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace ShadowVale.EnvironmentReview
{
    [InitializeOnLoad] public static partial class NativeWeaponPreviewTools
    {
        public const string Work="Docs/Reviews/WeaponAnimation_Unity_20261017";
        public const string Root="Assets/_Project/Art/WeaponAnimationReview_20261017";
        public const string Model=Root+"/ShadowVale_Weapons_BlenderReview.fbx";
        public const string ScenePath=Root+"/BlenderWeaponReview.unity";
        const string PreviousStart="ShadowVale.NativeWeaponPreview.PreviousStart";
        static double next;static bool busy;
        [Serializable] sealed class SourceTake {public string item,action;public int start,end;}
        [Serializable] sealed class SourceData {public SourceTake[] takes;}
        static SourceTake[] Source()=>JsonUtility.FromJson<SourceData>("{\"takes\":"+File.ReadAllText("SourceArt/WeaponAnimation_Blender_20261017/takes.json")+"}").takes;
        static NativeWeaponPreviewTools(){ReviewToolVersion.Schedule(Work,"2");EditorApplication.update+=MaintainPreviewStartup;EditorApplication.update+=Poll;EditorSceneManager.activeSceneChangedInEditMode+=SceneChanged;AssemblyReloadEvents.beforeAssemblyReload+=Release;EditorApplication.delayCall+=()=>SceneChanged(default,SceneManager.GetActiveScene());}
        static void MaintainPreviewStartup()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||SessionState.GetBool("ShadowVale.MergeDev.Tests",false))return;
            var active=SceneManager.GetActiveScene();
            if(active.path==ScenePath&&AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene)!=ScenePath)SceneChanged(default,active);
        }
        static void Release(){foreach(var p in Object.FindObjectsByType<NativeWeaponPreviewPlayer>(FindObjectsInactive.Include,FindObjectsSortMode.None))p.Release();}
        static void SceneChanged(Scene from,Scene to)
        {
            if(to.path==ScenePath){if(!SessionState.GetBool(PreviousStart+".saved",false)){SessionState.SetString(PreviousStart,AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));SessionState.SetBool(PreviousStart+".saved",true);}EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);}
            else if(SessionState.GetBool(PreviousStart+".saved",false)){string previous=SessionState.GetString(PreviousStart,"");EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(previous)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);SessionState.EraseBool(PreviousStart+".saved");}
        }
        static void Poll()
        {
            if(busy||SessionState.GetBool("ShadowVale.MergeDev.Tests",false)||EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+2;
            string request=Work+"/request.txt";if(!File.Exists(request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(File.Exists("Docs/Reviews/MergeDev_20261016/integration.request")||File.Exists("Docs/Reviews/WeaponAnimation_20261016/request.txt"))return;
            string command=File.ReadAllText(request).Trim().TrimStart('\uFEFF');File.Move(request,request+"."+DateTime.UtcNow.Ticks+".consumed");
            EditorApplication.delayCall+=()=>{try{File.WriteAllText(Work+"/status.txt","RUNNING "+command);if(command=="BUILD")Build();else if(command=="OPEN")Open();else if(command=="RENDER")BeginRender();else if(command=="INSPECT")Inspect();else if(command=="PLAY_PROOF")BeginProof();else throw new Exception("Unknown native preview command "+command);if(!busy)File.WriteAllText(Work+"/status.txt","COMPLETE "+command);}catch(Exception e){File.WriteAllText(Work+"/status.txt","FAILED "+e);Debug.LogException(e);}};
        }
        static void CheckClean(){if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Editor is in Play Mode");for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new Exception("Scene has unsaved changes; preview operation stopped: "+SceneManager.GetSceneAt(i).path);}
        [MenuItem("ShadowVale/Animation review/Open Blender weapon preview")]
        public static void Open(){CheckClean();Release();EditorSceneManager.OpenScene(ScenePath);SceneChanged(default,SceneManager.GetActiveScene());Selection.activeGameObject=Object.FindFirstObjectByType<NativeWeaponPreviewPlayer>()?.gameObject;}
        static string Label(string item)=>item=="ak47"?"AK":item=="knife"?"Dao":item=="pistol_k54"?"K54":item=="rifle_mosin"?"Mosin":item=="grenade_f1"?"Lựu đạn F1":"Bom khói";
        static string ActionLabel(string action){switch(action){case "carry":return "Cầm chờ";case "walk":return "Đi";case "run":return "Chạy";case "aim":return "Ngắm";case "crouch_aim":return "Khom ngắm";case "fire":return "Bắn";case "fire_empty":return "Bắn viên cuối";case "reload":return "Nạp đạn";case "reload_empty":return "Thay băng hết đạn";case "crouch_reload":return "Khom thay đạn";case "draw":return "Rút vũ khí";case "holster":return "Cất vũ khí";case "bolt":return "Thao tác khóa nòng";case "ready":return "Chuẩn bị";case "throw":return "Ném";case "crouch_throw":return "Khom ném";case "approach":return "Tiếp cận";case "strike":return "Ra đòn";case "recover":return "Thu tay";default:return action;}}
        static void Build()
        {
            CheckClean();Directory.CreateDirectory(Root);AssetDatabase.Refresh();
            var importer=AssetImporter.GetAtPath(Model) as ModelImporter;if(importer==null)throw new Exception("Missing Blender FBX export");
            importer.animationType=ModelImporterAnimationType.Generic;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;importer.motionNodeName="Player_Rig";
            importer.importAnimation=true;importer.importVisibility=false;importer.importCameras=false;importer.importLights=false;importer.optimizeGameObjects=false;importer.animationCompression=ModelImporterAnimationCompression.Off;
            var data=Source();string sourceTake=importer.defaultClipAnimations.FirstOrDefault()?.takeName;
            importer.clipAnimations=data.Select(t=>new ModelImporterClipAnimation{name=t.item+"__"+t.action,takeName=sourceTake,firstFrame=t.start-1,lastFrame=t.end-1,loopTime=new[]{"carry","walk","run","aim","crouch_aim","ready"}.Contains(t.action),keepOriginalOrientation=true,keepOriginalPositionXZ=true,keepOriginalPositionY=true,lockRootRotation=true,lockRootPositionXZ=true,lockRootHeightY=true}).ToArray();
            importer.SaveAndReimport();
            var clips=AssetDatabase.LoadAllAssetsAtPath(Model).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")).ToArray();
            if(clips.Length!=data.Length)throw new Exception("Expected "+data.Length+" FBX takes, got "+clips.Length);
            Release();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var holder=new GameObject("BLENDER WEAPON REVIEW — isolated art");var player=holder.AddComponent<NativeWeaponPreviewPlayer>();
            var actor=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Model),holder.transform);actor.name="Nam and all props — authored Blender replay";
            player.actor=actor.GetComponent<Animator>()??actor.AddComponent<Animator>();player.actor.applyRootMotion=false;player.actor.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            foreach(var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>())skin.updateWhenOffscreen=true;
            BindMaterials(actor);foreach(var renderer in actor.GetComponentsInChildren<Renderer>(true)){renderer.enabled=true;renderer.forceRenderingOff=false;}
            var camera=new GameObject("Art review camera").AddComponent<Camera>();camera.tag="MainCamera";camera.backgroundColor=new Color(.055f,.075f,.09f);camera.clearFlags=CameraClearFlags.SolidColor;camera.nearClipPlane=.03f;camera.farClipPlane=80;player.reviewCamera=camera;
            var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);floor.name="Review floor";floor.transform.localScale=Vector3.one*4;floor.transform.position=Vector3.down*.055f;Object.DestroyImmediate(floor.GetComponent<Collider>());
            var floorMat=NewMaterial("Review slate",new Color(.07f,.09f,.10f),0,.3f);floor.GetComponent<Renderer>().sharedMaterial=floorMat;
            var sun=new GameObject("Soft studio key").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.55f;sun.color=new Color(1,.94f,.85f);sun.transform.rotation=Quaternion.Euler(38,210,0);sun.shadows=LightShadows.Soft;
            var fill=new GameObject("Cool studio fill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.65f;fill.color=new Color(.75f,.84f,1);fill.transform.rotation=Quaternion.Euler(25,35,0);
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.30f,.35f,.40f);RenderSettings.ambientEquatorColor=new Color(.18f,.21f,.24f);RenderSettings.ambientGroundColor=new Color(.10f,.11f,.10f);RenderSettings.skybox=null;
            player.takes=data.Select(t=>new NativeWeaponPreviewPlayer.Take{item=t.item,action=t.action,label=Label(t.item)+" / "+ActionLabel(t.action),clip=clips.Single(c=>c.name==t.item+"__"+t.action),loop=new[]{"carry","walk","run","aim","crouch_aim","ready"}.Contains(t.action)}).ToArray();
            player.selected=Array.FindIndex(player.takes,t=>t.item=="pistol_k54"&&t.action=="aim");player.seconds=player.Current.clip.length*.5f;player.View(2);player.Sample(player.selected,player.seconds);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),ScenePath);player.Release();AssetDatabase.SaveAssets();SceneChanged(default,SceneManager.GetActiveScene());
            File.WriteAllText(Work+"/import_report.txt","takes="+clips.Length+" avatarValid="+(player.actor.avatar!=null&&player.actor.avatar.isValid)+" generic="+!player.actor.isHuman+" buildSceneIncluded="+EditorBuildSettings.scenes.Any(s=>s.path==ScenePath)+"\n"+string.Join("\n",clips.Select(c=>c.name+" "+c.length.ToString("F3")+"s")));
        }
        static Material NewMaterial(string name,Color color,float metal,float smooth)
        {
            string path=Root+"/Materials/"+name.Replace("/","_")+".mat";Directory.CreateDirectory(Root+"/Materials");var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metal);m.SetFloat("_Smoothness",smooth);EditorUtility.SetDirty(m);return m;
        }
        static void BindMaterials(GameObject actor)
        {
            var nam=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/Player.prefab");var skin=nam.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault();
            var catalog=ShadowVale.Map01.Map01ItemCatalog.Load();
            var available=AssetDatabase.FindAssets("t:Material",new[]{"Assets/_Project/Art/Environment/Map01Items"}).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<Material>).Where(m=>m!=null).GroupBy(m=>m.name).ToDictionary(g=>g.Key,g=>g.First());
            foreach(var renderer in actor.GetComponentsInChildren<Renderer>(true)){
                if(renderer is SkinnedMeshRenderer&&skin!=null){renderer.sharedMaterials=skin.sharedMaterials;continue;}
                if(renderer.name=="AK47_Player"||renderer.name=="AK47_Magazine"){var source=catalog.Model("rifle_standard")?.GetComponentInChildren<Renderer>(true);if(source!=null){renderer.sharedMaterial=source.sharedMaterial;continue;}}
                renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>{
                    if(m==null)return NewMaterial("Native fallback",Color.gray,0,.3f);
                    if(available.TryGetValue(m.name,out var existing))return existing;
                    string n=m.name.ToLowerInvariant();if(n.Contains("knife dark grip"))return NewMaterial("Knife dark grip",new Color(.055f,.027f,.012f),0,.25f);if(n.Contains("knife blued steel"))return NewMaterial("Knife blued steel",new Color(.085f,.105f,.12f),.85f,.65f);
                    Color color=m.HasProperty("_Color")?m.color:Color.gray;return NewMaterial("Native "+m.name,color,m.HasProperty("_Metallic")?m.GetFloat("_Metallic"):0,.35f);
                }).ToArray();
            }
        }
    }
}
