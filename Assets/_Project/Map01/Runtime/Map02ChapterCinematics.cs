using System;
using UnityEngine;
using UnityEngine.AI;
using ShadowVale.Gameplay.Audio;
namespace ShadowVale.Map01
{
    public sealed partial class Map02ChapterDirector
    {
        Vector3[] sacrificePath=Array.Empty<Vector3>();float sacrificeTravel;bool executionShot;
        GameObject memorialArt;
        Map01CinematicPose hungActionPose;
        void ReleaseSceneAction(){hungActionPose?.Dispose();hungActionPose=null;}
        public Vector3 ChargePoint
        {
            get{var point=(tank!=null?tank.transform.position:sites["lane3"].position+Vector3.forward*8)+Vector3.left*2.2f;return NavMesh.SamplePosition(point,out var h,4,NavMesh.AllAreas)?h.position:point;}
        }
        Vector3 RetreatPoint
        {
            get{var point=ChargePoint+(sites["ditch"].position-ChargePoint).normalized*3;return NavMesh.SamplePosition(point,out var h,2,NavMesh.AllAreas)?h.position:ChargePoint;}
        }
        float SceneDuration=>sceneTask!=null&&sceneTask.id=="N3_09"?Mathf.Max(sceneTask.seconds,sacrificeTravel+18):sceneTask?.seconds??0;
        void StartRescueEscort(Map02ChapterTask task)
        {
            escort=roles[task.kind=="rescueChild"?"Bé Tí":"Ông Sáu"];escort.Follow(Mission.Player,1.5f);
            secondaryEscort=task.kind=="rescueFamily"?roles["Chị Lành"]:null;secondaryEscort?.Follow(Mission.Player,1.45f);
            Mission.Say(secondaryEscort!=null?"Đưa cả ông Sáu và chị Lành tới bến nam. Giữ hai người trong tầm đi cùng.":"Đưa bé Tí tới bến nam, nơi chị Lành đang chờ.");UpdateMarkers();
        }
        void PrepareSceneMotion(Map02ChapterTask task)
        {
            sacrificePath=Array.Empty<Vector3>();sacrificeTravel=0;executionShot=false;
            if(task.id!="N3_09")return;
            var path=new NavMeshPath();
            if(NavMesh.CalculatePath(roles["Hùng"].transform.position,ChargePoint,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete)
            {sacrificePath=path.corners;for(int i=1;i<sacrificePath.Length;i++)sacrificeTravel+=Vector3.Distance(sacrificePath[i-1],sacrificePath[i])/1.8f;}
        }
        static Vector3 Along(Vector3[] path,float distance)
        {
            for(int i=1;i<path.Length;i++){float span=Vector3.Distance(path[i-1],path[i]);if(distance<=span)return Vector3.Lerp(path[i-1],path[i],span>.001f?distance/span:0);distance-=span;}
            return path[path.Length-1];
        }
        void SampleSacrifice()
        {
            var hung=roles["Hùng"];
            if(sceneClock<sacrificeTravel&&sacrificePath.Length>1)
            {var at=Along(sacrificePath,sceneClock*1.8f);hung.SceneMove(at,Along(sacrificePath,(sceneClock+.2f)*1.8f),1.8f);}
            else if(sceneClock<sacrificeTravel+7)hung.SceneMove(ChargePoint,tank.transform.position,0);
            else if(sceneClock<sacrificeTravel+11)hung.SceneMove(Vector3.Lerp(ChargePoint,RetreatPoint,Mathf.Clamp01((sceneClock-sacrificeTravel-7)/3)),RetreatPoint,1);
            else
            {
                if(!executionShot)
                {
                    executionShot=true;hung.StopAt(RetreatPoint);if(Layout==null||Layout.hungPlantRetreatClip==null)hung.SetDead(true);
                    var sound=Mission.Player.GetComponent<CombatAudio>();if(sound!=null&&sound.Gunshot.IsValid){var clip=sound.Gunshot;PooledAudioService.Resolve().PlayOneShot(clip.clip,RetreatPoint,.7f*clip.EffectiveGain,1,0,.85f,clip.startTime,clip.duration);}
                }
                if(sceneClock>sacrificeTravel+13&&tankSmoke!=null&&!tankSmoke.isPlaying)tankSmoke.Play();
            }
            if(sceneClock>=sacrificeTravel&&Layout!=null&&Layout.hungPlantRetreatClip!=null&&hung.visual!=null)
            {var animator=hung.visual.GetComponentInChildren<Animator>();if(animator!=null){hungActionPose??=new Map01CinematicPose(animator,null,false);hungActionPose.Sample(Layout.hungPlantRetreatClip,sceneClock-sacrificeTravel,false);}}
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
