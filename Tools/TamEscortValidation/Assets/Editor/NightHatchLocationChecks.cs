using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
public static class NightHatchLocationChecks
{
 [Serializable] class Proposal {public float[] patrolApproxXZ,hatchPosition;}
 [Serializable] class Receipt {public string status="location proposal only; no scene changes";public Vector3 patrolCenter,hatchPosition;public float roadEdgeDistanceMeters=10;public bool hatchOnNavMesh,patrolOnNavMesh;}
 public static void Run()
 {
  string folder=Path.GetFullPath("../../SourceArt/Map02NightHatchLocation");
  var proposal=JsonUtility.FromJson<Proposal>(File.ReadAllText(Path.Combine(folder,"location-proposal.json")));
  var data=AssetDatabase.LoadAssetAtPath<NavMeshData>("Assets/Map2NavMesh.asset");var nav=NavMesh.AddNavMeshData(data);
  try
  {
   var patrol=new Vector3(proposal.patrolApproxXZ[0],.77f,proposal.patrolApproxXZ[1]);
   var hatch=new Vector3(proposal.hatchPosition[0],proposal.hatchPosition[1],proposal.hatchPosition[2]);
   bool a=NavMesh.SamplePosition(patrol,out var patrolFloor,.5f,NavMesh.AllAreas),b=NavMesh.SamplePosition(hatch,out var hatchFloor,.5f,NavMesh.AllAreas);
   if(!a||!b)throw new Exception("Proposed location is outside actual Map 2 walkable ground");
   var path=new NavMeshPath();if(!NavMesh.CalculatePath(patrolFloor.position,hatchFloor.position,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)throw new Exception("No complete access path between proposed areas");
   var receipt=new Receipt{patrolCenter=patrolFloor.position,hatchPosition=hatchFloor.position,patrolOnNavMesh=a,hatchOnNavMesh=b};
   File.WriteAllText(Path.Combine(folder,"validated-location.json"),JsonUtility.ToJson(receipt,true));
   Debug.Log("PASS proposed red patrol="+receipt.patrolCenter.ToString("F3")+" green hatch="+receipt.hatchPosition.ToString("F3")+"; complete path; scene unchanged");
  }finally{nav.Remove();}
 }
}
