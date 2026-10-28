using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ShadowVale.Map01;
using Object=UnityEngine.Object;

namespace ShadowVale.EnvironmentReview
{
    public static class Map01LandscapeStage
    {
        const string Root=Map01ArtStage.Root,Work=Map01ArtStage.Work;
        [Serializable] class Report {public int recoloredPlants,movedDecoration,thinnedDecoration,jettyBoards,disabledHidingPoints;public string status;}
        [MenuItem("ShadowVale/Art Review/Refine forest and jetty in art stage")]
        public static void Apply()
        {
            var scene=EditorSceneManager.GetActiveScene();
            if(scene.path!=Map01ArtStage.Stage||EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Open the Map 1 art stage in Edit mode first.");
            var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
            File.WriteAllLines(Work+"/Audit/floor_collider_bounds.txt",all.SelectMany(t=>t.GetComponents<BoxCollider>()).Where(c=>c.name.StartsWith("COLLIDER_Floor")).Select(c=>c.transform.parent.name+" | "+c.bounds.center+" | "+c.bounds.size));
            var ground=all.Single(t=>t.name=="Continuous hills — 220 x 200").GetComponent<MeshCollider>();
            var camps=Enumerable.Range(1,3).Select(i=>all.Single(t=>t.name=="C"+i+" / Mission art")).ToArray();
            var plants=all.Where(t=>t.GetComponent<LODGroup>()!=null&&PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject).Contains("/ForestV2/")).ToArray();
            var leaves=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Environment/ForestV2/Materials/Forest leaves.mat");
            var grass=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Environment/ForestV2/Materials/Grass blades.mat");
            var colors=new[]{new Color(.40f,.49f,.29f),new Color(.43f,.55f,.38f),new Color(.52f,.57f,.31f),new Color(.46f,.51f,.29f)};
            var leafMats=colors.Select((c,i)=>Variant(leaves,"MR_Leaves_"+i,c)).ToArray();
            var grassMats=colors.Select((c,i)=>Variant(grass,"MR_Grass_"+i,Color.Lerp(c,new Color(.47f,.53f,.28f),.4f))).ToArray();
            var bark=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/MR_BarkBrown.mat");
            var hide=all.SelectMany(t=>t.GetComponents<ForestPoint>()).Where(p=>p.kind==ForestPointKind.Hide).Select(p=>p.transform.position).ToArray();
            var trees=plants.Where(IsTree).Select(t=>t.position).ToArray();int moved=0,thinned=0,recolored=0;
            bool NearCamp(Vector3 p,float extra=0)
            {return camps.Select((c,i)=>(c.position-p).sqrMagnitude<Mathf.Pow(i==2?25+extra:10+extra,2)).Any(b=>b);}
            foreach(var plant in plants)
            {
                var p=plant.position;float river=9*Mathf.Sin(p.z*.036f);float noise=Mathf.PerlinNoise((p.x+170)*.035f,(p.z+170)*.035f);
                int variant=Mathf.Abs(p.x-river)<21?1:p.y>13?3:noise>.55f?2:0;
                foreach(var r in plant.GetComponentsInChildren<MeshRenderer>(true))
                {
                    r.sharedMaterials=r.sharedMaterials.Select(m=>m.name.StartsWith("Forest leaves")||m.name.StartsWith("MR_Leaves_")?leafMats[variant]:
                        m.name.StartsWith("Grass blades")||m.name.StartsWith("MR_Grass_")?grassMats[variant]:m.name.StartsWith("Rough bark")?bark:m).ToArray();
                }
                recolored++;
                bool decorative=plant.name.StartsWith("Fern")||plant.name.StartsWith("GroundGrass");
                if(!plant.gameObject.activeInHierarchy||!decorative||NearCamp(p)||hide.Any(h=>(h-p).sqrMagnitude<1))continue;
                float patch=Mathf.PerlinNoise((p.x+143)*.12f,(p.z-71)*.12f);
                if(patch<.35f&&Mathf.Abs(p.x-river)>17)
                {plant.gameObject.SetActive(false);thinned++;continue;}
                var closest=trees.OrderBy(t=>(t-p).sqrMagnitude).FirstOrDefault();
                var delta=p-closest;delta.y=0;
                if(delta.magnitude>2&&delta.magnitude<6)
                {
                    var target=closest+delta.normalized*(1.8f+noise);target=Vector3.Lerp(p,target,.5f);
                    if(!NearCamp(target)&&GroundAt(ground,target,out var height))
                    {target.y=height;plant.position=target;moved++;}
                }
            }
            // A clear arrival corridor and irregular camp edge replace uniform growth across doors.
            foreach(var plant in plants.Where(p=>p.gameObject.activeInHierarchy))
            {
                for(int i=0;i<camps.Length;i++)
                {
                    var delta=plant.position-camps[i].position;delta.y=0;
                    var front=i==0?new Vector3(-.8f,0,.6f):i==1?new Vector3(-.5f,0,-.866f):Vector3.back;
                    float along=Vector3.Dot(delta,front.normalized),side=Vector3.Cross(delta,front.normalized).magnitude;
                    float halfWidth=i==2?19.5f:5.8f,halfDepth=i==2?17.5f:5.5f;
                    float crown=IsTree(plant)?6.5f:0;
                    bool footprint=side<halfWidth+crown&&Mathf.Abs(along)<halfDepth+crown;
                    bool entrance=along>halfDepth&&along<(i==2?31:19)&&side<(IsTree(plant)?5.5f:2.0f);
                    if(footprint||entrance){plant.gameObject.SetActive(false);break;}
                }
            }
            int disabledHide=0;
            foreach(var point in all.SelectMany(t=>t.GetComponents<ForestPoint>()).Where(p=>p.kind==ForestPointKind.Hide&&p.gameObject.activeInHierarchy))
            {
                var visual=plants.Where(p=>(p.position-point.transform.position).sqrMagnitude<1).OrderBy(p=>(p.position-point.transform.position).sqrMagnitude).FirstOrDefault();
                if(visual!=null&&!visual.gameObject.activeInHierarchy){point.gameObject.SetActive(false);disabledHide++;}
            }
            var jetty=all.Single(t=>t.name=="B — captive jetty");var wood=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/MR_WeatheredPlanks.mat");
            int boards=0;
            foreach(var t in jetty.GetComponentsInChildren<Transform>(true))
            {
                var filter=t.GetComponent<MeshFilter>();var renderer=t.GetComponent<MeshRenderer>();if(filter==null||renderer==null)continue;
                bool board=t.name=="Individual deck board"||t.name=="Handrail"||t.name=="Waiting bench"||t.name=="Bench leg"||t.name=="Boarding step"||t.name=="Mooring post";
                if(board)
                {
                    var mesh=Object.Instantiate(filter.sharedMesh);mesh.name="JettyGrain_"+boards;
                    var vertices=mesh.vertices;var normals=mesh.normals;var uv=new Vector2[vertices.Length];var scale=t.lossyScale;var b=mesh.bounds;
                    var physical=Vector3.Scale(b.size,new Vector3(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z)));
                    float offset=(boards*.173f)%1;float strip=new[]{.11f,.22f,.33f,.445f,.56f,.675f,.785f,.895f}[boards%8];
                    for(int i=0;i<uv.Length;i++)
                    {
                        var n=normals[i];int normal=Mathf.Abs(n.x)>Mathf.Abs(n.y)?(Mathf.Abs(n.x)>Mathf.Abs(n.z)?0:2):(Mathf.Abs(n.y)>Mathf.Abs(n.z)?1:2);
                        var axes=Enumerable.Range(0,3).Where(a=>a!=normal).ToArray();int longer=physical[axes[0]]>physical[axes[1]]?axes[0]:axes[1];int shorter=axes.First(a=>a!=longer);
                        uv[i]=new Vector2(strip+(vertices[i][shorter]-b.center[shorter])/b.size[shorter]*Mathf.Min(.065f,physical[shorter]*.45f),offset+(vertices[i][longer]-b.min[longer])/b.size[longer]*physical[longer]*.5f);
                    }
                    mesh.uv=uv;mesh.RecalculateTangents();string path=Root+"/StageData/Meshes/JettyGrain_"+boards+".asset";Save(mesh,path);
                    filter.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);renderer.sharedMaterial=wood;boards++;
                }
            }
            // Source colliders, boarding shelf, boat, captive and extraction transforms remain in place.
            Physics.SyncTransforms();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            File.WriteAllText(Work+"/Audit/landscape_report.json",JsonUtility.ToJson(new Report{recoloredPlants=recolored,movedDecoration=moved,thinnedDecoration=thinned,jettyBoards=boards,disabledHidingPoints=disabledHide,status="Stage only: shared foliage/wood assets unchanged. Decorative ferns/ground grass redistributed; hide-linked plants were excluded from movement. Hiding points of removed camp vegetation were disabled with their visuals. Existing jetty collider and actor transforms retained."},true));
            Recapture();Debug.Log("MAP LANDSCAPE STAGE COMPLETE");
        }
        static bool IsTree(Transform t)=>t.name.Contains("Tree")||t.name.Contains("Broadleaf")||t.name.Contains("Bamboo");
        static bool GroundAt(MeshCollider ground,Vector3 p,out float height)
        {bool hit=ground.Raycast(new Ray(new Vector3(p.x,100,p.z),Vector3.down),out var info,200);height=hit?info.point.y:p.y;return hit;}
        static Material Variant(Material source,string name,Color color)
        {
            string path=Root+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(source);AssetDatabase.CreateAsset(m,path);}m.name=name;m.SetColor("_BaseColor",color);m.SetColor("_Color",color);m.enableInstancing=true;
            EditorUtility.SetDirty(m);AssetDatabase.SaveAssetIfDirty(m);return m;
        }
        static void Save(Mesh mesh,string path)
        {var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old==null)AssetDatabase.CreateAsset(mesh,path);else{EditorUtility.CopySerialized(mesh,old);EditorUtility.SetDirty(old);AssetDatabase.SaveAssetIfDirty(old);Object.DestroyImmediate(mesh);}}
        [MenuItem("ShadowVale/Art Review/Recapture detailed art stage")]
        public static void Recapture()
        {
            if(EditorSceneManager.GetActiveScene().path!=Map01ArtStage.Stage)throw new Exception("Open art stage first.");
            var all=EditorSceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
            var camera=all.SelectMany(t=>t.GetComponents<Camera>()).Single(c=>c.name=="Art stage camera");
            var mission=Object.FindFirstObjectByType<Map01Mission>();
            var actors=all.SelectMany(t=>t.GetComponents<Animator>()).SelectMany(a=>a.GetComponentsInChildren<Renderer>(true))
                .Concat(mission!=null?mission.GetComponentsInChildren<Renderer>(true):Array.Empty<Renderer>()).Distinct().ToArray();var enabled=actors.Select(r=>r.enabled).ToArray();foreach(var r in actors)r.enabled=false;
            for(int i=0;i<3;i++)
            {
                string code="C"+(i+1);var root=all.Single(t=>t.name==code+" / Mission art");
                var front=i==0?new Vector3(-.8f,0,.6f):i==1?new Vector3(-.5f,0,-.866f):Vector3.back;
                var right=Vector3.Cross(front,Vector3.up).normalized;
                var eye=root.position+front*(i==2?32:10.5f)+right*(i==2?24:3.5f)+Vector3.up*(i==2?45:3.6f);
                camera.orthographic=i==2;camera.orthographicSize=24;camera.fieldOfView=54;
                camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(root.position+Vector3.up*(i==2?1.5f:2.0f)-eye));
                Capture(camera,Work+"/Previews/Unity_"+code+"_ArtStage.png");
                if(i==2)
                {
                    var roofs=root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name.StartsWith("ROOF /")).ToArray();
                    var roofEnabled=roofs.Select(r=>r.enabled).ToArray();foreach(var r in roofs)r.enabled=false;
                    Capture(camera,Work+"/Previews/Unity_C3_Functions_Cutaway.png");
                    for(int k=0;k<roofs.Length;k++)roofs[k].enabled=roofEnabled[k];
                    var arrival=root.position+front*24+Vector3.up*2.2f;
                    var ground=all.Single(t=>t.name=="Continuous hills — 220 x 200").GetComponent<MeshCollider>();
                    if(GroundAt(ground,arrival,out var h))arrival.y=Mathf.Max(arrival.y,h+2.0f);
                    camera.orthographic=false;camera.fieldOfView=60;camera.transform.SetPositionAndRotation(arrival,Quaternion.LookRotation(root.position+Vector3.up*3.0f-arrival));
                    Capture(camera,Work+"/Previews/Unity_C3_Arrival_ArtStage.png");
                }
            }
            camera.orthographic=false;camera.fieldOfView=52;var jettyEye=new Vector3(-18,8,74);camera.transform.SetPositionAndRotation(jettyEye,Quaternion.LookRotation(new Vector3(-3,1.4f,87)-jettyEye));
            Capture(camera,Work+"/Previews/Unity_B_Jetty_ArtStage.png");
            for(int i=0;i<actors.Length;i++)actors[i].enabled=enabled[i];
            ViewC3();EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),Map01ArtStage.Stage);
        }
        static void Capture(Camera camera,string path)
        {
            var prev=RenderTexture.active;var rt=RenderTexture.GetTemporary(1920,1080,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);var tex=new Texture2D(1920,1080,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;for(int i=0;i<3;i++)camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1920,1080),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());}
            finally{camera.targetTexture=null;RenderTexture.active=prev;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(tex);}
        }
        [MenuItem("ShadowVale/Art Review/Detailed stage - C1")]
        public static void ViewC1()=>View(1);
        [MenuItem("ShadowVale/Art Review/Detailed stage - C2")]
        public static void ViewC2()=>View(2);
        [MenuItem("ShadowVale/Art Review/Detailed stage - C3")]
        public static void ViewC3()=>View(3);
        static void View(int n)
        {
            if(EditorSceneManager.GetActiveScene().path!=Map01ArtStage.Stage)
            {if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;EditorSceneManager.OpenScene(Map01ArtStage.Stage);}
            var all=EditorSceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
            var root=all.Single(t=>t.name=="C"+n+" / Mission art");var cam=all.SelectMany(t=>t.GetComponents<Camera>()).Single(c=>c.name=="Art stage camera");
            var front=n==1?new Vector3(-.8f,0,.6f):n==2?new Vector3(-.5f,0,-.866f):Vector3.back;var right=Vector3.Cross(front,Vector3.up).normalized;
            var eye=root.position+front*(n==3?32:10.5f)+right*(n==3?24:3.5f)+Vector3.up*(n==3?45:3.6f);
            cam.orthographic=n==3;cam.orthographicSize=24;cam.fieldOfView=54;cam.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(root.position+Vector3.up*(n==3?1.5f:2.0f)-eye));
            Selection.activeGameObject=root.gameObject;EditorApplication.ExecuteMenuItem("Window/General/Game");
        }
    }
}
