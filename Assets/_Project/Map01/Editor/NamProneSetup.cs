using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ShadowVale.Map01.Editor
{
    [InitializeOnLoad] public static class NamProneSetup
    {
        public const string DataPath = "Assets/_Project/Map01/Resources/Animations/NamProne.asset";
        const string Source = "SourceArt/Animation/NamCrawl/game-motion.json";
        const string Work = "Logs/AnimationPreview/NamCrawl/Unity";
        [Serializable] sealed class Mat { public float[] m; public Matrix4x4 Value { get { var a = new Matrix4x4(); for(int r=0;r<4;r++) for(int c=0;c<4;c++) a[r,c]=m[r*4+c]; return a; } } }
        [Serializable] sealed class Frame { public Mat[] bones; public float[] grip, forward, up; public Mat oldMagazine,newMagazine; public bool oldVisible,newVisible; }
        [Serializable] sealed class Clip { public string name; public Frame[] frames; }
        [Serializable] sealed class SourceData { public int fps; public string[] names; public Mat[] rest; public Clip[] clips; }
        static NamProneSetup() => EditorApplication.update += Poll;
        static void Poll()
        {
            const string request = "Tools/NamProne.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            var command=File.ReadAllText(request).Trim();File.Delete(request); Directory.CreateDirectory(Work);
            try { if(command=="TEST"){TutorialValidation.RunNamProne();return;} Build(); File.WriteAllText(Work+"/status.txt", "PASS shared prone assets baked and audited"); }
            catch(Exception e) { File.WriteAllText(Work+"/status.txt", "FAIL "+e); Debug.LogException(e); }
        }
        static int Depth(Transform t) { int n=0; while(t.parent!=null){n++;t=t.parent;} return n; }
        static Matrix4x4 Basis(Vector3 right, Vector3 up, Vector3 toes)
        {
            up.Normalize();right=Vector3.ProjectOnPlane(right,up).normalized;
            var forward=toes-right*Vector3.Dot(toes,right)-up*Vector3.Dot(toes,up);
            var m=Matrix4x4.identity;m.SetColumn(0,new Vector4(right.x,right.y,right.z,0));m.SetColumn(1,new Vector4(up.x,up.y,up.z,0));
            forward.Normalize();m.SetColumn(2,new Vector4(forward.x,forward.y,forward.z,0));return m;
        }
        [MenuItem("ShadowVale/Animation/Nằm trườn/Cập nhật animation cho cả hai map")]
        public static void Build()
        {
            var source=JsonUtility.FromJson<SourceData>(File.ReadAllText(Source));
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/Player.prefab");
            var target=Object.Instantiate(prefab);target.hideFlags=HideFlags.HideAndDontSave;
            try {
                foreach(var b in target.GetComponentsInChildren<MonoBehaviour>(true))b.enabled=false;
                var actor=target.GetComponentInChildren<Animator>();actor.enabled=false;
                var nodes=actor.GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
                foreach(var b in actor.avatar.humanDescription.skeleton) if(nodes.TryGetValue(b.name,out var t)&&t!=actor.transform){t.SetLocalPositionAndRotation(b.position,b.rotation);t.localScale=b.scale;}
                var names=source.names.Where(nodes.ContainsKey).OrderBy(n=>Depth(nodes[n])).ToArray();
                if(names.Length!=source.names.Length)throw new Exception("Missing Nam bones: "+string.Join(",",source.names.Except(names)));
                var rest=source.names.Select((n,i)=>(n,i)).ToDictionary(p=>p.n,p=>source.rest[p.i].Value);
                var tr=names.ToDictionary(n=>n,n=>actor.transform.worldToLocalMatrix*nodes[n].localToWorldMatrix);
                Vector3 At(string n)=>rest[n].GetColumn(3);
                var srcBasis=Basis(At("RightArm")-At("LeftArm"),At("Neck")-At("Hips"),At("LeftToeBase")-At("LeftFoot"));
                var dstBasis=Basis((Vector3)tr["RightArm"].GetColumn(3)-(Vector3)tr["LeftArm"].GetColumn(3),(Vector3)tr["Neck"].GetColumn(3)-(Vector3)tr["Hips"].GetColumn(3),(Vector3)tr["LeftToeBase"].GetColumn(3)-(Vector3)tr["LeftFoot"].GetColumn(3));
                float ratio=Vector3.Distance(tr["LeftForeArm"].GetColumn(3),tr["LeftHand"].GetColumn(3))/Vector3.Distance(At("LeftForeArm"),At("LeftHand"));
                var units=dstBasis*srcBasis.inverse*Matrix4x4.Scale(Vector3.one*ratio);
                Directory.CreateDirectory(Work);
                File.WriteAllText(Work+"/alignment.txt", "actor="+actor.transform.localToWorldMatrix+"\nunits="+units+"\n"+string.Join("\n",names.Select(n=>n+" source="+rest[n].GetColumn(3)+" target="+tr[n].GetColumn(3))));
                var data=ScriptableObject.CreateInstance<NamProneAnimationData>();data.name="NamProne";data.fps=source.fps;data.frames=source.clips[0].frames.Length;
                using(var hash=SHA256.Create())data.sourceHash=BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(Source))).Replace("-","");
                var lookup=source.names.Select((n,i)=>(n,i)).ToDictionary(p=>p.n,p=>p.i);
                data.takes=source.clips.Select(clip=>{
                    var take=new NamProneAnimationData.Take{name=clip.name,bones=names.Select(n=>new Map01PistolAnimationData.Track{name=n,poses=new Map01PistolAnimationData.Pose[clip.frames.Length]}).ToArray()};
                    if(clip.frames[0].grip!=null&&clip.frames[0].grip.Length==3)take.grip=new Map01PistolAnimationData.Track{name="RightHand grip",poses=new Map01PistolAnimationData.Pose[clip.frames.Length]};
                    if(clip.name=="reload") {
                        take.oldMagazine=new Map01PistolAnimationData.Track{name="Old magazine",poses=new Map01PistolAnimationData.Pose[clip.frames.Length]};
                        take.newMagazine=new Map01PistolAnimationData.Track{name="New magazine",poses=new Map01PistolAnimationData.Pose[clip.frames.Length]};
                        take.oldVisible=clip.frames.Select(f=>f.oldVisible).ToArray();take.newVisible=clip.frames.Select(f=>f.newVisible).ToArray();
                    }
                    for(int f=0;f<clip.frames.Length;f++) {
                        var posed=names.ToDictionary(n=>n,n=>units*clip.frames[f].bones[lookup[n]].Value*rest[n].inverse*units.inverse*tr[n]);
                        for(int i=0;i<names.Length;i++) {
                            var bone=nodes[names[i]];
                            var parent=posed.TryGetValue(bone.parent.name,out var p)?p:actor.transform.worldToLocalMatrix*bone.parent.localToWorldMatrix;
                            var local=parent.inverse*posed[names[i]];
                            take.bones[i].poses[f]=new Map01PistolAnimationData.Pose{position=local.GetColumn(3),rotation=local.rotation,scale=local.lossyScale};
                        }
                        if(take.grip!=null){
                            Vector3 V(float[] a)=>new Vector3(a[0],a[1],a[2]);
                            var frame=clip.frames[f];var hand=posed["RightHand"];
                            var rotation=Quaternion.LookRotation(units.MultiplyVector(V(frame.forward)),units.MultiplyVector(V(frame.up)));
                            var world=Matrix4x4.TRS(units.MultiplyPoint3x4(V(frame.grip)),rotation,Vector3.one);
                            var local=hand.inverse*world;
                            take.grip.poses[f]=new Map01PistolAnimationData.Pose{position=local.GetColumn(3),rotation=local.rotation,scale=Vector3.one};
                        }
                        if(take.oldMagazine!=null) {
                            Map01PistolAnimationData.Pose Magazine(Mat m) {
                                var matrix=units*m.Value;
                                // Conjugation preserves handedness for the rotation delta.
                                var rotation=(units*m.Value*clip.frames[0].oldMagazine.Value.inverse*units.inverse).rotation;
                                return new Map01PistolAnimationData.Pose{position=matrix.GetColumn(3),rotation=rotation,scale=Vector3.one};
                            }
                            take.oldMagazine.poses[f]=Magazine(clip.frames[f].oldMagazine);
                            take.newMagazine.poses[f]=Magazine(clip.frames[f].newMagazine);
                        }
                    }
                    return take;
                }).ToArray();
                Directory.CreateDirectory(Path.GetDirectoryName(DataPath));AssetDatabase.Refresh();
                var old=AssetDatabase.LoadAssetAtPath<NamProneAnimationData>(DataPath);
                if(old==null)AssetDatabase.CreateAsset(data,DataPath);else{EditorUtility.CopySerialized(data,old);Object.DestroyImmediate(data);data=old;EditorUtility.SetDirty(data);}
                AssetDatabase.SaveAssets();
                Audit(data,target,actor,nodes);
            } finally { Object.DestroyImmediate(target); }
        }
        static void Audit(NamProneAnimationData data,GameObject target,Animator actor,Dictionary<string,Transform> nodes)
        {
            Directory.CreateDirectory(Work);var rows=new List<string>();
            foreach(var take in data.takes) {
                float low=float.PositiveInfinity,high=float.NegativeInfinity;
                foreach(int f in new[]{0,18,36,54,72}) {
                    foreach(var track in take.bones)track.poses[f].Apply(nodes[track.name]);
                    if(f==0)File.WriteAllText(Work+"/pose-"+take.name+".txt",string.Join("\n",take.bones.Select(t=>t.name+" world="+nodes[t.name].position+" scale="+nodes[t.name].lossyScale+" local="+nodes[t.name].localPosition)));
                    float head=actor.GetBoneTransform(HumanBodyBones.Head).position.y-target.transform.position.y;
                    if(head<.1f||head>1.0f)throw new Exception(take.name+" invalid prone head height "+head+" actor="+actor.transform.position+" head="+nodes["Head"].position+" hip="+nodes["Hips"].position);
                    foreach(var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>()){
                        var mesh=skin.sharedMesh;var vertices=mesh.vertices;var weights=mesh.boneWeights;
                        var matrices=skin.bones.Select((b,i)=>b.localToWorldMatrix*mesh.bindposes[i]).ToArray();
                        float skinLow=float.PositiveInfinity;
                        for(int v=0;v<vertices.Length;v++){
                            var w=weights[v];var p=vertices[v];
                            float y=(matrices[w.boneIndex0].MultiplyPoint3x4(p)*w.weight0+matrices[w.boneIndex1].MultiplyPoint3x4(p)*w.weight1+matrices[w.boneIndex2].MultiplyPoint3x4(p)*w.weight2+matrices[w.boneIndex3].MultiplyPoint3x4(p)*w.weight3).y-target.transform.position.y;
                            low=Mathf.Min(low,y);skinLow=Mathf.Min(skinLow,y);high=Mathf.Max(high,y);
                        }
                        if(f==0)File.AppendAllText(Work+"/pose-"+take.name+".txt","\nMESH "+skin.name+" scale="+skin.transform.lossyScale+" low="+skinLow);
                    }
                }
                if(low<-.07f||low>.08f)throw new Exception(take.name+" floor contact invalid "+low);
                rows.Add(take.name+" frames="+data.frames+" skinY="+low+".."+high);
            }
            File.WriteAllLines(Work+"/bake-audit.txt",rows);Debug.Log("NAM_PRONE_BAKE_PASS "+string.Join("; ",rows));
        }
    }
}
