using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;
using Object = UnityEngine.Object;

namespace ShadowVale.Map01.Editor
{
    public static partial class MapRiverAlignment
    {
        const string GrassFolder="Assets/_Project/Art/Environment/Map02_Village/GroundedVegetation";
        const float DryGrassHeight=.16f; // Above the 0.045 m surface plus its 0.080 m wave crest.

        static Vector3[] RootPoints(Mesh mesh)
        {
            var vertices=mesh.vertices;
            float bottom=vertices.Min(v=>v.y);
            var roots=vertices.Where(v=>v.y<=bottom+.005f).ToArray();
            var samples=new List<Vector3>{roots.OrderBy(v=>v.y).First()};
            for(int i=0;i<8;i++)
            {
                float angle=i*Mathf.PI/4;
                samples.Add(roots.OrderByDescending(p=>p.x*Mathf.Cos(angle)+p.z*Mathf.Sin(angle)).First());
            }
            return samples.Distinct().ToArray();
        }

        static bool VegetationGroup(LODGroup group)
        {
            return group.GetComponentsInChildren<MeshFilter>(true).Any(f=>f.sharedMesh!=null&&
                new[]{"Forest_","Banyan","Flamboyant","RoadTree","Roadside_Bamboo","Roadside_Hedge"}.Any(s=>f.sharedMesh.name.Contains(s)));
        }

        [MenuItem("ShadowVale/Map 2/Ground all vegetation and canal grass")]
        public static void RepairVegetation()
        {
            OpenMap2();
            var scene=EditorSceneManager.GetActiveScene();
            if(scene.isDirty)EditorSceneManager.SaveScene(scene);
            Directory.CreateDirectory(Reports);Directory.CreateDirectory("Library/RiverAlignment");
            const string backup="Library/RiverAlignment/Map2-before-grass-grounding.unity";
            if(!File.Exists(backup))File.Copy(Map2,backup);
            Directory.CreateDirectory(GrassFolder);AssetDatabase.Refresh();
            var ground=All<MeshCollider>(scene).Single(c=>c.name=="Continuous ground with meandering canal");
            var report=new List<string>();
            using(var sampler=new OutsideGround(scene))
            {
                GroundGrassInstances(scene,ground,report);
                GroundCanalPatches(scene,ground,report);
                int woody=0,changed=0;
                foreach(var group in All<LODGroup>(scene).Where(VegetationGroup))
                {
                    var filters=group.GetLODs().SelectMany(l=>l.renderers).OfType<MeshRenderer>().Select(r=>r.GetComponent<MeshFilter>()).Where(f=>f!=null&&f.sharedMesh!=null).Distinct().ToArray();
                    changed+=GroundPlantRoot(group.transform,filters,ground)?1:0;woody++;
                }
                foreach(var filter in All<MeshFilter>(scene).Where(f=>f.name=="Banana garden clump"||f.name=="Coconut at field edge"))
                {changed+=GroundPlantRoot(filter.transform,new[]{filter},ground)?1:0;woody++;}
                report.Add("Woody trees / bamboo / shrubs / coconut / banana audited: "+woody+"; vertical corrections: "+changed);
            }
            Physics.SyncTransforms();
            RebuildVegetationNavigation(scene,report);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            File.WriteAllLines(Reports+"/vegetation-repair.txt",report);
            VerifyVegetation();
        }

