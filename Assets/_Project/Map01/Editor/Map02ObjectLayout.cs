using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object=UnityEngine.Object;

namespace ShadowVale.Map01.Editor
{
    public static class Map02ObjectLayout
    {
        const string Folder="Assets/_Project/Map01/Resources/Story";
        [MenuItem("ShadowVale/Map 2/Build quest objects from village navigation")]
        public static void Build()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Stop Play before building the layout.");
            if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty&&!Application.isBatchMode)throw new InvalidOperationException("Save scene edits before building the quest object layout.");
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");
            var houses=Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t=>t.name.StartsWith("Stilt house",StringComparison.Ordinal)||t.name.StartsWith("Bamboo thatch house",StringComparison.Ordinal)).OrderBy(t=>t.position.z).ThenBy(t=>t.position.x).ToArray();
            if(houses.Length<8)throw new InvalidOperationException("Expected at least eight existing village homes.");
            var dock=Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r=>r.name=="Timber evacuation landing"&&r.bounds.center.z<0).OrderBy(r=>r.bounds.center.z).First();
            var foliageBounds=Object.FindObjectsByType<LODGroup>(FindObjectsSortMode.None)
                .SelectMany(g=>g.GetLODs().Length>0?g.GetLODs()[0].renderers:Array.Empty<Renderer>())
                .Where(r=>r!=null&&r.bounds.size.y>1&&r.bounds.size.magnitude<35).Select(r=>r.bounds).ToArray();
            Vector3 Dry(Vector3 point,float radius=8)
            {
                // A navigable point under a stilt-house floor is not necessarily tall enough
                // for Nam or his camera pivot. Test the standing capsule as well as the path.
                Vector3 best=default;float bestScore=float.NegativeInfinity;
                for(float ring=0;ring<=radius;ring+=2)
                    for(int direction=0;direction<(ring==0?1:12);direction++)
                    {
                        float angle=direction*Mathf.PI/6;
                        var candidate=point+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*ring;
                        if(!NavMesh.SamplePosition(candidate,out var hit,ring==0?radius:1.5f,NavMesh.AllAreas)||hit.position.y<.5f)continue;
                        if(Physics.CheckCapsule(hit.position+Vector3.up*.36f,hit.position+Vector3.up*1.62f,.30f,~0,QueryTriggerInteraction.Ignore))continue;
                        // Tree crowns often have no collider. A physically clear marker can
                        // still put the default spring-arm camera inside an opaque leaf mesh.
                        float score=-ring*.04f;
                        foreach(var offset in new[]{new Vector3(4.3f,2.7f,1),new Vector3(-4.3f,2.7f,-1),new Vector3(0,2.7f,4.3f),new Vector3(0,2.7f,-4.3f)})
                            if(!foliageBounds.Any(b=>b.Contains(hit.position+offset)))score+=offset.x>4?10:1;
                        if(score>bestScore){bestScore=score;best=hit.position;}
                    }
                if(!float.IsNegativeInfinity(bestScore))return best;
                throw new InvalidOperationException("No standing clearance on dry NavMesh at "+point);
            }
            Vector3 Home(int index)=>Dry(houses[index].position-houses[index].forward*8);
            var homePoints=Enumerable.Range(0,houses.Length).Select(Home).ToArray();
            Vector3 command=homePoints[houses.Length/2];
            Vector3 south=Dry(dock.bounds.center+Vector3.left*10,18);
            Vector3 scout=Dry(new Vector3(24,1,-8),18);
            var root=new GameObject("Map 2 quest objects (civilian art deferred)");var layout=root.AddComponent<Map02QuestLayout>();layout.homePositions=homePoints;
            var targets=new List<Map02ObjectiveObject>();var props=new Dictionary<string,GameObject>();
            var wood=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Map02QuestWood.mat");
            if(wood==null){wood=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Map02QuestWood",color=new Color(.22f,.16f,.09f)};AssetDatabase.CreateAsset(wood,Folder+"/Map02QuestWood.mat");}
            Transform Marker(string name,Vector3 point){var go=new GameObject(name);go.transform.SetParent(root.transform);go.transform.position=point;return go.transform;}
            void Add(Map02Work work,string propId,string label,Vector3 point,bool small=false)
            {
                if(!props.TryGetValue(propId,out var prop))
                {
                    prop=GameObject.CreatePrimitive(PrimitiveType.Cube);prop.name=propId;prop.transform.SetParent(root.transform);prop.transform.position=point+Vector3.right+Vector3.up*(small?.2f:.42f);prop.transform.localScale=small?new Vector3(.45f,.4f,.35f):new Vector3(1,.84f,.65f);prop.GetComponent<MeshRenderer>().sharedMaterial=wood;props.Add(propId,prop);
                }
                var target=prop.AddComponent<Map02ObjectiveObject>();target.saveId="m2_"+work;target.work=work;target.prompt=label;target.standPoint=Marker("Use_"+work,point);targets.Add(target);
            }
            try
            {
                Add(Map02Work.Contact,"Contact table","Bắt liên lạc",command);
                Add(Map02Work.ScoutOrder,"Contact table","Báo đã xong việc, nhận tin trinh sát",command);
                Add(Map02Work.ScoutReport,"Contact table","Báo cáo đã đưa trinh sát về",command);
                Add(Map02Work.MedicinePickup,"Medicine box","Nhận gói thuốc cần giao",homePoints[houses.Length/2+1],true);
                Add(Map02Work.MedicineDelivery,"Family doorway","Giao thuốc cho gia đình",homePoints[0],true);
                Add(Map02Work.CratePickup,"Supply crate","Nhận kiện vật tư",homePoints[houses.Length-1]);
                Add(Map02Work.CrateDelivery,"Storage","Đặt kiện vật tư vào chỗ cất",homePoints[houses.Length/2-1]);
                Add(Map02Work.ScoutAid,"Scout aid point","Tìm và giúp trinh sát bị thương",scout,true);
                Add(Map02Work.GatherPeople,"Assembly instructions","Tập kết người cần giúp đỡ",homePoints[houses.Length-2],true);
                Add(Map02Work.SouthStockPickup,"Storage","Nhận kiện vật tư đưa xuống bến nam",homePoints[houses.Length/2-1]);
                Add(Map02Work.SouthStockDelivery,"South stores","Đặt vật tư tại tuyến sơ tán",south+Vector3.left*3);
                Add(Map02Work.CheckBoats,"Boat inspection","Kiểm tra thuyền ở bến nam",south);
                Add(Map02Work.InnerLane,"Inner line","Lùi về chốt trong",command);
                Add(Map02Work.RescueFamily,"Family doorway","Mở lối đưa gia đình xuống bến",homePoints[0],true);
                Add(Map02Work.RescueNeighbours,"Second household","Mở lối đưa nhóm dân còn lại xuống bến",homePoints[2],true);
                Add(Map02Work.LastBoat,"Boat inspection","Lên chuyến thuyền cuối",south);
                layout.objects=targets.ToArray();layout.scoutShelter=Marker("Scout shelter",command+Vector3.back*3);layout.southAssembly=Marker("South assembly",south+Vector3.left*5);layout.innerLane=Marker("Inner line",command);layout.southDock=Marker("South dock",south);
                var reports=new List<string>();
                foreach(var t in layout.objects)
                {
                    var point=Dry(t.standPoint.position,2);t.standPoint.position=point;
                    var path=new NavMeshPath();bool valid=NavMesh.CalculatePath(south,point,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete;
                    reports.Add((valid?"PASS ":"FAIL ")+t.saveId+" "+point);
                    if(!valid)throw new InvalidOperationException("Unreachable quest object "+t.saveId);
                }
                foreach(var marker in new[]{layout.scoutShelter,layout.southAssembly})marker.position=Dry(marker.position,3);
                PrefabUtility.SaveAsPrefabAsset(root,Folder+"/Map02QuestObjects.prefab");AssetDatabase.SaveAssets();
                Directory.CreateDirectory("Logs/Map02QuestObjects");File.WriteAllLines("Logs/Map02QuestObjects/navigation.txt",reports);
                Debug.Log("Map 2 quest objects built: "+targets.Count+" reachable interactions; scene terrain unchanged.");
            }
            finally {Object.DestroyImmediate(root);}
        }
    }
}
