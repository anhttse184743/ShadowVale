using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Editor
{
    /// <summary>Non-destructive derived actors and 30 fps, Avatar-calibrated performances.</summary>
    public static class Map02DayOneBuilder
    {
        const string Root="Assets/_Project/Art/Map02DayOne";
        const string Data="Assets/_Project/Map01/Resources/Story/Map02DayOneArt.asset";
        static AnimationClip LoadClip(string path)=>AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview"));
        static AnimationClip idle,seat,walk;static Animator actor;static GameObject model;
        static AnimationCurve[] muscle;static AnimationCurve[] root;
        [Serializable] sealed class BoneFrame {public string name;public float[] matrix;}
        [Serializable] sealed class Frame {public float time;public BoneFrame[] bones;}
        [Serializable] sealed class Export {public string action;public string source;public int fps=30;public Frame[] frames;}
        static Vector3 frameOrigin,frameRight,frameForward;
        static Vector3 Local(Vector3 p)=>frameOrigin+frameRight*p.x+Vector3.up*p.y+frameForward*p.z;
        static void SolveArm(bool left,Vector3 target,Vector3 pole,float weight)
        {
            var u=actor.GetBoneTransform(left?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm);var e=actor.GetBoneTransform(left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm);var h=actor.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
            target=Vector3.Lerp(h.position,Local(target),weight);var origin=u.position;float a=Vector3.Distance(origin,e.position),b=Vector3.Distance(e.position,h.position);var delta=target-origin;float d=Mathf.Clamp(delta.magnitude,Mathf.Abs(a-b)+.015f,a+b-.015f);var direction=delta.normalized;
            var side=Vector3.ProjectOnPlane(Local(pole)-origin,direction).normalized;float adjacent=(a*a-b*b+d*d)/(2*d);var elbow=origin+direction*adjacent+side*Mathf.Sqrt(Mathf.Max(0,a*a-adjacent*adjacent));
            u.rotation=Quaternion.FromToRotation(e.position-u.position,elbow-origin)*u.rotation;e.rotation=Quaternion.FromToRotation(h.position-e.position,target-e.position)*e.rotation;
        }
        static float Ease(float t,float start,float end)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(start,end,t));
        static AnimationClip Performance(string id,float seconds)
        {
            var output=new AnimationClip{name=id,frameRate=30};muscle=HumanTrait.MuscleName.Select(_=>new AnimationCurve()).ToArray();root=Enumerable.Range(0,7).Select(_=>new AnimationCurve()).ToArray();var frames=new List<Frame>();
            using var sampler=new Map01CinematicPose(actor,null,false);using var handler=new HumanPoseHandler(actor.avatar,actor.transform);
            var pose=new HumanPose();sampler.Sample(idle,0);frameRight=Vector3.ProjectOnPlane(actor.GetBoneTransform(HumanBodyBones.RightUpperArm).position-actor.GetBoneTransform(HumanBodyBones.LeftUpperArm).position,Vector3.up).normalized;frameForward=Vector3.Cross(frameRight,Vector3.up).normalized;frameOrigin=(actor.GetBoneTransform(HumanBodyBones.LeftFoot).position+actor.GetBoneTransform(HumanBodyBones.RightFoot).position)*.5f;frameOrigin.y=0;
            for(int f=0;f<=Mathf.RoundToInt(seconds*30);f++)
            {
                float t=f/30f;bool sitting=id.Contains("Meal")||id.Contains("BandageHung")||id.Contains("Rest");sampler.Sample(sitting?seat:id.Contains("Walk")?walk:idle,t,true);
                float ramp=id.StartsWith("Carry")||id.StartsWith("Assist")||id.Contains("Injured")||id.Contains("Meal")||id.Contains("Bandage")?1:Ease(t,0,.7f);float breathing=Mathf.Sin(t*1.8f)*.006f;
                if(id.StartsWith("Carry")||id=="PlaceCrate")
                {float y=.88f-(id=="PlaceCrate"?Ease(t,.7f,1.7f)*.3f:0);SolveArm(true,new Vector3(-.275f,y,.36f),new Vector3(-.55f,.95f,.05f),ramp);SolveArm(false,new Vector3(.275f,y,.36f),new Vector3(.55f,.95f,.05f),ramp);}
                if(id=="InjuredIdle"||id=="InjuredWalk"||id=="BandageHung"||id=="MealHung")
                {SolveArm(false,new Vector3(.12f,sitting?.73f:1.0f,.21f),new Vector3(.36f,sitting?.6f:.95f,.05f),ramp);SolveArm(true,new Vector3(.09f,sitting?.72f:1.01f,.25f),new Vector3(-.42f,sitting?.62f:.92f,.08f),ramp);}
                if(id.StartsWith("Assist"))SolveArm(false,new Vector3(.4f,1.22f,.31f),new Vector3(.5f,1.02f,.07f),ramp);
                if(id=="Medicine")
                {float reach=Ease(t,.5f,1.5f)*(1-Ease(t,3.3f,4));SolveArm(true,new Vector3(-.12f,1.02f,.36f),new Vector3(-.48f,.95f,.02f),reach);SolveArm(false,new Vector3(.12f,.96f,.47f),new Vector3(.48f,.85f,.03f),reach);}
                if(id=="Hammer")
                {float stroke=(1-Mathf.Cos(Mathf.Repeat(t,1)*Mathf.PI*2))*.5f;SolveArm(false,new Vector3(.24f,1.05f+stroke*.4f,.42f-stroke*.12f),new Vector3(.5f,1.1f,.05f),ramp);}
                if(id=="BandageLanh")
                {SolveArm(true,new Vector3(-.15f,.82f,.43f),new Vector3(-.4f,.8f,.08f),ramp);SolveArm(false,new Vector3(.05f,.83f+Mathf.Sin(t*2)*.025f,.4f),new Vector3(.4f,.83f,.08f),ramp);}
                if(id=="MealNam")SolveArm(true,new Vector3(-.16f,.76f,.45f+Ease(t,2,3)*.12f),new Vector3(-.4f,.67f,.12f),ramp);
                handler.GetHumanPose(ref pose);
                if(id=="RestHung"){float recline=Ease(t,.7f,3.5f);pose.bodyRotation=Quaternion.Slerp(pose.bodyRotation,Quaternion.Euler(-90,0,0),recline);pose.bodyPosition=Vector3.Lerp(pose.bodyPosition,new Vector3(0,.23f,0),recline);for(int i=0;i<pose.muscles.Length;i++)if(HumanTrait.MuscleName[i].Contains("Leg")||HumanTrait.MuscleName[i].Contains("Foot"))pose.muscles[i]=Mathf.Lerp(pose.muscles[i],0,recline);}
                for(int i=0;i<pose.muscles.Length;i++)
                {
                    float value=pose.muscles[i];string n=HumanTrait.MuscleName[i];
                    if(n.Contains("Hand")&&(n.Contains("Down-Up")||n.Contains("In-Out")))value=Mathf.Clamp(value,-.35f,.35f);
                    if(n.Contains("Stretched"))value=id=="Carry"||id=="Hammer"?-.45f:Mathf.Clamp(value,-.45f,.65f);
                    pose.muscles[i]=Mathf.Clamp(value,-1,1);muscle[i].AddKey(t,pose.muscles[i]);
                }
                Vector3 body=pose.bodyPosition;body.y+=breathing;float[] values={body.x,body.y,body.z,pose.bodyRotation.x,pose.bodyRotation.y,pose.bodyRotation.z,pose.bodyRotation.w};for(int i=0;i<7;i++)root[i].AddKey(t,values[i]);
                pose.bodyPosition=body;handler.SetHumanPose(ref pose);
                // Save the same sampled bone transforms for the editable Blender counterpart.
                frames.Add(new Frame{time=t,bones=actor.GetComponentsInChildren<Transform>().Where(b=>b!=actor.transform).Select(b=>{var m=actor.transform.worldToLocalMatrix*b.localToWorldMatrix;return new BoneFrame{name=b.name,matrix=Enumerable.Range(0,16).Select(i=>m[i]).ToArray()};}).ToArray()});
            }
            for(int i=0;i<muscle.Length;i++)output.SetCurve("",typeof(Animator),HumanTrait.MuscleName[i],muscle[i]);
            string[] keys={"RootT.x","RootT.y","RootT.z","RootQ.x","RootQ.y","RootQ.z","RootQ.w"};for(int i=0;i<7;i++)output.SetCurve("",typeof(Animator),keys[i],root[i]);
            output.EnsureQuaternionContinuity();var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"/"+id+".anim");if(existing!=null){EditorUtility.CopySerialized(output,existing);Object.DestroyImmediate(output);output=existing;}else AssetDatabase.CreateAsset(output,Root+"/"+id+".anim");
            File.WriteAllText("SourceArt/Map02DayOne/"+id+".json",JsonUtility.ToJson(new Export{action=id,source="Assets/_Project/Prefabs/Player/Player.prefab",frames=frames.ToArray()}));return output;
        }
        public static void AuditEnvironment()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");
            var point=new Vector3(-48.46f,1.03f,-30.49f);Directory.CreateDirectory("Logs/Map02DayOne");
            File.WriteAllLines("Logs/Map02DayOne/family_geometry.txt",Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r=>Vector3.Distance(r.bounds.center,point)<15).Select(r=>r.name+" center="+r.bounds.center+" size="+r.bounds.size));
            var go=new GameObject("Audit camera");var camera=go.AddComponent<Camera>();camera.transform.SetPositionAndRotation(point+Vector3.up*30,Quaternion.Euler(90,0,0));camera.orthographic=true;camera.orthographicSize=12;camera.farClipPlane=60;var texture=new RenderTexture(1024,1024,24);camera.targetTexture=texture;camera.Render();var before=RenderTexture.active;RenderTexture.active=texture;var image=new Texture2D(1024,1024,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1024,1024),0,0);image.Apply();File.WriteAllBytes("Logs/Map02DayOne/family_top.png",image.EncodeToPNG());RenderTexture.active=before;camera.targetTexture=null;Object.DestroyImmediate(go);Object.DestroyImmediate(texture);Object.DestroyImmediate(image);
        }
        public static void Build()
        {
            Directory.CreateDirectory(Root);Directory.CreateDirectory("SourceArt/Map02DayOne");AssetDatabase.Refresh();
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/Player.prefab");model=Object.Instantiate(prefab);model.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);actor=model.GetComponentInChildren<Animator>();
            walk=LoadClip("Assets/_Project/Art/Characters/Animations/Nam/Nam_Rifle_Walk_F.fbx");idle=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Art/Characters/Animations/Generated/Idle_Tuned.anim");seat=LoadClip("Assets/_Project/Art/Characters/Animations/Extraction/Nam_Boat_Seated_Travel.fbx");
            var data=AssetDatabase.LoadAssetAtPath<Map02DayOneArt>(Data);if(data==null){data=ScriptableObject.CreateInstance<Map02DayOneArt>();AssetDatabase.CreateAsset(data,Data);}
            try{data.actions=new[]{"Carry","CarryWalk","PlaceCrate","InjuredIdle","InjuredWalk","Assist","AssistWalk","Medicine","Hammer","BandageHung","BandageLanh","MealNam","MealHung","RestNam","RestHung"}.Select(id=>new Map02DayOneArt.Action{id=id,clip=Performance(id,id=="Medicine"?4:4)}).ToArray();}
            finally{Object.DestroyImmediate(model);}
            var layout=AssetDatabase.LoadAssetAtPath<Map02ChapterLayout>("Assets/_Project/Map01/Resources/Story/Map02ChapterLayout.asset");var arts=(layout.actors??Array.Empty<Map02ChapterLayout.RoleArt>()).ToList();
            var pairs=new[]{("Anh Lực","Du kích Lực"),("Chị Lành","Chị Lành"),("Ông Sáu","Ông 6"),("Bé Tí","Bé Tí"),("Chú Bảy","Ông 7 chèo thuyền"),("Tâm","Anh Tâm"),("Hùng","hung"),("Anh Thành","Chỉ huy Thành")};
            foreach(var (role,name) in pairs)
            {
                string source="Assets/_Project/Art/Characters/NPCs/"+name+".fbx",copy=Root+"/"+name+".fbx";if(!File.Exists(copy))File.Copy(source,copy);AssetDatabase.ImportAsset(copy,ImportAssetOptions.ForceSynchronousImport);
                var importer=(ModelImporter)AssetImporter.GetAtPath(copy);importer.importCameras=false;importer.importLights=false;importer.animationType=ModelImporterAnimationType.Human;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;importer.optimizeGameObjects=false;importer.SaveAndReimport();
                var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(copy));
                try
                {
                    var animator=go.GetComponent<Animator>();if(animator==null||animator.avatar==null||!animator.avatar.isValid||!animator.avatar.isHuman)throw new Exception("Invalid derived Avatar: "+role);
                    var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Characters/NPCs/Materials/"+(role=="Hùng"?"hung - Wounded":name+" - Material")+".mat");if(material==null)throw new Exception("Missing material: "+role);
                    foreach(var r in go.GetComponentsInChildren<Renderer>()){r.sharedMaterial=material;if(r is SkinnedMeshRenderer skin)skin.updateWhenOffscreen=true;}
                    string controllerPath=Root+"/"+name+".controller";var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);if(controller==null){controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);controller.AddParameter("Speed",AnimatorControllerParameterType.Float);var machine=controller.layers[0].stateMachine;var still=machine.AddState("Idle");still.motion=idle;var walking=machine.AddState("Walk");walking.motion=LoadClip("Assets/_Project/Art/Characters/Animations/Nam/Nam_Rifle_Walk_F.fbx");var start=still.AddTransition(walking);start.hasExitTime=false;start.duration=.2f;start.AddCondition(AnimatorConditionMode.Greater,.1f,"Speed");var stop=walking.AddTransition(still);stop.hasExitTime=false;stop.duration=.2f;stop.AddCondition(AnimatorConditionMode.Less,.1f,"Speed");}
                    animator.runtimeAnimatorController=controller;var wrapper=new GameObject(name+" DayOne derived");go.transform.SetParent(wrapper.transform,false);if(role=="Bé Tí")go.transform.localScale*=.64f;var saved=PrefabUtility.SaveAsPrefabAsset(wrapper,Root+"/"+name+".prefab");Object.DestroyImmediate(wrapper);arts.RemoveAll(a=>a.role==role);arts.Add(new Map02ChapterLayout.RoleArt{role=role,prefab=saved});
                }finally{if(go!=null)Object.DestroyImmediate(go);}
            }
            layout.actors=arts.ToArray();if(!layout.sites.Any(s=>s.id=="dayOneFamily"))layout.sites=layout.sites.Concat(new[]{new Map02ChapterLayout.Site{id="dayOneFamily",position=new Vector3(-50.7f,1.03f,-35.7f)}}).ToArray();EditorUtility.SetDirty(layout);EditorUtility.SetDirty(data);AssetDatabase.SaveAssets();Debug.Log("MAP02_DAYONE_ART_BUILD_PASS");
        }
    }
}
