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
    public static class NamMotionBuilder
    {
        public const string Root="Assets/_Project/Art/NamMotionV3";
        public const string AssetPath="Assets/_Project/Map01/Resources/Characters/NamMotionV3.asset";
        public const string Work="Docs/Reviews/AnimationRefresh_Unity_20261022";
        const string Export="SourceArt/AnimationRefresh_20261022/UnityExport";
        [Serializable] sealed class Receipt {public string sourceHash;public int frames,fps,bones;public bool rootFlightRemoved,rootXZRemoved;public Take[] clips;public PropSpec[] props;public Visibility[] visibility;}
        [Serializable] sealed class Take {public string label,prop;public int start,end,frames;}
        [Serializable] sealed class PropSpec {public string key,root;public string[] parts;}
        [Serializable] sealed class Visibility {public string name;public bool[] visible;}
        static int Depth(Transform t){int n=0;while(t.parent!=null){n++;t=t.parent;}return n;}
        static Dictionary<string,Transform> Map(GameObject o)=>o.GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
        static Map01PistolAnimationData.Pose Pose(Matrix4x4 m)=>new(){position=m.GetColumn(3),rotation=m.rotation,scale=m.lossyScale};
        static int BindPose(GameObject go)
        {
            var matrices=new Dictionary<Transform,Matrix4x4>();
            foreach(var skin in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                for(int i=0;i<skin.bones.Length;i++)if(skin.bones[i]!=null&&!matrices.ContainsKey(skin.bones[i]))
                    matrices[skin.bones[i]]=skin.transform.localToWorldMatrix*skin.sharedMesh.bindposes[i].inverse;
            foreach(var x in matrices.OrderBy(x=>Depth(x.Key)))Pose(x.Key.parent.worldToLocalMatrix*x.Value).Apply(x.Key);
            return matrices.Count;
        }
        static string Key(string prop)=>prop=="F1"?"f1":prop=="Stone"?"stone":prop=="Smoke"?"smoke":prop=="Knife"?"knife":prop=="Pistol"?"pistol":"unarmed";
        static string Id(Take t)
        {
            var l=t.label;var k=Key(t.prop);
            if(l.Contains("NHAY"))return "jump_"+k;
            if(l.StartsWith("01 "))return "pistol_carry";
            if(l.StartsWith("02 "))return "pistol_raise";
            if(l.StartsWith("03 "))return "pistol_aim";
            if(l.StartsWith("04 "))return "pistol_fire";
            if(l.StartsWith("05 "))return "pistol_reload";
            if(l.StartsWith("06 "))return "pistol_lower";
            if(l.StartsWith("07 "))return "knife_idle";
            if(l.StartsWith("08 "))return "knife_attack";
            if(l.StartsWith("09 ")||l.StartsWith("10 "))return k+(l.EndsWith("/ di")?"_walk":l.EndsWith("/ chay")?"_run":"_crouch");
            if(l.StartsWith("16 "))return "pistol_aim_walk";
            if(l.StartsWith("17 "))return "knife_draw";
            if(l.StartsWith("18 "))return "knife_holster";
            if(l.StartsWith("19 "))return k+"_crouch_"+(l.Contains("chuan bi")?"prepare":l.Contains("lay da")?"wind":l.Contains("ngam")?"aim":"throw");
            return k+"_"+(l.StartsWith("11 ")?"prepare":l.StartsWith("12 ")?"wind":l.StartsWith("13 ")?"aim":"throw");
        }
        [MenuItem("ShadowVale/Characters/Install approved Nam motion v3")]
        public static void Build()
        {
            Directory.CreateDirectory(Root);Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));Directory.CreateDirectory(Work);
            var receipt=JsonUtility.FromJson<Receipt>(File.ReadAllText(Export+"/export.json"));
            if(receipt.bones!=62||receipt.frames!=3208||!receipt.rootFlightRemoved)throw new Exception("Wrong Nam v3 export");
            var model=Root+"/Nam_AnimationRefresh_v3.fbx";File.Copy(Export+"/Nam_AnimationRefresh_v3.fbx",model,true);AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var importer=(ModelImporter)AssetImporter.GetAtPath(model);importer.animationType=ModelImporterAnimationType.Generic;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;importer.importAnimation=true;importer.importVisibility=false;importer.importCameras=false;importer.importLights=false;importer.optimizeGameObjects=false;importer.animationCompression=ModelImporterAnimationCompression.Off;importer.isReadable=true;
            string take=importer.defaultClipAnimations.First().takeName;
            // Blender keys start at frame 1. Including frame 0 freezes the previous take at every boundary.
            importer.clipAnimations=new[]{new ModelImporterClipAnimation{name="Nam_v3_Source60",takeName=take,firstFrame=1,lastFrame=receipt.frames,keepOriginalOrientation=true,keepOriginalPositionXZ=true,keepOriginalPositionY=true,lockRootRotation=true,lockRootPositionXZ=true,lockRootHeightY=true}};
            importer.SaveAndReimport();
            var source=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(model));
            var target=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/Player.prefab"));
            try
            {
                foreach(var b in target.GetComponentsInChildren<MonoBehaviour>(true))b.enabled=false;
                foreach(var a in source.GetComponentsInChildren<Animator>(true))a.enabled=false;
                int sourceBinds=BindPose(source),targetBinds=BindPose(target);
                var sm=Map(source);var tm=Map(target);var sh=sm["Hips"];var th=tm["Hips"];
                var witness=source.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(x=>x.name=="EXPORT_BIND_WITNESS_DO_NOT_RENDER");
                var names=witness.bones.Select(b=>b.name).Distinct().Where(tm.ContainsKey).OrderBy(n=>Depth(tm[n])).ToArray();
                if(names.Length!=62)throw new Exception("Nam motion must bind the existing 62 bones, got "+names.Length);
                Matrix4x4 InHip(Transform h,Transform b)=>h.worldToLocalMatrix*b.localToWorldMatrix;
                Quaternion Basis(Transform h,Dictionary<string,Transform> map)
                {
                    Vector3 up=InHip(h,map["Neck"]).GetColumn(3),right=(Vector3)InHip(h,map["RightArm"]).GetColumn(3)-(Vector3)InHip(h,map["LeftArm"]).GetColumn(3);
                    return Quaternion.LookRotation(Vector3.Cross(right,up).normalized,up.normalized);
                }
                float unit=Vector3.Distance(InHip(th,tm["LeftForeArm"]).GetColumn(3),InHip(th,tm["LeftHand"]).GetColumn(3))/Vector3.Distance(InHip(sh,sm["LeftForeArm"]).GetColumn(3),InHip(sh,sm["LeftHand"]).GetColumn(3));
                var units=Matrix4x4.Rotate(Basis(th,tm)*Quaternion.Inverse(Basis(sh,sm)))*Matrix4x4.Scale(Vector3.one*unit);
                var sourceHipRest=sh.worldToLocalMatrix;var targetHipRest=th.localToWorldMatrix;
                var sr=names.ToDictionary(n=>n,n=>units*sourceHipRest*sm[n].localToWorldMatrix*units.inverse);
                var tr=names.ToDictionary(n=>n,n=>th.worldToLocalMatrix*tm[n].localToWorldMatrix);
                var correction=tr["RightHand"].inverse*units*InHip(sh,sm["RightHand"]);
                var data=ScriptableObject.CreateInstance<NamMotionData>();data.sourceHash=receipt.sourceHash;data.frames=receipt.frames;data.fps=receipt.fps;data.rootFlightRemoved=receipt.rootFlightRemoved;data.rootXZRemoved=receipt.rootXZRemoved;
                data.clips=receipt.clips.Select(t=>new NamMotionData.Clip{id=Id(t),label=t.label,prop=t.prop,start=t.start,end=t.end}).ToArray();
                if(data.clips.Select(c=>c.id).Distinct().Count()!=47)throw new Exception("Motion clip ids must be unique");
                data.bones=names.Select(n=>new Map01PistolAnimationData.Track{name=n,poses=new Map01PistolAnimationData.Pose[receipt.frames]}).ToArray();
                data.props=receipt.props.Select(spec=>new NamMotionData.Prop{key=spec.key,root=new Map01PistolAnimationData.Track{name=spec.root,poses=new Map01PistolAnimationData.Pose[receipt.frames]},parts=spec.parts.Select(n=>new NamMotionData.Part{track=new Map01PistolAnimationData.Track{name=n,poses=new Map01PistolAnimationData.Pose[receipt.frames]},visible=receipt.visibility.Single(v=>v.name==n).visible}).ToArray()}).ToArray();
                var sourceClip=AssetDatabase.LoadAllAssetsAtPath(model).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview"));
                var rootParent=names.ToDictionary(n=>n,n=>tm[n].parent);
                var externalParent=names.ToDictionary(n=>n,n=>tm[n].parent.localToWorldMatrix);
                float maxScaleError=0;
                for(int f=0;f<receipt.frames;f++)
                {
                    sourceClip.SampleAnimation(source,f/(float)receipt.fps);
                    var posed=names.ToDictionary(n=>n,n=>targetHipRest*units*sourceHipRest*sm[n].localToWorldMatrix*units.inverse*sr[n].inverse*tr[n]);
                    for(int i=0;i<names.Length;i++)
                    {
                        string n=names[i];var parent=rootParent[n];var parentWorld=posed.TryGetValue(parent.name,out var parentPose)?parentPose:externalParent[n];
                        var pose=Pose(parentWorld.inverse*posed[n]);data.bones[i].poses[f]=pose;maxScaleError=Mathf.Max(maxScaleError,(pose.scale-tm[n].localScale).magnitude);
                    }
                    foreach(var prop in data.props)
                    {
                        prop.root.poses[f]=Pose(correction*sm["RightHand"].worldToLocalMatrix*sm[prop.root.name].localToWorldMatrix);
                        foreach(var part in prop.parts)part.track.poses[f]=Pose(correction*sm["RightHand"].worldToLocalMatrix*sm[part.track.name].localToWorldMatrix);
                    }
                }
                var materials=AssetDatabase.FindAssets("t:Material",new[]{"Assets/_Project/Art/Environment/Map01Items","Assets/_Project/Art/K54_v12"}).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<Material>).Where(m=>m!=null).GroupBy(m=>m.name).ToDictionary(g=>g.Key,g=>g.First());
                Material Resolve(Material m)
                {
                    string name=m!=null?m.name:"Nam prop";
                    if(materials.TryGetValue(name,out var existing))return existing;
                    string clean=System.Text.RegularExpressions.Regex.Replace(name,@"\.\d{3}$","");
                    if(materials.TryGetValue(clean,out existing))return existing;
                    var path=Root+"/"+System.Text.RegularExpressions.Regex.Replace(name,@"[^a-zA-Z0-9_-]","_")+".mat";
                    var result=AssetDatabase.LoadAssetAtPath<Material>(path);
                    if(result==null){result=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};if(m!=null)result.color=m.color;AssetDatabase.CreateAsset(result,path);}
                    materials[name]=result;return result;
                }
                foreach(var prop in data.props)
                {
                    var go=new GameObject(prop.key+"_v3");
                    foreach(var part in prop.parts)
                    {
                        var from=sm[part.track.name];var child=new GameObject(part.track.name);child.transform.SetParent(go.transform,false);
                        child.AddComponent<MeshFilter>().sharedMesh=from.GetComponent<MeshFilter>().sharedMesh;
                        child.AddComponent<MeshRenderer>().sharedMaterials=from.GetComponent<MeshRenderer>().sharedMaterials.Select(Resolve).ToArray();
                        part.track.At(1).Apply(child.transform);
                    }
                    prop.prefab=PrefabUtility.SaveAsPrefabAsset(go,Root+"/"+prop.key+"_v3.prefab");Object.DestroyImmediate(go);
                }
                var previous=AssetDatabase.LoadAssetAtPath<NamMotionData>(AssetPath);
                if(previous==null)AssetDatabase.CreateAsset(data,AssetPath);else{EditorUtility.CopySerialized(data,previous);Object.DestroyImmediate(data);}
                PatchJump();RefreshPropSurfaces();AssetDatabase.SaveAssets();
                File.WriteAllText(Work+"/bake.txt",$"NAM_V3_BAKE_PASS\nbones={names.Length} binds={sourceBinds}/{targetBinds} frames={receipt.frames} fps={receipt.fps}\nunit={unit} maxScaleError={maxScaleError} clipSeconds={sourceClip.length}\nsource={receipt.sourceHash}");
                if(maxScaleError>.02f)throw new Exception("Nam v3 target scale mismatch "+maxScaleError);
                Debug.Log("NAM_V3_BAKE_PASS");
            }
            finally{Object.DestroyImmediate(source);Object.DestroyImmediate(target);}
        }
        static void PatchJump()
        {
            var path=AssetDatabase.FindAssets("AC_Player t:AnimatorController").Select(AssetDatabase.GUIDToAssetPath).First(p=>Path.GetFileNameWithoutExtension(p)=="AC_Player");
            var c=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);var jump=c.layers[0].stateMachine.states.Select(s=>s.state).First(s=>s.name=="Jump");
            foreach(var t in jump.transitions){t.hasExitTime=false;t.duration=.1f;t.hasFixedDuration=true;t.conditions=new[]{new AnimatorCondition{mode=AnimatorConditionMode.If,parameter="Grounded"}};}
            EditorUtility.SetDirty(c);
        }
        public static void RefreshPropSurfaces()
        {
            // These two source materials use the viewport diffuse colour in the approved reel.
            // FBX exports their untouched default Principled colour as white instead.
            void Surface(string name,Color color,float smoothness)
            {
                var material=AssetDatabase.LoadAssetAtPath<Material>(Root+"/"+name+".mat");if(material==null)return;
                material.color=color;material.SetFloat("_Smoothness",smoothness);EditorUtility.SetDirty(material);
            }
            Surface("Dao_KaBar",new Color(.25f,.25f,.26f),.25f);
            Surface("DEMO_Stone_Mat",new Color(.32f,.30f,.26f),.08f);
            Surface("Hemp_rope",new Color(.45f,.36f,.22f),.05f);
            AssetDatabase.SaveAssets();
        }
        public static void Diagnose()
        {
            var source=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Nam_AnimationRefresh_v3.fbx"));
            var target=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/Player.prefab"));
            try
            {
                var sm=Map(source);var tm=Map(target);var data=Resources.Load<NamMotionData>("Characters/NamMotionV3");
                var clip=AssetDatabase.LoadAllAssetsAtPath(Root+"/Nam_AnimationRefresh_v3.fbx").OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview"));
                var report=new System.Text.StringBuilder();
                foreach(int f in new[]{1,365,400,464,465,500,537,1600,3208})
                {
                    clip.SampleAnimation(source,(f-1)/60f);foreach(var track in data.bones)track.At(f).Apply(tm[track.name]);
                    report.AppendLine($"FRAME {f} source hips={sm["Hips"].position.ToString("F4")} hand={sm["RightHand"].position.ToString("F4")} knife={sm["Nam_Dao_KaBar"].position.ToString("F4")} target hips={tm["Hips"].position.ToString("F4")} hand={tm["RightHand"].position.ToString("F4")} foot={tm["RightFoot"].position.ToString("F4")}");
                    foreach(var p in data.props)
                    foreach(var part in p.parts)
                        report.AppendLine($"  {p.key}/{part.track.name} visible={part.Visible(f)} local={part.track.At(f).position.ToString("F4")} scale={part.track.At(f).scale.ToString("F4")} world={tm["RightHand"].TransformPoint(part.track.At(f).position).ToString("F4")}");
                }
                File.WriteAllText(Work+"/diagnose.txt",report.ToString());
            }
            finally{Object.DestroyImmediate(source);Object.DestroyImmediate(target);}
        }
    }
}
