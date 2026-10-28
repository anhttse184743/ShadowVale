using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using ShadowVale.Map01;
using ShadowVale.Gameplay.Combat;
using Object=UnityEngine.Object;

namespace ShadowVale.EnvironmentReview
{
    public static partial class WeaponAnimationReviewTools
    {
        public const string Preview=Root+"/WeaponAnimationReview.unity";
        static AnimationClip Clip(string name)
        {
            var candidates=AssetDatabase.FindAssets(name+" t:AnimationClip").Select(AssetDatabase.GUIDToAssetPath).Distinct().ToArray();
            foreach(var p in candidates)foreach(var c in AssetDatabase.LoadAllAssetsAtPath(p).OfType<AnimationClip>())if(c.name==name)return c;
            throw new Exception("Missing authored clip "+name);
        }
        [MenuItem("ShadowVale/Animation review/Open weapon preview")]
        public static void OpenPreview(){if(EditorApplication.isPlaying)return;if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;EditorSceneManager.OpenScene(Preview);EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(Preview);SceneView.lastActiveSceneView?.Frame(new Bounds(Vector3.up,Vector3.one*2.4f));}
        [MenuItem("ShadowVale/Animation review/Return to Map 1")]
        public static void ReturnToMap(){if(EditorApplication.isPlaying)return;if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");}
        static void CheckClean(){if(SceneManager.GetActiveScene().isDirty&&!SessionState.GetBool("WeaponReview.OwnBuildScene",false))throw new Exception("Current scene has unsaved changes. Review stopped without replacing it.");}
        static Transform Find(Transform root,string name)=>root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name==name);
        static Transform Part(Transform root,string name)=>root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name==name||t.name.StartsWith(name+".",StringComparison.Ordinal));
        static Transform Pivot(Transform root,Transform mesh,Vector3 world)
        {if(mesh==null)return null;var pivot=new GameObject(mesh.name+"_AnimationPivot").transform;pivot.SetParent(root,false);pivot.position=world;mesh.SetParent(pivot,true);return pivot;}
        static WeaponAnimationPreview.Prop Prop(string id,Transform anchor,GameObject probe)
        {
            var go=Object.Instantiate(Map01ItemCatalog.Load().Model(id),anchor,false);go.name=id;go.transform.localPosition=Vector3.zero;go.transform.localRotation=Quaternion.identity;go.transform.localScale=Vector3.one;
            foreach(var component in go.GetComponentsInChildren<MonoBehaviour>(true))Object.DestroyImmediate(component);
            foreach(var collider in go.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(collider);
            var p=new WeaponAnimationPreview.Prop{item=id,root=go.transform};var frame=Find(probe.transform,id);
            Vector3 Point(string key){var t=Part(frame,key);return t!=null?probe.transform.InverseTransformPoint(t.position):Vector3.zero;}
            p.origin=Point("Origin");p.forward=Point("Forward");p.up=Point("Up");p.grip=Point("Grip");p.support=Point("Support");p.magWell=Point("MagWell");p.boltKnob=Point("BoltKnob");p.boltAxis=Point("BoltAxis");p.pullRing=Point("PullRing");
            if(Vector3.Distance(p.origin,p.forward)<.09f||Vector3.Distance(p.origin,p.up)<.09f)throw new Exception("Invalid model reference frame "+id);
            p.slide=Part(p.root,"slide");p.magazine=Part(p.root,"magazine");p.bolt=Part(p.root,"bolt");p.ring=Part(p.root,"ring");p.lever=Part(p.root,"lever");
            if(p.magazine!=null){var bounds=p.magazine.GetComponent<Renderer>().bounds;p.magazine=Pivot(p.root,p.magazine,bounds.center);p.magHome=p.magazine.localPosition;p.magRotation=p.magazine.localRotation;}
            if(p.slide!=null)p.slideHome=p.slide.localPosition;
            if(p.bolt!=null){p.bolt=Pivot(p.root,p.bolt,p.root.TransformPoint(p.boltAxis));p.boltHome=p.bolt.localPosition;p.boltRotation=p.bolt.localRotation;p.boltContact=p.bolt.InverseTransformPoint(p.root.TransformPoint(p.boltKnob));}
            if(p.ring!=null){p.ringHome=p.ring.localPosition;p.ringRotation=p.ring.localRotation;}if(p.lever!=null){p.leverHome=p.lever.localPosition;p.leverRotation=p.lever.localRotation;}
            return p;
        }
        static void BuildReview()
        {
            CheckClean();ReleasePreviewGraphs();Directory.CreateDirectory(Root+"/Clips");AssetDatabase.Refresh();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            SessionState.SetBool("WeaponReview.OwnBuildScene",true);
            var root=new GameObject("Weapon animation review — isolated art scene");var driver=root.AddComponent<WeaponAnimationPreview>();
            var nam=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Player),root.transform);nam.name="Nam — animation review";nam.transform.SetLocalPositionAndRotation(Vector3.zero,Quaternion.identity);
            var a=nam.GetComponentInChildren<Animator>();var originalCarrier=a.GetComponent<ReloadMagazineCarrier>();var carrierTakes=originalCarrier!=null?originalCarrier.Takes:null;
            foreach(var b in nam.GetComponentsInChildren<MonoBehaviour>(true))Object.DestroyImmediate(b);
            foreach(var c in nam.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(c);
            a.applyRootMotion=false;a.cullingMode=AnimatorCullingMode.AlwaysAnimate;driver.actor=a;
            var anchor=Find(a.transform,"WeaponAnchor");if(anchor==null)throw new Exception("Nam has no authored WeaponAnchor");
            var grip=AssetDatabase.LoadAssetAtPath<WeaponGripConfig>("Assets/_Project/ScriptableObjects/GameSettings/WeaponGripConfig.asset");
            var ak=Object.Instantiate(Map01ItemCatalog.Load().Model("rifle_standard"),anchor);grip.Apply(ak.transform,WeaponKind.Rifle);ak.name="AK — original game model";
            var knife=Object.Instantiate(Map01ItemCatalog.Load().Model("knife"),anchor);grip.Apply(knife.transform,WeaponKind.Knife);knife.name="Knife — original game model";
            foreach(var b in ak.GetComponentsInChildren<MonoBehaviour>(true))Object.DestroyImmediate(b);foreach(var b in knife.GetComponentsInChildren<MonoBehaviour>(true))Object.DestroyImmediate(b);
            driver.carrier=a.gameObject.AddComponent<ReloadMagazineCarrier>();driver.carrier.enabled=false;driver.carrier.Configure(a,a.GetBoneTransform(HumanBodyBones.LeftHand),anchor,carrierTakes);driver.carrier.Bind(ak.transform);
            var probe=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Props/ItemGripFrames.fbx"));probe.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            driver.props=new[]{new WeaponAnimationPreview.Prop{item="rifle_standard",root=ak.transform},new WeaponAnimationPreview.Prop{item="knife",root=knife.transform},Prop("pistol_k54",anchor,probe),Prop("rifle_mosin",anchor,probe),Prop("grenade_f1",anchor,probe),Prop("smoke_bomb",anchor,probe)};
            var mosin=Array.Find(driver.props,p=>p.item=="rifle_mosin");var round=GameObject.CreatePrimitive(PrimitiveType.Capsule);round.name="Single round — visual reload prop";Object.DestroyImmediate(round.GetComponent<Collider>());round.transform.SetParent(mosin.root,false);round.transform.localScale=new Vector3(.008f,.037f,.008f);round.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Environment/Map01Items/Materials/Brass.mat");mosin.round=round.transform;round.SetActive(false);
            Object.DestroyImmediate(probe);
            CalibrateFingers(a);driver.handReference=rightHandFrame;driver.leftHandReference=leftRifleFrame;
            AnimationMode.StartAnimationMode();
            try{
                Sample(a,Clip("Nam_Rifle_AimIdle"),.2f);var muzzle=Find(ak.transform,"Muzzle");
                var gunForward=muzzle!=null?muzzle.forward:ak.transform.forward;var turn=Quaternion.FromToRotation(Vector3.ProjectOnPlane(gunForward,Vector3.up).normalized,Vector3.forward);
                var takes=new List<WeaponAnimationPreview.Take>();
                void Existing(string item,string label,string action,string clip,bool loop=false,string magazineState=null)=>takes.Add(new WeaponAnimationPreview.Take{item=item,label=label,action=action,clip=Clip(clip),loop=loop,magazineState=magazineState});
                Existing("rifle_standard","AK / cầm súng", "carry","Nam_Rifle_Idle",true);Existing("rifle_standard","AK / đi bộ", "walk","Nam_Rifle_Walk_F",true);Existing("rifle_standard","AK / chạy", "run","Nam_Rifle_Run",true);
                Existing("rifle_standard","AK / ngắm", "aim","Nam_Rifle_AimIdle",true);Existing("rifle_standard","AK / bắn", "fire","Nam_Rifle_Fire");
                Existing("rifle_standard","AK / thay băng đang dùng", "reload","Nam_Rifle_ReloadTactical",false,"Reload_Rifle");Existing("rifle_standard","AK / thay băng hết đạn", "reload_empty","Nam_Rifle_Reload",false,"ReloadEmpty_Rifle");Existing("rifle_standard","AK / nạp khi khom", "crouch_reload","Nam_Crouch_Reload",false,"ReloadCrouch_Rifle");
                foreach(var x in new[]{("carry","Nam_Knife_Idle"),("walk","Nam_Knife_Walk"),("run","Nam_Knife_Run"),("crouch","Nam_Knife_Sneak_Idle"),("crouch_walk","Nam_Knife_Sneak_Walk")})Existing("knife","Dao / "+x.Item1,x.Item1,x.Item2,true);
                Existing("knife","Dao / đâm — clip gốc, tốc độ game 2,4×","stab","Stab");
                foreach(var item in new[]{"pistol_k54","rifle_mosin"}){
                    var actions=item=="pistol_k54"?new[]{"carry","walk","run","aim","crouch_aim","fire","fire_empty","reload","reload_empty","draw","holster"}:new[]{"carry","walk","run","aim","crouch_aim","fire","bolt","reload","draw","holster"};
                    foreach(var action in actions){float duration=action.StartsWith("reload")?(item=="pistol_k54"?2.4f:4.8f):action=="bolt"?1.6f:action.StartsWith("fire")?.40f:action=="draw"||action=="holster"?.75f:1.4f;
                        var take=new WeaponAnimationPreview.Take{item=item,label=(item=="pistol_k54"?"K54":"Mosin PU")+" / "+action,action=action,newPose=true,loop=new[]{"carry","walk","run","aim","crouch_aim"}.Contains(action)};
                        take.clip=Bake(driver,take,duration);takes.Add(take);}
                }
                foreach(var item in new[]{"grenade_f1","smoke_bomb"})foreach(var action in new[]{"ready","throw","crouch_throw"}){
                    var take=new WeaponAnimationPreview.Take{item=item,label=(item=="grenade_f1"?"Lựu đạn F1":"Bom khói")+" / "+action,action=action,newPose=true,loop=action=="ready"};take.clip=Bake(driver,take,action=="ready"?1.2f:2.8f);takes.Add(take);}
                driver.takes=takes.ToArray();
            }finally{AnimationMode.StopAnimationMode();}
            a.runtimeAnimatorController=null;driver.selected=0;driver.seconds=0;
            foreach(var skin in a.GetComponentsInChildren<SkinnedMeshRenderer>(true))skin.updateWhenOffscreen=true;
            var camera=new GameObject("Review camera — Full HD").AddComponent<Camera>();camera.tag="MainCamera";camera.nearClipPlane=.05f;camera.farClipPlane=60;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.075f,.095f,.10f);driver.reviewCamera=camera;driver.View(0);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Neutral studio floor";floor.transform.SetPositionAndRotation(new Vector3(0,-.055f,0),Quaternion.identity);floor.transform.localScale=new Vector3(20,.1f,20);
            var mat=AssetDatabase.LoadAssetAtPath<Material>(Root+"/ReviewFloor.mat");if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Review floor"};AssetDatabase.CreateAsset(mat,Root+"/ReviewFloor.mat");}mat.color=new Color(.18f,.21f,.19f);EditorUtility.SetDirty(mat);floor.GetComponent<Renderer>().sharedMaterial=mat;
            var key=new GameObject("Soft key").AddComponent<Light>();key.type=LightType.Directional;key.intensity=1.4f;key.shadows=LightShadows.Soft;key.transform.rotation=Quaternion.Euler(48,-30,0);
            var fill=new GameObject("Fill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.5f;fill.transform.rotation=Quaternion.Euler(30,145,0);
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.58f,.62f,.66f);
            foreach(var p in driver.props)p.root.gameObject.SetActive(p.item=="rifle_standard");driver.Sample(0,0);
            EditorSceneManager.SaveScene(scene,Preview);AssetDatabase.SaveAssets();EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(Preview);
            SessionState.EraseBool("WeaponReview.OwnBuildScene");
            File.WriteAllLines(Work+"/takes.txt",driver.takes.Select(t=>t.item+" | "+t.action+" | "+t.clip.length.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+"s | "+AssetDatabase.GetAssetPath(t.clip)));
        }
        static void Sample(Animator a,AnimationClip clip,float time){AnimationMode.BeginSampling();AnimationMode.SampleAnimationClip(a.gameObject,clip,time);AnimationMode.EndSampling();}
        static AnimationClip Bake(WeaponAnimationPreview d,WeaponAnimationPreview.Take take,float seconds)
        {
            var a=d.actor;var baseClip=Clip(take.action.Contains("crouch")?"Nam_Crouch_Idle":take.action=="walk"?"Nam_Rifle_Walk_F":take.action=="run"?"Nam_Rifle_Run":"Nam_Rifle_Idle");
            var fingers=a.GetComponentsInChildren<Transform>(true).Where(t=>ShadowVale.Editor.StoryAnimationSetup.IsGenericBone(t.name)).ToArray();
            var names=HumanTrait.MuscleName;var curves=new Dictionary<string,AnimationCurve>();foreach(var name in names)curves[name]=new AnimationCurve();foreach(var name in new[]{"RootT.x","RootT.y","RootT.z","RootQ.x","RootQ.y","RootQ.z","RootQ.w"})curves[name]=new AnimationCurve();
            var generic=fingers.ToDictionary(t=>t,t=>new[]{new AnimationCurve(),new AnimationCurve(),new AnimationCurve(),new AnimationCurve()});
            var prop=Array.Find(d.props,p=>p.item==take.item);var pose=new HumanPose();using(var handler=new HumanPoseHandler(a.avatar,a.transform)){
                int frames=Mathf.CeilToInt(seconds*24);
                for(int frame=0;frame<=frames;frame++){
                    float u=frame/(float)frames,time=u*seconds;var sampleClip=baseClip;float sampleTime=take.action=="walk"||take.action=="run"?u*baseClip.length:0;
                    if((take.item=="grenade_f1"||take.item=="smoke_bomb")&&take.action.Contains("throw")&&u>.46f){sampleClip=Clip("Nam_Throw");sampleTime=Mathf.Lerp(.42f,.9f,Mathf.InverseLerp(.46f,.85f,u))*sampleClip.length;}
                    Sample(a,sampleClip,sampleTime);
                    WeaponReviewMotion.Targets(take.item,take.action,u,out var right,out var left,out float tilt);
                    var chest=a.GetBoneTransform(HumanBodyBones.UpperChest)??a.GetBoneTransform(HumanBodyBones.Chest);var head=a.GetBoneTransform(HumanBodyBones.Head);var headRotation=head.rotation;
                    if(take.item=="rifle_mosin"||take.item=="pistol_k54"){
                        var l=a.GetBoneTransform(HumanBodyBones.LeftUpperArm);var r=a.GetBoneTransform(HumanBodyBones.RightUpperArm);var side=Vector3.ProjectOnPlane(r.position-l.position,Vector3.up);
                        var desired=Quaternion.Euler(0,take.item=="rifle_mosin"?45:0,0)*Vector3.right;chest.rotation=Quaternion.FromToRotation(side.normalized,desired)*chest.rotation;head.rotation=headRotation;
                    }
                    Quaternion rr=Quaternion.Euler(tilt,0,0)*d.handReference;var lr=take.item=="pistol_k54"?leftPistolFrame:d.leftHandReference;
                    WeaponReviewMotion.Arm(a,true,d.transform.TransformPoint(right),rr);WeaponReviewMotion.Arm(a,false,d.transform.TransformPoint(left),lr);
                    d.PoseProp(prop,take,u);WeaponReviewMotion.Contacts(a,d.transform,prop,take.action,u,d.palmOffset);
                    PoseFingers(take.item,take.action,u);
                    handler.GetHumanPose(ref pose);
                    for(int i=0;i<names.Length;i++)curves[names[i]].AddKey(time,pose.muscles[i]);
                    var p=pose.bodyPosition;var q=pose.bodyRotation;var values=new[]{p.x,p.y,p.z,q.x,q.y,q.z,q.w};int k=0;foreach(var name in new[]{"RootT.x","RootT.y","RootT.z","RootQ.x","RootQ.y","RootQ.z","RootQ.w"})curves[name].AddKey(time,values[k++]);
                    foreach(var t in fingers){var r=t.localRotation;var c=generic[t];c[0].AddKey(time,r.x);c[1].AddKey(time,r.y);c[2].AddKey(time,r.z);c[3].AddKey(time,r.w);}
                }
            }
            var clip=new AnimationClip{name="Nam_"+take.item+"_"+take.action,frameRate=24};
            foreach(var pair in curves)clip.SetCurve("",typeof(Animator),pair.Key,Reduce(pair.Value));
            foreach(var pair in generic){string curvePath=AnimationUtility.CalculateTransformPath(pair.Key,a.transform);for(int i=0;i<4;i++)clip.SetCurve(curvePath,typeof(Transform),"m_LocalRotation."+"xyzw"[i],Reduce(pair.Value[i]));}
            clip.EnsureQuaternionContinuity();var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=take.loop;settings.loopBlend=take.loop;settings.keepOriginalPositionY=true;settings.keepOriginalPositionXZ=true;settings.keepOriginalOrientation=true;AnimationUtility.SetAnimationClipSettings(clip,settings);
            var path=Root+"/Clips/"+clip.name+".anim";var old=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);if(old!=null){EditorUtility.CopySerialized(clip,old);Object.DestroyImmediate(clip);clip=old;EditorUtility.SetDirty(clip);}else AssetDatabase.CreateAsset(clip,path);return clip;
        }
        static AnimationCurve Reduce(AnimationCurve source)
        {
            var keys=source.keys;if(keys.Length>2&&keys.All(k=>Mathf.Abs(k.value-keys[0].value)<.00001f))return AnimationCurve.Linear(keys[0].time,keys[0].value,keys[^1].time,keys[^1].value);
            for(int i=0;i<source.length;i++){AnimationUtility.SetKeyLeftTangentMode(source,i,AnimationUtility.TangentMode.ClampedAuto);AnimationUtility.SetKeyRightTangentMode(source,i,AnimationUtility.TangentMode.ClampedAuto);}return source;
        }
    }
}
