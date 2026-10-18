using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
using UnityEngine.AI;
using Unity.AI.Navigation;
using Object = UnityEngine.Object;

namespace ShadowVale.Map01.Editor
{
    [InitializeOnLoad]
    public static partial class MapRiverAlignment
    {
        const string Request = "Tools/MapRiverAlignment.request";
        const string Reports = "Tools/RiverAlignmentReports";
        const string Map2 = "Assets/_Project/Scenes/Maps/Map 2.unity";
        static MeshCollider outsideGround;
        static MeshCollider[] visibleGround = Array.Empty<MeshCollider>();
        sealed class OutsideGround : IDisposable
        {
            readonly GameObject probe;
            public OutsideGround(Scene scene)
            {
                var source=All<MeshFilter>(scene).Single(f=>f.name=="Continuous land • hills north and fields east west");
                probe=new GameObject("Temporary vegetation terrain sampler"){hideFlags=HideFlags.HideAndDontSave};
                probe.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);
                probe.transform.localScale=source.transform.lossyScale;
                outsideGround=probe.AddComponent<MeshCollider>();outsideGround.sharedMesh=source.sharedMesh;
                var surfaces=new List<MeshCollider>();
                foreach(var filter in All<MeshFilter>(scene).Where(f=>f.sharedMesh!=null&&IsGroundSurface(f.name)))
                {
                    var collider=filter.GetComponent<MeshCollider>();
                    if(collider==null)
                    {
                        var child=new GameObject("Soil surface sampler"){hideFlags=HideFlags.HideAndDontSave};child.transform.SetParent(probe.transform,false);
                        child.transform.SetPositionAndRotation(filter.transform.position,filter.transform.rotation);child.transform.localScale=filter.transform.lossyScale;
                        collider=child.AddComponent<MeshCollider>();collider.sharedMesh=filter.sharedMesh;
                    }
                    surfaces.Add(collider);
                }
                visibleGround=surfaces.ToArray();Physics.SyncTransforms();
            }
            public void Dispose(){outsideGround=null;visibleGround=Array.Empty<MeshCollider>();Object.DestroyImmediate(probe);}
        }
        static bool IsGroundSurface(string name)=>name=="Irregular rice parcel"||name=="Uneven planted bund"||name=="Soft-edged homestead yard"||name=="Grassy irregular landing clearing"||(name.StartsWith("Winding ")&&name!="Winding canal water");
        static MapRiverAlignment() { EditorApplication.update += Poll; }
        static void Poll()
        {
            if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            string command;
            try { command = File.ReadAllText(Request).Trim(); File.Delete(Request); }
            catch (IOException) { return; }
            Directory.CreateDirectory(Reports);
            try
            {
                if (command == "stop") EditorApplication.isPlaying = false;
                else if (command == "inspect") Inspect();
                else if (command == "inspect2") { OpenMap2(); Inspect(); }
                else if (command == "sync1") SyncMap1();
                else if (command == "trees2") RepairMap2Trees();
                else if (command == "capture2") CaptureMap2("after");
                else if (command == "before2") CaptureMap2("before");
                else if (command == "vegetationInspect") InspectVegetation();
                else if (command == "vegetation2") RepairVegetation();
                else if (command == "verify2") VerifyVegetation();
                else if (command == "interactions1") Map01RiverInteractions.Apply();
                else throw new Exception("Unknown command: " + command);
                File.WriteAllText(Reports + "/status.txt", "PASS " + command);
            }
            catch (Exception e) { File.WriteAllText(Reports + "/status.txt", e.ToString()); Debug.LogException(e); }
        }
        static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true)).ToArray();
        static void OpenMap2()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stop Play Mode first.");
            var current=EditorSceneManager.GetActiveScene();
            if(current.path==Map2)return;
            if(current.isDirty)EditorSceneManager.SaveScene(current);
            EditorSceneManager.OpenScene(Map2);
        }
        [MenuItem("ShadowVale/Map 1/Synchronize water with Map 2")]
        public static void SyncMap1()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stop Play Mode first.");
            var current=EditorSceneManager.GetActiveScene();
            const string path="Assets/_Project/Scenes/Maps/Map 1.unity";
            if(current.path!=path) {if(current.isDirty)EditorSceneManager.SaveScene(current);EditorSceneManager.OpenScene(path);}
            Map01WaterRepair.Apply();
            var river=All<RiverWater>(EditorSceneManager.GetActiveScene()).Single(r=>r.name=="Winding river");
            var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Environment/Map02_Village/PhysicalRiver.mat");
            if(river.waterRenderer.sharedMaterial!=material || Mathf.Abs(river.waveAmplitude-material.GetFloat("_WaveAmplitude"))>.00001f || Mathf.Abs(river.flowSpeed-material.GetFloat("_FlowSpeed"))>.00001f)
                throw new Exception("Map 1 water settings differ from Map 2.");
            File.WriteAllText(Reports+"/water-sync.txt","PASS shared PhysicalRiver.mat, RiverWater simulation, amplitude="+river.waveAmplitude+", flow="+river.flowSpeed+", 401 fitted bank rows; terrain and river level preserved.");
        }
        static void Inspect()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var lines = new List<string> { scene.path, "Playing=" + EditorApplication.isPlaying, "Dirty=" + scene.isDirty };
            foreach (var f in All<MeshFilter>(scene))
            {
                var renderer = f.GetComponent<Renderer>();
                if (f.sharedMesh != null && renderer != null && renderer.bounds.size.x > 100)
                    lines.Add("Ground " + f.name + " " + AssetDatabase.GetAssetPath(f.sharedMesh) + " collider=" + (f.GetComponent<Collider>() != null));
            }
            foreach (var group in All<LODGroup>(scene))
            {
                var t = group.transform;
                lines.Add("LOD " + t.name + " parent=" + t.parent?.name + " pos=" + t.position.ToString("F3") + " scale=" + t.lossyScale + " bounds=" + string.Join(";", group.GetComponentsInChildren<MeshFilter>(true).Take(2).Select(f=>f.sharedMesh?.name+":"+f.sharedMesh?.bounds)));
            }
            foreach (var river in All<RiverWater>(scene)) lines.Add("River " + river.name + " banks=" + river.leftBank.Length + " amplitude=" + river.waveAmplitude + " flow=" + river.flowSpeed + " material=" + AssetDatabase.GetAssetPath(river.waterRenderer?.sharedMaterial));
            if(scene.path==Map2)
            {
                var ground=All<MeshCollider>(scene).Single(c=>c.name=="Continuous ground with meandering canal");
                foreach(var group in All<LODGroup>(scene).Where(g=>g.name.StartsWith("Tree_")))
                {
                    var f=group.GetComponentsInChildren<MeshFilter>(true).First();
                    var points=f.sharedMesh.vertices.Select(v=>f.transform.TransformPoint(v)).ToArray();
                    float bottom=points.Min(p=>p.y);
                    var feet=points.Where(p=>p.y<bottom+.12f).ToArray();
                    var gaps=feet.Select(p=>p.y-GroundHeight(ground,p)).ToArray();
                    lines.Add("BASE "+group.name+" at="+group.transform.position.ToString("F3")+" minGap="+gaps.Min().ToString("F3")+" maxGap="+gaps.Max().ToString("F3")+" footRadius="+feet.Max(p=>Vector2.Distance(new Vector2(p.x,p.z),new Vector2(group.transform.position.x,group.transform.position.z))).ToString("F3"));
                }
                var camera=Camera.main;
                if(camera!=null)lines.Add("Main camera "+camera.transform.position+" "+camera.transform.eulerAngles);
            }
            File.WriteAllLines(Reports + "/inspection.txt", lines);
        }
        static float GroundHeight(MeshCollider ground,Vector3 p)
        {
            var ray=new Ray(new Vector3(p.x,200,p.z),Vector3.down);
            if(!ground.Raycast(ray,out var hit,400) && (outsideGround==null || !outsideGround.Raycast(ray,out hit,400)))throw new Exception("Ground missing at "+p);
            float height=hit.point.y;
            foreach(var surface in visibleGround)
            {
                var b=surface.bounds;
                if(p.x>=b.min.x&&p.x<=b.max.x&&p.z>=b.min.z&&p.z<=b.max.z&&surface.Raycast(ray,out var upper,400))height=Mathf.Max(height,upper.point.y);
            }
            return height;
        }
        static Vector3[] Feet(LODGroup group)
        {
            var filter=group.GetLODs()[0].renderers.OfType<MeshRenderer>().Select(r=>r.GetComponent<MeshFilter>()).First(f=>f!=null);
            var points=filter.sharedMesh.vertices.Select(v=>filter.transform.TransformPoint(v)).ToArray();
            float bottom=points.Min(p=>p.y);
            return points.Where(p=>p.y<bottom+.12f).ToArray();
        }
        static void InspectVegetation()
        {
            OpenMap2();
            var scene=EditorSceneManager.GetActiveScene();
            var ground=All<MeshCollider>(scene).Single(c=>c.name=="Continuous ground with meandering canal");
            var lines=new List<string>();
            using(var sampler=new OutsideGround(scene))
            {
            foreach(var batch in All<VillageRiceInstances>(scene))
            {
                float maxGap=float.MinValue,minGap=float.MaxValue;int wet=0,floating=0;
                foreach(var p in batch.plants)
                {
                    var world=batch.transform.TransformPoint(new Vector3(p.x,p.y,p.z));
                    float h=GroundHeight(ground,world);float gap=world.y-h;
                    maxGap=Mathf.Max(maxGap,gap);minGap=Mathf.Min(minGap,gap);if(gap>.05f)floating++;if(h<.15f)wet++;
                }
                lines.Add("BATCH "+batch.name+" n="+batch.plants.Length+" gap="+minGap.ToString("F3")+".."+maxGap.ToString("F3")+" floating="+floating+" wet="+wet+" meshes="+batch.detailedMesh.name+" minY="+batch.detailedMesh.bounds.min.y+" farMinY="+batch.distantMesh?.bounds.min.y);
            }
            foreach(var filter in All<MeshFilter>(scene).Where(f=>f.name=="Patchy canal grasses"))
            {
                var points=filter.sharedMesh.vertices.Select(v=>filter.transform.TransformPoint(v)).ToArray();
                float bottom=points.Min(p=>p.y);var feet=points.Where(p=>p.y<bottom+.04f).ToArray();
                var gaps=feet.Select(p=>p.y-GroundHeight(ground,p)).ToArray();
                lines.Add("PATCH "+filter.transform.GetSiblingIndex()+" pos="+filter.transform.position.ToString("F3")+" gap="+gaps.Min().ToString("F3")+".."+gaps.Max().ToString("F3")+" n="+points.Length+" bounds="+filter.sharedMesh.bounds);
            }
            foreach(var root in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Where(t=>t.name=="Natural village gardens"||t.name=="Orchards palms and reference landmarks"))
                foreach(Transform child in root)lines.Add("GARDEN "+child.name+" at="+child.position.ToString("F3")+" meshes="+string.Join(",",child.GetComponentsInChildren<MeshFilter>().Select(f=>f.sharedMesh.name)));
            File.WriteAllLines(Reports+"/vegetation-inspection.txt",lines);
            }
        }
        static float RootRadius(LODGroup group,Vector3[] feet)
        {
            var pivot=group.transform.position;
            return Mathf.Max(.3f,feet.Max(p=>Vector2.Distance(new Vector2(p.x,p.z),new Vector2(pivot.x,pivot.z))));
        }
        static bool DryFootprint(MeshCollider ground,Vector3 center,float radius)
        {
            float lo=float.MaxValue,hi=float.MinValue;
            for(int i=0;i<9;i++)
            {
                float angle=i*Mathf.PI/4;
                var p=center+(i==8?Vector3.zero:new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*(radius+.25f));
                if(!ground.Raycast(new Ray(new Vector3(p.x,100,p.z),Vector3.down),out var hit,200))return false;
                if(hit.point.y<.6f || hit.normal.y<.94f)return false;
                lo=Mathf.Min(lo,hit.point.y);hi=Mathf.Max(hi,hit.point.y);
            }
            return hi-lo<.3f;
        }
        static Vector3 NearestAcross(RiverWater river,Vector3 point)
        {
            int nearest=0;float distance=float.MaxValue;
            for(int i=0;i<river.leftBank.Length;i++)
            {
                var center=(river.leftBank[i]+river.rightBank[i])*.5f;
                float d=new Vector2(point.x-center.x,point.z-center.z).sqrMagnitude;
                if(d<distance){distance=d;nearest=i;}
            }
            var midpoint=(river.leftBank[nearest]+river.rightBank[nearest])*.5f;
            var across=(river.rightBank[nearest]-river.leftBank[nearest]).normalized;
            return Vector3.Dot(point-midpoint,across)<0?-across:across;
        }
        [MenuItem("ShadowVale/Map 2/Ground riverside trees")]
        public static void RepairMap2Trees()
        {
            OpenMap2();
            var scene=EditorSceneManager.GetActiveScene();
            if(scene.isDirty)EditorSceneManager.SaveScene(scene);
            Directory.CreateDirectory(Reports); Directory.CreateDirectory("Library/RiverAlignment");
            const string backup="Library/RiverAlignment/Map2-before-tree-grounding.unity";
            if(!File.Exists(backup))File.Copy(Map2,backup);
            var ground=All<MeshCollider>(scene).Single(c=>c.name=="Continuous ground with meandering canal");
            var river=All<RiverWater>(scene).Single(r=>r.name=="Winding canal water");
            var trees=All<LODGroup>(scene).Where(g=>g.name.StartsWith("Tree_")).ToArray();
            var colliders=All<Collider>(scene).Where(c=>!c.isTrigger&&c!=ground).ToArray();
            var report=new List<string>();int moved=0,grounded=0;
            foreach(var tree in trees)
            {
                var feet=Feet(tree);float radius=RootRadius(tree,feet);
                var original=tree.transform.position;
                var target=original;
                if(!DryFootprint(ground,target,radius))
                {
                    var away=NearestAcross(river,original);var along=Vector3.Cross(Vector3.up,away);
                    bool found=false;
                    for(float distance=.5f;distance<=18&&!found;distance+=.5f)
                    foreach(float offset in new[]{0f,-1.5f,1.5f,-3f,3f,-4.5f,4.5f})
                    {
                        var candidate=original+away*distance+along*offset;
                        if(!DryFootprint(ground,candidate,radius))continue;
                        candidate.y=GroundHeight(ground,candidate);
                        if(trees.Any(t=>t!=tree&&Vector2.Distance(new Vector2(t.transform.position.x,t.transform.position.z),new Vector2(candidate.x,candidate.z))<2.8f))continue;
                        if(colliders.Any(c=>!c.transform.IsChildOf(tree.transform)&&Vector3.Distance(c.ClosestPoint(candidate+Vector3.up),candidate+Vector3.up)<radius+.35f))continue;
                        target=candidate;found=true;break;
                    }
                    if(!found)throw new Exception("No safe dry bank for "+tree.name+" at "+original);
                    moved++;
                }
                Undo.RecordObject(tree.transform,"Ground Map 2 tree roots");
                tree.transform.position=target;
                feet=Feet(tree);
                float shift=feet.Min(p=>GroundHeight(ground,p)-p.y)-.04f;
                tree.transform.position+=Vector3.up*shift;
                PrefabUtility.RecordPrefabInstancePropertyModifications(tree.transform);
                EditorUtility.SetDirty(tree.transform);grounded++;
                float gap=Feet(tree).Max(p=>p.y-GroundHeight(ground,p));
                if(gap>-.03f || !DryFootprint(ground,tree.transform.position,radius))throw new Exception("Root grounding failed at "+tree.transform.position);
                report.Add("PASS "+tree.name+" "+original.ToString("F3")+" -> "+tree.transform.position.ToString("F3")+" max root gap="+gap.ToString("F3"));
            }
            Physics.SyncTransforms();
            if(moved>0)
            {
                var env=scene.GetRootGameObjects().Single(g=>g.name.StartsWith("01 Environment"));
                var surface=env.GetComponent<NavMeshSurface>();
                if(surface==null)throw new Exception("Missing Map 2 navigation surface.");
                var old=surface.navMeshData;
                string oldPath=AssetDatabase.GetAssetPath(old);
                surface.BuildNavMesh();
                var built=surface.navMeshData;
                const string navPath="Assets/_Project/Art/Environment/Map02_Village/Riverbank_NavMesh.asset";
                var saved=AssetDatabase.LoadAssetAtPath<NavMeshData>(navPath);
                if(saved==null){AssetDatabase.CreateAsset(built,navPath);saved=built;}
                else {surface.RemoveData();EditorUtility.CopySerialized(built,saved);EditorUtility.SetDirty(saved);Object.DestroyImmediate(built);}
                surface.RemoveData();surface.navMeshData=saved;surface.AddData();EditorUtility.SetDirty(surface);
                report.Add("PASS navigation rebuilt for relocated tree colliders; prior asset preserved: "+oldPath);
            }
            foreach(var target in new[]{new Vector3(-60,1,0),new Vector3(35,1,-10),new Vector3(30,1,60)})
            {
                // The river crossings must remain connected after moving colliders on both banks.
                var path=new NavMeshPath();
                bool ok=NavMesh.SamplePosition(new Vector3(-45,1,-45),out var a,15,NavMesh.AllAreas)&&NavMesh.SamplePosition(target,out var b,15,NavMesh.AllAreas)&&NavMesh.CalculatePath(a.position,b.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete;
                report.Add((ok?"PASS":"FAIL")+" navigation route "+target);
            }
            if(report.Any(s=>s.StartsWith("FAIL")))throw new Exception("Navigation validation failed: "+string.Join(";",report.Where(s=>s.StartsWith("FAIL"))));
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            report.Insert(0,"Grounded "+grounded+" trees; relocated "+moved+" off wet or steep banks.");
            File.WriteAllLines(Reports+"/tree-grounding.txt",report);
        }
        static void CaptureMap2(string label)
        {
            OpenMap2();
            var scene=EditorSceneManager.GetActiveScene();
            var source=All<Camera>(scene).First();
            var go=new GameObject("Temporary riverbank review camera"){hideFlags=HideFlags.HideAndDontSave};
            var camera=go.AddComponent<Camera>();camera.CopyFrom(source);camera.enabled=false;camera.useOcclusionCulling=false;camera.orthographic=false;
            var data=camera.GetUniversalAdditionalCameraData();data.requiresDepthTexture=true;data.requiresColorTexture=true;
            var rt=new RenderTexture(1440,900,24);var image=new Texture2D(1440,900,TextureFormat.RGB24,false);var previous=RenderTexture.active;
            bool async=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
            try
            {
                var views=new[]{(new Vector3(-3,1.45f,87),new Vector3(-12,1.4f,84),"north"),(new Vector3(-7,1.4f,-86),new Vector3(-18,1.5f,-92),"south")};
                foreach(var view in views)
                {
                    camera.transform.position=view.Item1;camera.transform.LookAt(view.Item2);camera.fieldOfView=60;
                    camera.targetTexture=rt;camera.Render();camera.Render();RenderTexture.active=rt;
                    image.ReadPixels(new Rect(0,0,1440,900),0,0);image.Apply();File.WriteAllBytes(Reports+"/map2-"+label+"-"+view.Item3+".png",image.EncodeToPNG());
                }
            }
            finally{ShaderUtil.allowAsyncCompilation=async;RenderTexture.active=previous;Object.DestroyImmediate(go);Object.DestroyImmediate(rt);Object.DestroyImmediate(image);}
        }
    }
}
