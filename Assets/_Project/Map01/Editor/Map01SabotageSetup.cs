using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.EditorTools
{
    /// <summary>Reproducible native Unity props: no imported character rigs or paid assets.</summary>
    public static class Map01SabotageSetup
    {
        const string Root="Assets/_Project/Art/Environment/RadioSabotage";
        static Material metal,dark,copper,rubber,red,ceramic,glass;
        static int serial;
        static Material Mat(string name,Color color,float shine=.18f){
            string path=Root+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
            m.color=color;m.SetFloat("_Smoothness",shine);EditorUtility.SetDirty(m);return m;
        }
        static GameObject Part(Transform parent,string name,Vector3 at,Vector3 size,Material mat,PrimitiveType type=PrimitiveType.Cube){
            var g=GameObject.CreatePrimitive(type);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=at;g.transform.localScale=size;
            Object.DestroyImmediate(g.GetComponent<Collider>());g.GetComponent<Renderer>().sharedMaterial=mat;return g;
        }
        static Transform Node(Transform parent,string name,Vector3 at){var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=at;return t;}
        static void Tube(Transform parent,string name,Vector3[] pts,float radius,Material mat){
            const int sides=8;var v=new List<Vector3>();var tris=new List<int>();
            for(int i=0;i<pts.Length;i++){
                var tangent=(pts[Mathf.Min(i+1,pts.Length-1)]-pts[Mathf.Max(0,i-1)]).normalized;
                var side=Vector3.Cross(tangent,Mathf.Abs(tangent.y)>.9f?Vector3.forward:Vector3.up).normalized;var up=Vector3.Cross(tangent,side);
                for(int j=0;j<sides;j++)v.Add(pts[i]+radius*(Mathf.Cos(j*2*Mathf.PI/sides)*side+Mathf.Sin(j*2*Mathf.PI/sides)*up));
                if(i==0)continue;for(int j=0;j<sides;j++){int a=(i-1)*sides+j,b=(i-1)*sides+(j+1)%sides,c=i*sides+j,d=i*sides+(j+1)%sides;tris.AddRange(new[]{a,c,b,b,c,d});}
            }
            var mesh=new Mesh{name=name};mesh.SetVertices(v);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            string path=Root+"/Meshes/"+(serial++).ToString("D3")+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(old==null)AssetDatabase.CreateAsset(mesh,path);else{EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);mesh=old;}
            var g=Node(parent,name,Vector3.zero).gameObject;g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=mat;
        }
        static Vector3[] Curve(Vector3 a,Vector3 b,Vector3 c,Vector3 d){var p=new Vector3[25];for(int i=0;i<p.Length;i++){float t=i/24f,u=1-t;p[i]=u*u*u*a+3*u*u*t*b+3*u*t*t*c+t*t*t*d;}return p;}
        static void Dial(Transform parent,Vector3 at,float radius,bool broken=false){
            var rim=Part(parent,"Instrument bezel",at,new Vector3(radius*2,.014f,radius*2),dark,PrimitiveType.Cylinder);rim.transform.localRotation=Quaternion.Euler(90,0,0);
            var face=Part(parent,"Unlit gauge",at+Vector3.forward*.012f,new Vector3(radius*1.65f,.008f,radius*1.65f),broken?dark:ceramic,PrimitiveType.Cylinder);face.transform.localRotation=Quaternion.Euler(90,0,0);
            if(!broken){var needle=Part(parent,"Voltmeter needle",at+new Vector3(.012f,.012f,.024f),new Vector3(.006f,radius,.006f),dark);needle.transform.localRotation=Quaternion.Euler(0,0,-38);}
        }
        public static void Build(){
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Exit Play Mode before authoring sabotage props.");
            Directory.CreateDirectory(Root+"/Meshes");AssetDatabase.Refresh();serial=0;
            metal=Mat("Olive worn casing",new Color(.20f,.23f,.17f));dark=Mat("Charred steel",new Color(.035f,.034f,.028f));
            copper=Mat("Exposed copper",new Color(.48f,.25f,.095f),.35f);rubber=Mat("Cable rubber",new Color(.045f,.042f,.033f));red=Mat("Faded red insulation",new Color(.29f,.065f,.04f));ceramic=Mat("Old ceramic",new Color(.64f,.60f,.43f));glass=Mat("Damaged painted cover",new Color(.11f,.13f,.10f));
            var scene=EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");var l=Object.FindFirstObjectByType<Map01OperationsLayout>();
            if(l==null)throw new Exception("Missing operations layout.");
            if(l.sabotage!=null)Object.DestroyImmediate(l.sabotage.gameObject);
            var root=Node(l.transform,"Point 2 — radio damage and power sabotage",Vector3.zero);var props=root.gameObject.AddComponent<Map01SabotageProps>();l.sabotage=props;
            props.healthyRadio=Array.Empty<Renderer>();
            var face=(l.radioRightContact.position+l.radioLeftContact.position)*.5f+Vector3.up*.12f;
            var front=Vector3.ProjectOnPlane(l.radioApproach.position-face,Vector3.up).normalized;
            var broken=Node(root,"Radio — ruptured face and exposed wires",Vector3.zero);broken.SetPositionAndRotation(face,Quaternion.LookRotation(front));props.damagedRadio=broken.gameObject;
            Part(broken,"Burnt open chassis",new Vector3(0,0,.015f),new Vector3(.67f,.36f,.035f),dark);
            for(int i=0;i<5;i++){
                var torn=Part(broken,"Bent sheet metal",new Vector3(-.29f+i*.145f,.16f-i%2*.025f,.03f),new Vector3(.15f,.055f,.045f),glass);torn.transform.localRotation=Quaternion.Euler(-20-i*9,0,i%2==0?12:-15);
            }
            var flap=Part(broken,"Torn control cover hanging from hinge",new Vector3(.21f,-.17f,.16f),new Vector3(.30f,.26f,.025f),metal);flap.transform.localRotation=Quaternion.Euler(32,-18,16);
            Dial(broken,new Vector3(-.2f,.045f,.045f),.082f,true);
            for(int i=0;i<5;i++){
                float x=-.23f+i*.105f;float drop=.16f+(i*7%4)*.055f;float reach=.19f+(i%3)*.045f;
                Tube(broken,"Loose insulated lead "+i,Curve(new Vector3(x,.11f,.045f),new Vector3(x+.10f,-drop*.3f,reach),new Vector3(x-.12f,-drop*1.25f,reach+.07f),new Vector3(x-.07f,-drop,reach)),.0045f+i*.0003f,i%2==0?red:rubber);
                Tube(broken,"Stripped copper end "+i,Curve(new Vector3(x-.07f,-drop,reach),new Vector3(x-.09f,-drop-.02f,reach),new Vector3(x-.10f,-drop-.03f,reach+.02f),new Vector3(x-.11f,-drop-.035f,reach+.025f)),.0018f,copper);
            }
            for(int i=0;i<4;i++)Part(broken,"Cracked ceramic terminal",new Vector3(-.22f+i*.13f,.015f,.055f),new Vector3(.045f,.065f,.035f),ceramic);
            // Place a floor-standing field power converter beside the desk, with a reachable front.
            Vector3 body=default,stand=default;bool found=false;
            var right=Quaternion.LookRotation(front)*Vector3.right;
            foreach(var side in new[]{1f,-1f})foreach(float along in new[]{1.4f,1.8f,2.2f,2.7f}){
                var desired=l.radioApproach.position+right*side*along-front*.2f;
                if(!NavMesh.SamplePosition(desired,out var at,.5f,NavMesh.AllAreas))continue;
                var pos=Map01OperationSession.GroundPoint(at.position,null);
                if(Physics.OverlapBox(pos+Vector3.up*.6f,new Vector3(.38f,.45f,.28f),Quaternion.LookRotation(front),~0,QueryTriggerInteraction.Ignore).Any(c=>!(c is TerrainCollider)&&c.bounds.max.y>pos.y+.25f))continue;
                if(!NavMesh.SamplePosition(pos+front*.85f,out var a,.5f,NavMesh.AllAreas))continue;
                var path=new NavMeshPath();if(!NavMesh.CalculatePath(l.radioApproach.position,a.position,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)continue;
                body=pos;stand=a.position;found=true;break;
            }
            if(!found)throw new Exception("No clear power converter footprint beside radio; inspect placement before saving.");
            var power=Node(root,"Field power converter",Vector3.zero);power.SetPositionAndRotation(body,Quaternion.LookRotation(front));
            Part(power,"Steel body",new Vector3(0,.58f,0),new Vector3(.62f,.92f,.42f),metal);
            Part(power,"Steel skid",new Vector3(0,.08f,0),new Vector3(.78f,.12f,.58f),dark);
            for(int side=-1;side<=1;side+=2)for(int j=0;j<7;j++)Part(power,"Cooling slit",new Vector3(side*.312f,.36f+j*.065f,0),new Vector3(.006f,.018f,.28f),dark);
            for(int side=-1;side<=1;side+=2)Tube(power,"Carry handle",Curve(new Vector3(side*.22f,1.04f,-.09f),new Vector3(side*.22f,1.20f,-.09f),new Vector3(side*.22f,1.20f,.09f),new Vector3(side*.22f,1.04f,.09f)),.017f,dark);
            var healthy=Node(power,"Connected fuse panel",Vector3.zero);props.healthyPower=healthy.gameObject;
            Part(healthy,"Front service cover",new Vector3(0,.63f,.22f),new Vector3(.53f,.72f,.025f),glass);Dial(healthy,new Vector3(0,.50f,.25f),.09f);
            var ruined=Node(power,"Burned and opened fuse panel",Vector3.zero);props.damagedPower=ruined.gameObject;
            Part(ruined,"Scorched cavity",new Vector3(0,.62f,.225f),new Vector3(.53f,.74f,.03f),dark);Dial(ruined,new Vector3(0,.50f,.25f),.09f,true);
            var door=Part(ruined,"Buckled service cover",new Vector3(-.34f,.63f,.32f),new Vector3(.30f,.67f,.025f),metal);door.transform.localRotation=Quaternion.Euler(7,-64,-12);
            for(int i=0;i<3;i++){
                float x=-.16f+i*.16f;Part(healthy,"Ceramic fuse "+i,new Vector3(x,.85f,.27f),new Vector3(.055f,.095f,.05f),ceramic,PrimitiveType.Capsule);
                foreach(float y in new[]{.76f,.94f}){Part(healthy,"Fuse terminal",new Vector3(x,y,.28f),new Vector3(.075f,.035f,.05f),copper);Part(ruined,"Empty fuse socket",new Vector3(x,y,.27f),new Vector3(.075f,.035f,.05f),dark);}
                Tube(ruined,"Charred loose power lead",Curve(new Vector3(x,.73f,.27f),new Vector3(x+.1f,.56f,.40f),new Vector3(x-.07f,.3f,.45f),new Vector3(x-.08f,.35f,.36f)),.007f,i==1?red:rubber);
            }
            props.smokeOrigin=Node(power,"Smoke from burnt winding",new Vector3(0,1.03f,-.08f));
            props.powerRightContact=Node(power,"Right hand fuse contact",new Vector3(-.16f,.85f,.29f));props.powerLeftContact=Node(power,"Left hand panel brace",new Vector3(.24f,.87f,.25f));
            props.powerApproach=Node(root,"Power interaction — reachable front",Vector3.zero);props.powerApproach.SetPositionAndRotation(stand,Quaternion.LookRotation(-front));
            var box=power.gameObject.AddComponent<BoxCollider>();box.center=new Vector3(0,.58f,0);box.size=new Vector3(.64f,1.05f,.44f);
            var obstacle=power.gameObject.AddComponent<NavMeshObstacle>();obstacle.shape=NavMeshObstacleShape.Box;obstacle.center=box.center;obstacle.size=box.size;obstacle.carving=true;
            Vector3 start=power.TransformPoint(new Vector3(.18f,.22f,-.23f));Vector3 end=l.radioWorkbench.position+Vector3.up*.12f;
            Tube(root,"Radio power cable — disconnected after sabotage",Curve(root.InverseTransformPoint(start),root.InverseTransformPoint(start-Vector3.up*.2f),root.InverseTransformPoint(end-Vector3.up*.12f),root.InverseTransformPoint(end)),.009f,rubber);
            props.Present(false,false);EditorUtility.SetDirty(l);EditorUtility.SetDirty(props);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Logs/PowerSabotage");File.WriteAllText("Logs/PowerSabotage/placement.txt","Body "+body+" Stand "+stand+" Radio face "+face+"\nNative meshes generated by Map01SabotageSetup; cosmetic smoke budget 48 particles.");
            EditorApplication.Exit(0);
        }
    }
}
