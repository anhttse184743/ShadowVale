using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using ShadowVale.Map01;
using ShadowVale.Gameplay.Player;
using Object=UnityEngine.Object;
[InitializeOnLoad] public static class NightBankRouteChecks
{
 const string LiveKey="NightBankFlowLive";
 static NightBankRouteChecks(){EditorApplication.update+=LiveTick;}
 public static void BeginLive(){SessionState.SetInt(LiveKey,1);EditorApplication.isPlaying=true;}
 static void LiveTick()
 {
  int phase=SessionState.GetInt(LiveKey,0);if(phase==0)return;
  if(phase==3&&!EditorApplication.isPlayingOrWillChangePlaymode){SessionState.SetInt(LiveKey,0);EditorApplication.Exit(SessionState.GetInt(LiveKey+"Exit",1));return;}
  if(phase!=1||!EditorApplication.isPlaying)return;SessionState.SetInt(LiveKey,2);
  try{Run();SessionState.SetInt(LiveKey+"Exit",0);}catch(Exception e){Debug.LogException(e);SessionState.SetInt(LiveKey+"Exit",1);}
  SessionState.SetInt(LiveKey,3);EditorApplication.isPlaying=false;
 }
 static readonly List<string> report=new();
 static void Check(bool value,string message){if(!value)throw new Exception(message);report.Add("PASS "+message);}
 static object Call(object o,string name,params object[] a)=>o.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,a);
 static void Set(object o,string name,object v)=>o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(o,v);
 static Vector3[] Bank(string scene,string name)=>Regex.Matches(Regex.Match(scene,name+@":\r?\n((?:  - \{x:.*\r?\n)+)").Groups[1].Value,@"x: ([^,]+), y: ([^,]+), z: ([^}]+)").Select(m=>new Vector3(float.Parse(m.Groups[1].Value,CultureInfo.InvariantCulture),float.Parse(m.Groups[2].Value,CultureInfo.InvariantCulture),float.Parse(m.Groups[3].Value,CultureInfo.InvariantCulture))).ToArray();
 static float ShoreDistance(Vector3 p,Vector3[] shore)
 {
  float result=float.MaxValue;p.y=0;
  for(int i=1;i<shore.Length;i++){var a=shore[i-1];var b=shore[i];a.y=b.y=0;var line=b-a;var q=a+line*Mathf.Clamp01(Vector3.Dot(p-a,line)/line.sqrMagnitude);result=Mathf.Min(result,Vector3.Distance(q,p));}return result;
 }
 static float RoadDistance(Vector3 p)
 {
  var texture=new Texture2D(2,2);texture.LoadImage(File.ReadAllBytes("../../Assets/_Project/Art/Environment/Map02_Village/Roadside_Rounded_Lanes.png"));var colors=texture.GetPixels32();float closest=float.MaxValue;
  for(int y=0;y<texture.height;y++)for(int x=0;x<texture.width;x++)if(colors[y*texture.width+x].r>127){float dx=x*220f/(texture.width-1)-110-p.x,dz=y*200f/(texture.height-1)-100-p.z;closest=Mathf.Min(closest,dx*dx+dz*dz);}Object.DestroyImmediate(texture);return Mathf.Sqrt(closest);
 }
 public static void Run()
 {
  var nav=NavMesh.AddNavMeshData(AssetDatabase.LoadAssetAtPath<NavMeshData>("Assets/Map2NavMesh.asset"));
  var root=new GameObject("Night 2 bank route audit");
  try
  {
   var river=root.AddComponent<RiverWater>();string scene=File.ReadAllText("../../Assets/_Project/Scenes/Maps/Map 2.unity");river.leftBank=Bank(scene,"leftBank");river.rightBank=Bank(scene,"rightBank");Check(river.leftBank.Length>100,"actual saved river banks loaded");
   var c=root.AddComponent<Map02ChapterDirector>();c.enabled=false;Set(c,"root",root);
   var layout=ScriptableObject.CreateInstance<Map02ChapterLayout>();typeof(Map02ChapterDirector).GetProperty("Layout").SetValue(c,layout);
   var sites=(Dictionary<string,Transform>)typeof(Map02ChapterDirector).GetField("sites",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(c);
   foreach(var pair in new[]{("bridge",new Vector3(29,.73f,-4.66f)),("nightPatrol",Vector3.zero),("dock",new Vector3(-27.75f,.83f,-86.19f))}){var site=new GameObject(pair.Item1).transform;site.SetParent(root.transform);site.position=pair.Item2;sites[pair.Item1]=site;}
   foreach(var loc in new[]{new Vector3(-6.764572f,1,-5.646862f),new Vector3(-13.1754265f,1,-8.113138f)}){var deck=new GameObject("Walkable bridge deck");deck.transform.SetParent(root.transform);deck.transform.position=loc;deck.transform.rotation=new Quaternion(0,-.18237078f,0,.9832298f);deck.transform.localScale=new Vector3(.88f,1,1);deck.AddComponent<BoxCollider>().size=new Vector3(24,.18f,3.7f);}
   Call(c,"InitializeNightBankRoute");c.State.phase=3;c.State.completed.Add("N2_02");
   Check(Mathf.Abs(Vector3.ProjectOnPlane(c.NightDoorPosition-new Vector3(-3.971f,0,-21.858f),Vector3.up).magnitude-30)<.02f,"new hatch exactly thirty metres farther down the bank");
   Check(Vector3.ProjectOnPlane(c.NightDoorPosition-new Vector3(-3.802574f,0,-51.886887f),Vector3.up).magnitude<.25f,"new hatch lies among actual serialized young rice plants");
   c.State.completed.Remove("N2_02");Check(c.NightBankPositionAllowed(c.NightBankRoute[3]),"dry bank remains walkable before observation");c.State.completed.Add("N2_01");Check(c.NightBankPositionAllowed(c.NightBankRoute[3]),"observation does not create a road barrier");c.State.completed.Add("N2_02");
   report.Add("hatch="+c.NightDoorPosition.ToString("F3"));report.Add("observation="+c.NightObservationSpawn.ToString("F3"));
   Check(c.NightBankPositionAllowed(c.NightObservationSpawn),"actual bridge observation spawn admitted");
   var path=new NavMeshPath();
   Check(NavMesh.CalculatePath(c.NightObservationSpawn,c.NightBankRoute[0],NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete,"bridge connects to bank entry on actual NavMesh");
   for(int j=1;j<path.corners.Length;j++)for(float t=0;t<=1;t+=.025f)Check(c.NightBankPositionAllowed(Vector3.Lerp(path.corners[j-1],path.corners[j],t)),"bridge to bank native path stays open");
   for(int i=0;i<c.NightBankRoute.Length;i++)
   {
    var p=c.NightBankRoute[i];report.Add("bank "+i+"="+p.ToString("F3"));
    Check(NavMesh.SamplePosition(p,out var floor,.5f,NavMesh.AllAreas),"bank waypoint on actual NavMesh "+i);
    Check(c.NightBankPositionAllowed(p),"bank waypoint dry and within corridor "+i);
    if(i==0)continue;
    var a=c.NightBankRoute[i-1];Check(NavMesh.CalculatePath(a,p,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete,"complete native path to bank waypoint "+i);
    for(float t=0;t<=1;t+=.025f)Check(c.NightBankPositionAllowed(Vector3.Lerp(a,p,t)),"continuous dry centerline "+i+"/"+t.ToString("F3"));
    for(int j=1;j<path.corners.Length;j++)for(float t=0;t<=1;t+=.025f)Check(c.NightBankPositionAllowed(Vector3.Lerp(path.corners[j-1],path.corners[j],t)),"native path remains within allowed corridor "+i+"/"+j+"/"+t.ToString("F3"));
   }
   Check(c.NightBankPositionAllowed(c.NightDoorStand),"hatch interaction stand is accessible");
   int wetRow=Enumerable.Range(0,river.leftBank.Length).OrderBy(i=>Mathf.Abs((river.leftBank[i].z+river.rightBank[i].z)*.5f+40)).First();var wetPoint=(river.leftBank[wetRow]+river.rightBank[wetRow])*.5f;
   Check(!c.NightBankPositionAllowed(wetPoint),"actual river shortcut blocked");
   Check(c.NightBankPositionAllowed(new Vector3(30,.8f,-30)),"outer dry field stays walkable");
   Check(c.NightBankPositionAllowed(new Vector3(-40,.8f,-30)),"west village roads stay walkable");
   var from=c.NightBankRoute[2];var jump=c.ConstrainNightMovement(from,new Vector3(-30,2,0));Check(Mathf.Abs(jump.y-2)<.001f&&c.NightBankPositionAllowed(from+Vector3.ProjectOnPlane(jump,Vector3.up)),"jump retains gravity but cannot cross river bounds");
   for(int i=0;i<3;i++)
   {
    var p=(Vector3)Call(c,"NightPatrolPost",i);Check(NavMesh.SamplePosition(p,out var floor,.5f,NavMesh.AllAreas),"guard post valid "+i);report.Add("guard "+i+"="+floor.position.ToString("F3"));
    for(int j=0;j<i;j++){var q=(Vector3)Call(c,"NightPatrolPost",j);Check(Mathf.Abs(Vector3.ProjectOnPlane(p-q,Vector3.up).magnitude-(i-j)*10)<.01f,"straight row spacing ten metres per guard");}
    float shore=ShoreDistance(p,river.rightBank);report.Add("guard shoreline distance="+shore);Check(shore>=39.9f&&shore<41.4f,"straight row about forty metres from curved river bank");
    if(i==0){float road=RoadDistance(p);report.Add("first guard road-edge distance="+road);Check(Mathf.Abs(road-20)<.1f,"first guard twenty metres from actual road edge");}
    var dir=(Vector3)Call(c,"NightPatrolMissionDirection",i);Check(dir.x<-.8f,"guard scan sector faces river "+i);
   }
   c.State.nightPatrolSeed=12345;
   foreach(var point in c.NightBankRoute)
   {
    bool swept=false;
    for(float t=0;t<15;t+=.1f)for(int i=0;i<3;i++){c.State.patrolScanSeconds=t;var p=(Vector3)Call(c,"NightPatrolPost",i);var dir=(Vector3)Call(c,"NightPatrolSweepDirection",i);if(Vector3.Distance(point,p)<40&&Vector3.Angle(dir,Vector3.ProjectOnPlane(point-p,Vector3.up))<11)swept=true;}
    Check(swept,"river corridor waypoint actually crossed by a guard sweep "+point.ToString("F2"));
   }
   c.State.patrolScanSeconds=12;
   foreach(var point in c.NightBankRoute)for(int i=0;i<3;i++){var p=(Vector3)Call(c,"NightPatrolPost",i);var dir=(Vector3)Call(c,"NightPatrolSweepDirection",i);Check(Vector3.ProjectOnPlane(point-p,Vector3.up).magnitude>40||Vector3.Angle(dir,Vector3.ProjectOnPlane(point-p,Vector3.up))>11,"common dark interval clears whole bank corridor");}
   c.State.completed.Add("N2_03");Check(!c.NightBankMovementRestricted&&c.NightBankPositionAllowed(wetPoint),"only completed hatch inspection opens river crossing");
   Check(c.ConstrainNightMovement(from,new Vector3(-30,2,0))==new Vector3(-30,2,0),"movement constraint completely releases after inspection");
   var saved=JsonUtility.FromJson<Map02ChapterState>(JsonUtility.ToJson(c.State));Check(saved.Done("N2_03"),"crossing unlock persists through save/load");
   if(Application.isPlaying)CheckNaturalRiver(c,root);
   c.State.phase=2;c.State.completed.Clear();Check(c.NightBankPositionAllowed(new Vector3(90,1,90)),"other chapters unrestricted");
   Object.DestroyImmediate(layout);
  }
  catch(Exception e){report.Add("FAIL "+e);throw;}
  finally{File.WriteAllLines("night-bank-route-checks.txt",report);Object.DestroyImmediate(root);nav.Remove();}
 }
 static void CheckNaturalRiver(Map02ChapterDirector c,GameObject root)
 {
  var ground=new GameObject("Continuous ground with meandering canal");ground.transform.SetParent(root.transform);var terrain=ground.AddComponent<MeshCollider>();terrain.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Map2Ground.asset");
  var pawn=new GameObject("Nam crossing probe");pawn.transform.SetParent(root.transform);pawn.transform.position=c.NightDoorStand;
  var body=pawn.AddComponent<CharacterController>();body.height=1.8f;body.center=Vector3.up*.9f;body.radius=.3f;body.stepOffset=.3f;var motor=pawn.AddComponent<PlayerController>();motor.enabled=false;var health=pawn.AddComponent<ShadowVale.Gameplay.Combat.Health>();
  var mission=root.AddComponent<Map02Mission>();mission.enabled=false;Set(mission,"player",motor);typeof(Map02ChapterDirector).GetProperty("Mission").SetValue(c,mission);Set(c,"player",motor);
  Set(mission,"health",health);typeof(Map02Mission).GetProperty("Chapter").SetValue(mission,c);typeof(Map02Mission).GetProperty("Ready").SetValue(mission,true);mission.World.story.stage=Map02Stage.QuietPreparation;
  Check(!mission.Cinematic,"river probe runs with active map safety, not a cinematic exemption");
  Call(c,"BuildNightRiverCrossing");var ford=root.transform.Find("Đêm 2 — chỉ dẫn lội sông tự nhiên").gameObject;
  Check(ford.GetComponentsInChildren<Renderer>(true).Length==0&&ford.GetComponentsInChildren<Collider>(true).Length==0,"crossing creates no visible road, raised floor or artificial collider");
  c.State.completed.Remove("N2_03");Call(c,"SyncNightBankState");Check(!ford.activeSelf,"cross-river guidance closed before actual inspection");
  var river=root.GetComponent<RiverWater>();int row=Enumerable.Range(0,river.leftBank.Length).OrderBy(i=>Mathf.Abs((river.leftBank[i].z+river.rightBank[i].z)*.5f+40)).First();var wet=(river.leftBank[row]+river.rightBank[row])*.5f;
  Check(!c.TryUnlockedRiverSurface(wet,out _)&&!c.NightBankPositionAllowed(wet),"unfinished bank mission still blocks water");
  c.State.completed.Add("N2_03");Call(c,"SyncNightBankState");Physics.SyncTransforms();Check(ford.activeSelf&&c.TryUnlockedRiverSurface(wet,out _),"inspection unlocks natural river rather than one narrow crossing");
  var link=ford.GetComponent<Unity.AI.Navigation.NavMeshLink>();var path=new NavMeshPath();Check(NavMesh.CalculatePath(link.startPoint,link.endPoint,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete,"unlocked ford links both native bank NavMeshes");
  float length=0;for(int i=1;i<path.corners.Length;i++)length+=Vector3.Distance(path.corners[i-1],path.corners[i]);Check(length<35,"guidance takes short ford instead of returning to bridge: "+length);
  void Place(Vector3 p){body.enabled=false;pawn.transform.position=p;body.enabled=true;Physics.SyncTransforms();motor.RestoreMotion(false);}
  foreach(float z in new[]{-25f,-40f,-60f})
  {
   int index=Enumerable.Range(0,river.leftBank.Length).OrderBy(i=>Mathf.Abs((river.leftBank[i].z+river.rightBank[i].z)*.5f-z)).First();var left=river.leftBank[index];var right=river.rightBank[index];var across=Vector3.ProjectOnPlane(left-right,Vector3.up).normalized;
   var start=right-across*3;var end=left+across*3;
   Check(terrain.Raycast(new Ray(start+Vector3.up*3,Vector3.down),out var floor,6),"natural starting bank has terrain at z="+z);start.y=floor.point.y+.06f;Place(start);Call(mission,"ResetMapSafety");Call(mission,"TickMapSafety");int recoveries=mission.BoundaryRecoveryCount;float lowest=start.y;
   for(int i=0;i<1000;i++){var delta=Vector3.ProjectOnPlane(end-pawn.transform.position,Vector3.up);if(delta.magnitude<.15f)break;body.Move(c.ConstrainNightMovement(pawn.transform.position,delta.normalized*Mathf.Min(.04f,delta.magnitude)+Vector3.down*.08f));Call(mission,"TickMapSafety");lowest=Mathf.Min(lowest,pawn.transform.position.y);}
   Check(Vector3.ProjectOnPlane(pawn.transform.position-end,Vector3.up).magnitude<.2f&&mission.BoundaryRecoveryCount==recoveries,"actual Nam capsule crosses natural river at z="+z+" without recovery/teleport");Check(lowest<-.65f&&lowest> -1.2f,"crossing uses real submerged river bed at z="+z+", feet="+lowest);
  }
  Check(terrain.Raycast(new Ray(wet+Vector3.up*3,Vector3.down),out var bed,6),"natural river bed sampled for load regression");Place(bed.point+Vector3.up*.05f);Call(mission,"ResetMapSafety");int prior=mission.BoundaryRecoveryCount;Call(mission,"TickMapSafety");Check(mission.BoundaryRecoveryCount==prior,"loading while standing in unlocked river does not recover to shore");
  Place(new Vector3(wet.x,-3,wet.z));Call(mission,"TickMapSafety");Check(mission.BoundaryRecoveryCount==prior+1,"falling below real river bed still triggers recovery");
  terrain.enabled=false;Place(bed.point+Vector3.up*.05f);Call(mission,"TickMapSafety");Check(mission.BoundaryRecoveryCount==prior+2,"missing terrain in water still triggers recovery");terrain.enabled=true;
  var target=c.Layout.nightTwoEvacuationRoute[0];Check(NavMesh.CalculatePath(link.endPoint,target,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete,"west-bank exit connects to evacuation inspection route");
  c.State.completed.Remove("N2_03");Place(c.NightDoorStand);Call(c,"SyncNightBankState");Check(!ford.activeSelf&&!c.TryUnlockedRiverSurface(wet,out _)&&!c.NightBankPositionAllowed(wet),"retrying pre-hatch checkpoint restores river lock");
 }
}
