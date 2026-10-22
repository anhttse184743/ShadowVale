using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Editor
{
    [InitializeOnLoad] public static class Map02CharacterSetup
    {
        const string Request="Temp/Map02CastSetup.request",Folder="Assets/_Project/Map01/Resources/Characters/Map02Roles";
        static readonly (string role,string file,string key)[] Cast={
            ("Anh Thành","Chỉ huy Thành","Thanh"),("Anh Lực","Du kích Lực","Luc"),
            ("Tâm","Anh Tâm","Tam"),("Chú Bảy","Ông 7 chèo thuyền","Bay"),
            ("Ông Sáu","Ông 6","Sau"),("Chị Lành","Chị Lành","Lanh"),("Bé Tí","Bé Tí","Ti")};
        static Map02CharacterSetup(){EditorApplication.update+=Tick;}
        static string Source(string file)=>"Assets/_Project/Art/Characters/NPCs/"+file+".fbx";
        static void Tick()
        {
            if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            var action=File.ReadAllText(Request).Trim();File.Delete(Request);
            try{if(action=="build")Build();else Inspect();}
            catch(Exception e){Directory.CreateDirectory("Logs/Map02Chapter/Cast");File.WriteAllText("Logs/Map02Chapter/Cast/setup-error.txt",e.ToString());Debug.LogException(e);}
        }
        static void Inspect()
        {
            var text=new StringBuilder();
            foreach(var entry in Cast)
            {
                var source=AssetDatabase.LoadAssetAtPath<GameObject>(Source(entry.file));
                var model=Object.Instantiate(source);try{
                    var actor=model.GetComponentInChildren<Animator>();var renderers=model.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
                    text.AppendLine(entry.role+" | "+Source(entry.file)+" | size="+bounds.size+" | min="+bounds.min+" | avatar="+(actor?.avatar!=null?actor.avatar.name:"none"));
                    text.AppendLine(string.Join(",",model.GetComponentsInChildren<Transform>().Select(t=>t.name)));
                }finally{Object.DestroyImmediate(model);}
            }
            Directory.CreateDirectory("Logs/Map02Chapter/Cast");File.WriteAllText("Logs/Map02Chapter/Cast/source-inspection.txt",text.ToString());
        }
        [MenuItem("ShadowVale/Map 2/Đồng bộ model nhân vật theo kịch bản")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play before building character assets.");
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            foreach(var entry in Cast)
            {
                var importer=(ModelImporter)AssetImporter.GetAtPath(Source(entry.file));
                if(importer.animationType!=ModelImporterAnimationType.Human||importer.avatarSetup!=ModelImporterAvatarSetup.CreateFromThisModel)
                {importer.animationType=ModelImporterAnimationType.Human;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;importer.SaveAndReimport();}
            }
            var controller=BuildController();var layout=Resources.Load<Map02ChapterLayout>("Story/Map02ChapterLayout");
            var roles=(layout.actors??Array.Empty<Map02ChapterLayout.RoleArt>()).ToList();var report=new StringBuilder();
            foreach(var entry in Cast)
            {
                var root=new GameObject(entry.role+" — Map 2 visual");
                try
                {
                    var model=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Source(entry.file)),root.transform);model.name=entry.file;
                    model.transform.localPosition=Vector3.zero;model.transform.localRotation=Quaternion.identity;
                    var actor=model.GetComponentInChildren<Animator>();
                    if(actor==null||actor.avatar==null||!actor.avatar.isValid||!actor.avatar.isHuman)throw new InvalidOperationException("Invalid humanoid avatar: "+entry.file);
                    actor.runtimeAnimatorController=controller;actor.applyRootMotion=false;actor.cullingMode=AnimatorCullingMode.AlwaysAnimate;actor.Rebind();actor.Update(0);
                    var bounds=SkinBounds(model);
                    // All seven exports share an adult-sized rig, including the seven-year-old child.
                    float height=entry.key=="Ti"?1.15f:entry.key=="Lanh"?1.58f:entry.key=="Sau"?1.62f:entry.key=="Bay"?1.65f:1.72f;
                    float scale=height/bounds.size.y;
                    model.transform.localScale*=scale;
                    model.transform.localPosition-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z)*scale;
                    actor.Rebind();actor.Update(0);
                    var prefab=PrefabUtility.SaveAsPrefabAsset(root,Folder+"/"+entry.key+".prefab");
                    roles.RemoveAll(r=>r.role==entry.role);roles.Add(new Map02ChapterLayout.RoleArt{role=entry.role,prefab=prefab});
                    report.AppendLine(entry.role+" <- "+entry.file+" | target height="+height+" | humanoid valid | "+AssetDatabase.GetAssetPath(prefab));
                }
                finally{Object.DestroyImmediate(root);}
            }
            layout.actors=roles.ToArray();EditorUtility.SetDirty(layout);AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Logs/Map02Chapter/Cast");File.WriteAllText("Logs/Map02Chapter/Cast/setup-result.txt",report.ToString());
        }
        static Bounds SkinBounds(GameObject model)
        {
            var mesh=new Mesh();bool first=true;var bounds=new Bounds();
            try{foreach(var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>()){
                skin.BakeMesh(mesh);foreach(var vertex in mesh.vertices){var at=skin.transform.TransformPoint(vertex);if(first){bounds=new Bounds(at,Vector3.zero);first=false;}else bounds.Encapsulate(at);}
            }}finally{Object.DestroyImmediate(mesh);}
            if(first||bounds.size.y<.001f)throw new InvalidOperationException("Character has no skinned geometry: "+model.name);
            return bounds;
        }
        static AnimatorController BuildController()
        {
            string path=Folder+"/Map02Civilian.controller";var existing=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);if(existing!=null)return existing;
            var source=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/_Project/Art/Characters/Animations/Controllers/AC_Player.controller");
            var idle=source.animationClips.First(c=>c.name=="Idle_Tuned");
            var walk=AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Characters/Animations/Clips/Standard Walk.fbx").OfType<AnimationClip>().First(c=>!c.name.StartsWith("__"));
            var crouch=source.animationClips.First(c=>c.name.Contains("Crouch")&&c.name.Contains("Idle"));
            var controller=AnimatorController.CreateAnimatorControllerAtPath(path);controller.AddParameter("Speed",AnimatorControllerParameterType.Float);controller.AddParameter("Sitting",AnimatorControllerParameterType.Bool);
            var machine=controller.layers[0].stateMachine;var moving=machine.AddState("Locomotion");machine.defaultState=moving;
            var tree=new BlendTree{name="Idle / Walk",blendParameter="Speed",useAutomaticThresholds=false};AssetDatabase.AddObjectToAsset(tree,controller);tree.AddChild(idle,0);tree.AddChild(walk,1.35f);moving.motion=tree;
            var sitting=machine.AddState("Sitting");sitting.motion=crouch;
            var down=moving.AddTransition(sitting);down.hasExitTime=false;down.duration=.2f;down.AddCondition(AnimatorConditionMode.If,0,"Sitting");
            var up=sitting.AddTransition(moving);up.hasExitTime=false;up.duration=.2f;up.AddCondition(AnimatorConditionMode.IfNot,0,"Sitting");
            EditorUtility.SetDirty(controller);return controller;
        }
    }
}
