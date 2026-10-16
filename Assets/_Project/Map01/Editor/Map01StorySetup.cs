using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object=UnityEngine.Object;

namespace ShadowVale.Map01.Editor
{
    public static class Map01StorySetup
    {
        const string Map="Assets/_Project/Scenes/Maps/Map 1.unity";
        const string Art="Assets/_Project/Map01/Resources/Story/";
        [MenuItem("ShadowVale/Story/Prepare Map 1 operations")]
        public static void Prepare()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play before authoring the operation markers.");
            var current=EditorSceneManager.GetActiveScene();
            if(current.isDirty)throw new InvalidOperationException("Save the current scene before preparing Map 1 operations.");
            var scene=current.path==Map?current:EditorSceneManager.OpenScene(Map);
            var existing=Object.FindFirstObjectByType<Map01OperationsLayout>();
            if(existing!=null){Validate(existing);return;}
            Directory.CreateDirectory(Art);
            var root=new GameObject("Map01 Operations — commander orders");
            var layout=root.AddComponent<Map01OperationsLayout>();
            var c1=GameObject.Find("Enemy outpost 1").transform;
            var c2=GameObject.Find("Enemy outpost 2").transform;
            var c3=GameObject.Find("Enemy outpost 3").transform;
            // Reuse the existing, fitted camp art. No extra radio table/supply pile is stacked over it.
            layout.supply=Prop(c1,"SupplyStack");layout.radio=Prop(c2,"RadioStation");
            layout.supplyFull=layout.supply.gameObject;
            layout.supplyApproach=Marker(root.transform,"C1 Supply interaction",ReachableBeside(layout.supply.position,c1.position),layout.supply.position);
            layout.radioApproach=Marker(root.transform,"C2 Radio interaction",ReachableBeside(layout.radio.position,c2.position),layout.radio.position);
            layout.commanderPost=Marker(root.transform,"C3 Commander post",Walkable(c3.TransformPoint(new Vector3(-.3f,0,1.2f)),3),c3.position+Vector3.back*6);
            // A separate empty crate is revealed only after this finite supply objective is taken.
            var crate=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Environment/Map01_Optimized/Prefabs/Prop_Crate.prefab");
            if(crate==null)throw new Exception("Existing Prop_Crate asset missing.");
            var empty=(GameObject)PrefabUtility.InstantiatePrefab(crate,root.transform);
            empty.name="Recovered supply — empty crate";empty.transform.SetPositionAndRotation(layout.supply.position,layout.supply.rotation);
            empty.transform.localScale=Vector3.one*.65f;Ground(empty,layout.supply.position.y);
            layout.supplyEmpty=empty;empty.SetActive(false);
            layout.radioPowered=Status(root.transform,layout.radio,"Radio power — on",new Color(.25f,.55f,.22f));
            layout.radioDisabled=Status(root.transform,layout.radio,"Radio power — off",new Color(.13f,.13f,.1f));
            layout.radioDisabled.SetActive(false);
            Validate(layout);
            EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene))throw new Exception("Cannot save Map 1 operations.");
            AssetDatabase.SaveAssets();
            Debug.Log("[Map01 Story] Existing supply/radio art linked; distinct, reachable operation markers saved in Map 1.");
        }
        static Transform Prop(Transform root,string name)=>root.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(t=>t.name==name||t.name.StartsWith(name+" (",StringComparison.Ordinal))??throw new Exception("Missing "+name+" under "+root.name);
        static Vector3 Walkable(Vector3 at,float radius)
        {
            if(!NavMesh.SamplePosition(at,out var hit,radius,NavMesh.AllAreas))throw new Exception("No walkable ground near "+at);
            return hit.position;
        }
        static Vector3 ReachableBeside(Vector3 prop,Vector3 center)
        {
            var origin=Walkable(center,4);
            foreach(float radius in new[]{1.1f,1.5f,1.9f})for(int i=0;i<16;i++)
            {
                var p=prop+Quaternion.Euler(0,180+i*22.5f,0)*Vector3.forward*radius;
                if(!NavMesh.SamplePosition(p,out var hit,.4f,NavMesh.AllAreas)||Mathf.Abs(hit.position.y-prop.y)>1)continue;
                var path=new NavMeshPath();if(!NavMesh.CalculatePath(origin,hit.position,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)continue;
                if(Physics.OverlapCapsule(hit.position+Vector3.up*.45f,hit.position+Vector3.up*1.4f,.27f,~0,QueryTriggerInteraction.Ignore)
                    .Any(c=>!(c is TerrainCollider)&&c.bounds.max.y>hit.position.y+.4f))continue;
                return hit.position;
            }
            throw new Exception("No clear interaction approach beside "+prop);
        }
        static Transform Marker(Transform parent,string name,Vector3 p,Vector3 face)
        {
            var t=new GameObject(name).transform;t.SetParent(parent);t.position=p;
            var direction=Vector3.ProjectOnPlane(face-p,Vector3.up);if(direction.sqrMagnitude>.01f)t.rotation=Quaternion.LookRotation(direction);
            return t;
        }
        static GameObject Status(Transform root,Transform radio,string name,Color color)
        {
            var status=GameObject.CreatePrimitive(PrimitiveType.Cube);status.name=name;status.transform.SetParent(root);
            var bounds=radio.GetComponentsInChildren<Renderer>().Select(r=>r.bounds).Aggregate((a,b)=>{a.Encapsulate(b);return a;});
            status.transform.position=new Vector3(bounds.center.x,bounds.max.y+.01f,bounds.center.z);
            status.transform.localScale=new Vector3(.06f,.02f,.04f);Object.DestroyImmediate(status.GetComponent<Collider>());
            string path=Art+name.Replace(' ','_')+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.color=color;AssetDatabase.CreateAsset(material,path);}
            status.GetComponent<Renderer>().sharedMaterial=material;return status;
        }
        static void Ground(GameObject prop,float y)
        {
            var renderers=prop.GetComponentsInChildren<Renderer>();if(renderers.Length==0)return;
            prop.transform.position+=Vector3.up*(y-renderers.Min(r=>r.bounds.min.y));
        }
        public static void Validate(Map01OperationsLayout layout)
        {
            foreach(var marker in new[]{layout.supplyApproach,layout.radioApproach,layout.commanderPost})
                if(marker==null||!NavMesh.SamplePosition(marker.position,out var hit,.4f,NavMesh.AllAreas)||Vector3.Distance(marker.position,hit.position)>.2f)
                    throw new Exception("Operation marker must lie on the NavMesh: "+marker);
            if(layout.supplyFull==null||layout.supplyEmpty==null||layout.radioPowered==null||layout.radioDisabled==null)throw new Exception("Incomplete operation visuals.");
        }
    }
}
