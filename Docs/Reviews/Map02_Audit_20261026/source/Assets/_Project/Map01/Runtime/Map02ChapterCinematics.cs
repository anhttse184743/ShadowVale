using System;
using UnityEngine;
using UnityEngine.AI;
namespace ShadowVale.Map01
{
    public sealed partial class Map02ChapterDirector
    {
        Vector3[] sacrificePath=Array.Empty<Vector3>();float sacrificeTravel;
        GameObject memorialArt;
        Map01CinematicPose hungActionPose;
        void ReleaseSceneAction(){hungActionPose?.Dispose();hungActionPose=null;ClearFinale();}
        public Vector3 ChargePoint
        {
            get
            {
                // The flank of the tracks on the bank side, where Hùng comes up out of the ditch.
                var body=tank!=null?tank.transform:null;var at=body!=null?body.position:sites["tankDock"].position;
                var side=body!=null?(Vector3.Dot(body.right,sites["ditch"].position-at)>=0?body.right:-body.right):(sites["ditch"].position-at).normalized;
                var point=at+side*2.2f-(body!=null?body.forward*.4f:Vector3.zero);
                return NavMesh.SamplePosition(point,out var h,4,NavMesh.AllAreas)?h.position:point;
            }
        }
        Vector3 RetreatPoint
        {
            get{var point=ChargePoint+(sites["ditch"].position-ChargePoint).normalized*3;return NavMesh.SamplePosition(point,out var h,2,NavMesh.AllAreas)?h.position:ChargePoint;}
        }
        float SceneDuration=>sceneTask?.id=="N1_04"||sceneTask?.id=="N1_01"?DaySceneMinimum:sceneTask?.seconds??0;
        void StartRescueEscort(Map02ChapterTask task)
        {
            escort=roles[task.kind=="rescueChild"?"Bé Tí":"Ông Sáu"];escort.Follow(Mission.Player,1.5f);
            secondaryEscort=task.kind=="rescueFamily"?roles["Chị Lành"]:null;secondaryEscort?.Follow(Mission.Player,1.45f);
            Mission.Say(secondaryEscort!=null?"Đưa cả ông Sáu và chị Lành tới bến nam. Giữ hai người trong tầm đi cùng.":"Đưa bé Tí tới bến nam, nơi chị Lành đang chờ.");UpdateMarkers();
        }
        void PrepareSceneMotion(Map02ChapterTask task)
        {
            sacrificePath=Array.Empty<Vector3>();sacrificeTravel=0;
            PrepareFinale(task);
        }
        static Vector3 Along(Vector3[] path,float distance)
        {
            for(int i=1;i<path.Length;i++){float span=Vector3.Distance(path[i-1],path[i]);if(distance<=span)return Vector3.Lerp(path[i-1],path[i],span>.001f?distance/span:0);distance-=span;}
            return path[path.Length-1];
        }
        void RefreshMemorial()
        {
            if(memorialArt==null)
            {
                memorialArt=new GameObject("Dawn covered Hùng — art attachment slot");memorialArt.transform.SetParent(root.transform);memorialArt.transform.position=sites["ditch"].position+Vector3.forward*2.5f;
                if(Layout!=null&&Layout.coveredHungArt!=null)Instantiate(Layout.coveredHungArt,memorialArt.transform,false);
            }
            memorialArt.SetActive(State.phase==6&&State.hungDead);
        }
    }
}