        static bool GroundPlantRoot(Transform owner,MeshFilter[] filters,MeshCollider ground)
        {
            var feet=filters.SelectMany(f=>RootPoints(f.sharedMesh).Select(p=>f.transform.TransformPoint(p))).ToArray();
            float gap=feet.Max(p=>p.y-GroundHeight(ground,p));
            if(gap<=.015f&&gap>=-.12f)return false;
            Undo.RecordObject(owner,"Ground vegetation roots");
            owner.position+=Vector3.up*(-gap-.025f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(owner);EditorUtility.SetDirty(owner);
            return true;
        }

        static void GroundGrassInstances(Scene scene,MeshCollider ground,List<string> report)
        {
            int scanned=0,changed=0,removed=0,batches=0,rice=0;
            var roots=new Dictionary<Mesh,Vector3[]>();
            foreach(var batch in All<VillageRiceInstances>(scene))
            {
                if(batch.name.StartsWith("Connected rice parcel")){rice+=batch.plants.Length;continue;}
                var meshes=new[]{batch.detailedMesh,batch.distantMesh}.Where(m=>m!=null).Distinct().ToArray();
                foreach(var mesh in meshes)if(!roots.ContainsKey(mesh))roots[mesh]=RootPoints(mesh);
                var kept=new List<Vector4>();var sizes=new List<float>();
                Undo.RecordObject(batch,"Ground grass instances");
                for(int i=0;i<batch.plants.Length;i++)
                {
                    scanned++;var plant=batch.plants[i];float size=i<batch.sizes.Length?batch.sizes[i]:1;
                    var matrix=batch.transform.localToWorldMatrix*Matrix4x4.TRS(new Vector3(plant.x,plant.y,plant.z),Quaternion.Euler(0,plant.w,0),Vector3.one*size);
                    float gap=float.MinValue;bool dry=true;
                    foreach(var mesh in meshes)foreach(var root in roots[mesh])
                    {
                        var world=matrix.MultiplyPoint3x4(root);float h=GroundHeight(ground,world);
                        gap=Mathf.Max(gap,world.y-h);if(h<DryGrassHeight)dry=false;
                    }
                    if(!dry){removed++;continue;}
                    var center=batch.transform.TransformPoint(new Vector3(plant.x,plant.y,plant.z));
                    float shift=-gap-.008f;if(Mathf.Abs(shift)>.002f)changed++;
                    center.y+=shift;var local=batch.transform.InverseTransformPoint(center);
                    kept.Add(new Vector4(local.x,local.y,local.z,plant.w));sizes.Add(size);
                }
                batch.plants=kept.ToArray();batch.sizes=sizes.ToArray();batch.Rebuild();EditorUtility.SetDirty(batch);batches++;
            }
            report.Add("Grass instances audited (both LODs): "+scanned+" in "+batches+" batches; grounded="+changed+"; removed wet roots="+removed);
            report.Add("Rice instances audited separately, preserved: "+rice);
        }

        static List<int[]> ConnectedTufts(Mesh mesh)
        {
            var vertices=mesh.vertices;var parent=Enumerable.Range(0,vertices.Length).ToArray();
            int Find(int a){while(parent[a]!=a){parent[a]=parent[parent[a]];a=parent[a];}return a;}
            void Join(int a,int b){parent[Find(a)]=Find(b);}
            var weld=new Dictionary<Vector3Int,int>();
            for(int i=0;i<vertices.Length;i++)
            {
                var p=vertices[i];var key=new Vector3Int(Mathf.RoundToInt(p.x*10000),Mathf.RoundToInt(p.y*10000),Mathf.RoundToInt(p.z*10000));
                if(weld.TryGetValue(key,out var previous))Join(i,previous);else weld[key]=i;
            }
            var triangles=mesh.triangles;
            for(int i=0;i<triangles.Length;i+=3){Join(triangles[i],triangles[i+1]);Join(triangles[i],triangles[i+2]);}
            return Enumerable.Range(0,vertices.Length).GroupBy(Find).Select(g=>g.ToArray()).ToList();
        }

        static void GroundCanalPatches(Scene scene,MeshCollider ground,List<string> report)
        {
            var source=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Project/Art/Environment/Map01_Optimized/Meshes/Prop_Grass2.asset");
            if(source==null)throw new Exception("Missing original canal grass mesh.");
            var tufts=ConnectedTufts(source);var sourceVertices=source.vertices;var triangles=source.triangles;
            int count=0,removed=0,retained=0,rootChecks=0;
            var patches=All<MeshFilter>(scene).Where(f=>f.name=="Patchy canal grasses").OrderBy(f=>GlobalObjectId.GetGlobalObjectIdSlow(f).targetObjectId).ToArray();
            foreach(var filter in patches)
            {
                var vertices=(Vector3[])sourceVertices.Clone();var live=new bool[vertices.Length];
                foreach(var tuft in tufts)
                {
                    var world=tuft.Select(i=>filter.transform.TransformPoint(sourceVertices[i])).ToArray();
                    float bottom=world.Min(p=>p.y);
                    var feet=world.Where(p=>p.y<=bottom+.0001f).ToArray();
                    var center=feet.Aggregate(Vector3.zero,(sum,p)=>sum+p)/feet.Length;
                    if(feet.Any(p=>GroundHeight(ground,p)<DryGrassHeight)){removed++;continue;}
                    retained++;float baseHeight=GroundHeight(ground,center);
                    for(int j=0;j<tuft.Length;j++)
                    {
                        int index=tuft[j];var p=world[j];float height=p.y-bottom;
                        float bend=1-Mathf.SmoothStep(0,1,height/Mathf.Max(.05f,.18f*filter.transform.lossyScale.y));
                        p.y=baseHeight+height+(GroundHeight(ground,p)-baseHeight)*bend-.012f;
                        vertices[index]=filter.transform.InverseTransformPoint(p);live[index]=true;
                        if(height<=.0001f){rootChecks++;if(Mathf.Abs(p.y-GroundHeight(ground,p)+.012f)>.002f)throw new Exception("Canal grass root is not on terrain.");}
                    }
                }
                var kept=new List<int>();
                for(int i=0;i<triangles.Length;i+=3)if(live[triangles[i]])kept.AddRange(new[]{triangles[i],triangles[i+1],triangles[i+2]});
                var mesh=Object.Instantiate(source);mesh.name="Terrain fitted canal grass "+count;
                mesh.vertices=vertices;mesh.triangles=kept.ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();
                string path=GrassFolder+"/CanalGrass_"+count.ToString("D3")+".asset";
                var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(saved==null){AssetDatabase.CreateAsset(mesh,path);saved=mesh;}else{EditorUtility.CopySerialized(mesh,saved);EditorUtility.SetDirty(saved);Object.DestroyImmediate(mesh);}
                Undo.RecordObject(filter,"Fit canal grass to the bank");filter.sharedMesh=saved;EditorUtility.SetDirty(filter);count++;
            }
            report.Add("Canal grass patches conformed: "+count+"; connected tufts per original mesh: "+tufts.Count+"; retained="+retained+"; removed underwater tufts="+removed+"; verified individual blade roots="+rootChecks);
        }

        static void RebuildVegetationNavigation(Scene scene,List<string> report)
        {
            var env=scene.GetRootGameObjects().Single(g=>g.name.StartsWith("01 Environment"));
            var surface=env.GetComponent<NavMeshSurface>();surface.BuildNavMesh();var built=surface.navMeshData;
            const string path="Assets/_Project/Art/Environment/Map02_Village/Riverbank_NavMesh.asset";
            var saved=AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
            surface.RemoveData();
            if(saved==null){AssetDatabase.CreateAsset(built,path);saved=built;}else{EditorUtility.CopySerialized(built,saved);EditorUtility.SetDirty(saved);Object.DestroyImmediate(built);}
            surface.navMeshData=saved;surface.AddData();EditorUtility.SetDirty(surface);
            foreach(var target in new[]{new Vector3(-60,1,0),new Vector3(35,1,-10),new Vector3(30,1,60)})
            {
                var route=new NavMeshPath();
                bool ok=NavMesh.SamplePosition(new Vector3(-45,1,-45),out var a,15,NavMesh.AllAreas)&&NavMesh.SamplePosition(target,out var b,15,NavMesh.AllAreas)&&NavMesh.CalculatePath(a.position,b.position,NavMesh.AllAreas,route)&&route.status==NavMeshPathStatus.PathComplete;
                if(!ok)throw new Exception("Vegetation blocks route to "+target);report.Add("PASS navigation route "+target);
            }
        }

        static void VerifyVegetation()
        {
            OpenMap2();var scene=EditorSceneManager.GetActiveScene();
            if(scene.isDirty)EditorSceneManager.SaveScene(scene);
            scene=EditorSceneManager.OpenScene(Map2);
            var ground=All<MeshCollider>(scene).Single(c=>c.name=="Continuous ground with meandering canal");
            var report=new List<string>();int plants=0,patches=0,roots=0,woody=0;float worst=float.MinValue;
            using(var sampler=new OutsideGround(scene))
            {
                var cache=new Dictionary<Mesh,Vector3[]>();
                foreach(var batch in All<VillageRiceInstances>(scene).Where(b=>!b.name.StartsWith("Connected rice parcel")))
                {
                    var meshes=new[]{batch.detailedMesh,batch.distantMesh}.Where(m=>m!=null).Distinct().ToArray();
                    foreach(var mesh in meshes)if(!cache.ContainsKey(mesh))cache[mesh]=RootPoints(mesh);
                    for(int i=0;i<batch.plants.Length;i++)
                    {
                        var p=batch.plants[i];float size=i<batch.sizes.Length?batch.sizes[i]:1;
                        var matrix=batch.transform.localToWorldMatrix*Matrix4x4.TRS(new Vector3(p.x,p.y,p.z),Quaternion.Euler(0,p.w,0),Vector3.one*size);
                        foreach(var mesh in meshes)foreach(var root in cache[mesh])
                        {
                            var world=matrix.MultiplyPoint3x4(root);float h=GroundHeight(ground,world);float gap=world.y-h;
                            if(gap>.012f||h<DryGrassHeight)throw new Exception("Grass root validation failed in "+batch.name+" at "+world);
                            worst=Mathf.Max(worst,gap);roots++;
                        }
                        plants++;
                    }
                }
                foreach(var filter in All<MeshFilter>(scene).Where(f=>f.name=="Patchy canal grasses"))
                {
                    var mesh=filter.sharedMesh;var vertices=mesh.vertices;var used=mesh.triangles.Distinct().ToArray();
                    var original=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Project/Art/Environment/Map01_Optimized/Meshes/Prop_Grass2.asset");
                    var originalVertices=original.vertices;
                    foreach(int i in used.Where(i=>originalVertices[i].y<=original.bounds.min.y+.0001f))
                    {
                        var p=filter.transform.TransformPoint(vertices[i]);float h=GroundHeight(ground,p);float gap=p.y-h;
                        if(gap>.005f||h<DryGrassHeight)throw new Exception("Canal grass still floating at "+p);
                        worst=Mathf.Max(worst,gap);roots++;
                    }
                    patches++;
                }
                foreach(var group in All<LODGroup>(scene).Where(VegetationGroup))
                {
                    foreach(var filter in group.GetLODs().SelectMany(l=>l.renderers).OfType<MeshRenderer>().Select(r=>r.GetComponent<MeshFilter>()).Where(f=>f!=null&&f.sharedMesh!=null).Distinct())
                    foreach(var root in RootPoints(filter.sharedMesh))
                    {
                        var p=filter.transform.TransformPoint(root);
                        if(p.y-GroundHeight(ground,p)>.016f)throw new Exception("Woody vegetation root is floating: "+group.name+" at "+p);
                    }
                    woody++;
                }
                foreach(var filter in All<MeshFilter>(scene).Where(f=>f.name=="Banana garden clump"||f.name=="Coconut at field edge"))
                {
                    foreach(var root in RootPoints(filter.sharedMesh))
                    {
                        var p=filter.transform.TransformPoint(root);
                        if(p.y-GroundHeight(ground,p)>.016f)throw new Exception("Garden vegetation root is floating: "+filter.name+" at "+p);
                    }
                    woody++;
                }
            }
            report.Add("PASS saved-scene reload, grass instances="+plants+", canal patches="+patches+", actual root samples="+roots);
            report.Add("PASS both near and far grass LODs; worst root gap="+worst.ToString("F4")+" m; no retained grass roots below wave clearance");
            report.Add("PASS saved woody vegetation roots in all LODs: "+woody+" trees / bamboo / shrubs / coconut / banana");
            File.WriteAllLines(Reports+"/vegetation-validation.txt",report);
        }
    }
}
