using System.Collections.Generic;
using System.Linq;
using ShadowVale.Gameplay.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace ShadowVale.Map01
{
    /// <summary>
    /// Night 2's failure when Nam is caught on an evacuation route: the enemy subduing and tying him (the wrist
    /// ropes), then the failure screen.
    /// </summary>
    public sealed partial class Map02ChapterDirector
    {
        public const float EvacuationCaptureCameraSeconds=3,EvacuationCapturePinSeconds=2,EvacuationCaptureTieSeconds=3;
        public bool EvacuationCaptureActive=>evacuationCaptureClock>=0;
        float evacuationCaptureClock=-1,evacuationCapturePinAt=-1;
        Vector3 evacuationCaptureNam,evacuationCaptureCamera,evacuationCaptureRear,evacuationCaptureForward;
        Quaternion evacuationCaptureNamRotation,evacuationCaptureCameraRotation;
        float evacuationCaptureFov;
        bool evacuationCaptureProtected;
        Map02ChapterEnemy[] evacuationCaptors;
        Vector3[] evacuationCaptorTargets;
        Map01CinematicPose evacuationNamPose;
        Map01CinematicPose[] evacuationCaptorPoses;
        Animator evacuationNamAnimator;
        AnimationClip evacuationNamIdle,evacuationTieClip;
        NamProneAnimationData.Take evacuationProneTake;
        Transform[] evacuationProneBones;
        readonly List<GameObject> evacuationCaptureWeapons=new List<GameObject>();
        readonly List<Map02PatrolFlashlight> evacuationCaptureTorches=new List<Map02PatrolFlashlight>();
        GameObject evacuationRopes;
        LineRenderer[] evacuationWristRopes;
        Material evacuationRopeMaterial;

        void BeginEvacuationCapture()
        {
            if(EvacuationCaptureActive||Mission.World.story.failed)return;
            evacuationCaptureClock=0;evacuationCapturePinAt=-1;
            evacuationCaptureNam=Mission.Player.position;evacuationCaptureNamRotation=Mission.Player.rotation;
            evacuationCaptureCamera=rig.transform.position;evacuationCaptureCameraRotation=rig.transform.rotation;
            evacuationCaptureFov=rig.GetComponent<Camera>().fieldOfView;
            evacuationCaptors=Enemies.Where(e=>e!=null&&e.group==EvacuationPatrolGroup&&!e.Dead&&e.gameObject.activeInHierarchy)
                .OrderBy(e=>Vector3.Distance(e.transform.position,evacuationCaptureNam)).Take(2).ToArray();
            var group=evacuationCaptors.Length>0?evacuationCaptors.Aggregate(Vector3.zero,(p,e)=>p+e.transform.position)/evacuationCaptors.Length:evacuationCaptureNam+Mission.Player.forward*8;
            evacuationCaptureForward=Vector3.ProjectOnPlane(evacuationCaptureNam-group,Vector3.up).normalized;
            if(evacuationCaptureForward.sqrMagnitude<.1f)evacuationCaptureForward=Mission.Player.forward;
            evacuationCaptureRear=group-evacuationCaptureForward*3+Vector3.up*2;
            var side=Vector3.Cross(Vector3.up,evacuationCaptureForward);
            evacuationCaptorTargets=new Vector3[evacuationCaptors.Length];
            evacuationCaptorPoses=new Map01CinematicPose[evacuationCaptors.Length];
            for(int i=0;i<evacuationCaptors.Length;i++)
            {
                var target=evacuationCaptureNam+side*(i==0?.75f:-.75f)-evacuationCaptureForward*.2f;
                evacuationCaptorTargets[i]=NavMesh.SamplePosition(target,out var floor,1.5f,NavMesh.AllAreas)?floor.position:evacuationCaptors[i].transform.position;
            }
            evacuationCaptureProtected=health.CinematicInvulnerable;health.CinematicInvulnerable=true;
            SetBinoculars(false);player.RestoreMotion(false);combat.SetHandsOccupied(true);
            CancelNightRadio();DropBarks();voice?.Stop();Mission.RequireInteractionRelease();
            foreach(var unit in Enemies.Where(e=>e!=null&&e.group==EvacuationPatrolGroup&&!e.Dead))
            {
                unit.StopAtStoreArrival();var torch=unit.GetComponent<Map02PatrolFlashlight>();
                if(torch!=null&&torch.enabled){torch.enabled=false;if(torch.Emitter!=null)torch.Emitter.parent.gameObject.SetActive(false);evacuationCaptureTorches.Add(torch);}
            }
            foreach(var light in evacuationLights)if(light!=null)light.enabled=false;
            foreach(var beam in evacuationLightBeams)if(beam!=null)beam.SetActive(false);
            evacuationNamAnimator=Mission.Player.GetComponentInChildren<Animator>();
            var clips=evacuationNamAnimator!=null&&evacuationNamAnimator.runtimeAnimatorController!=null?evacuationNamAnimator.runtimeAnimatorController.animationClips:new AnimationClip[0];
            evacuationNamIdle=clips.FirstOrDefault(c=>c.name=="Idle_Tuned")??clips.FirstOrDefault();
            evacuationTieClip=clips.FirstOrDefault(c=>c.name.ToLowerInvariant().Contains("untie"));
            if(evacuationNamAnimator!=null)
            {
                evacuationNamPose=new Map01CinematicPose(evacuationNamAnimator,skinGrounding:false);
                evacuationProneTake=Resources.Load<NamProneAnimationData>("Animations/NamProne")?.Find("unarmed");
                if(evacuationProneTake!=null)
                {
                    var nodes=evacuationNamAnimator.GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
                    evacuationProneBones=evacuationProneTake.bones.Select(b=>nodes.TryGetValue(b.name,out var node)?node:null).ToArray();
                }
            }
            foreach(var weapon in Mission.Player.GetComponentsInChildren<Weapon>(true))if(weapon.gameObject.activeSelf){weapon.gameObject.SetActive(false);evacuationCaptureWeapons.Add(weapon.gameObject);}
            rig.SetCinematicView(evacuationCaptureCamera,evacuationCaptureCameraRotation,evacuationCaptureFov,1);
        }
        public void ControlEvacuationCaptureUnit(Map02ChapterEnemy unit,NavMeshAgent agent)
        {
            if(agent==null||!agent.enabled||!agent.isOnNavMesh)return;
            int index=System.Array.IndexOf(evacuationCaptors,unit);
            if(Mission.Paused||ForestMenu.Visible||evacuationCaptureClock<EvacuationCaptureCameraSeconds||index<0||evacuationCapturePinAt>=0)
            {agent.ResetPath();agent.isStopped=true;return;}
            agent.speed=1.8f;agent.stoppingDistance=.15f;
            if(Vector3.Distance(unit.transform.position,evacuationCaptorTargets[index])>.3f)
            {agent.isStopped=false;if(!agent.pathPending&&(!agent.hasPath||(agent.destination-evacuationCaptorTargets[index]).sqrMagnitude>.01f))agent.SetDestination(evacuationCaptorTargets[index]);}
            else{agent.ResetPath();agent.isStopped=true;}
        }
        void TickEvacuationCapture(float dt)
        {
            if(Mission.World.story.failed)return;
            evacuationCaptureClock+=dt;
            foreach(var unit in Enemies.Where(e=>e!=null&&e.group==EvacuationPatrolGroup&&!e.Dead))ControlEvacuationCaptureUnit(unit,unit.GetComponent<NavMeshAgent>());
            if(evacuationCapturePinAt<0&&evacuationCaptureClock>=EvacuationCaptureCameraSeconds&&evacuationCaptors.Length>0&&
                evacuationCaptors.Select((e,i)=>Vector3.Distance(e.transform.position,evacuationCaptorTargets[i])<.4f).All(v=>v))
            {
                evacuationCapturePinAt=evacuationCaptureClock;
                for(int i=0;i<evacuationCaptors.Length;i++)
                {
                    var actor=evacuationCaptors[i].GetComponentInChildren<Animator>();
                    if(actor!=null)evacuationCaptorPoses[i]=new Map01CinematicPose(actor,skinGrounding:false);
                    foreach(var weapon in evacuationCaptors[i].GetComponentsInChildren<Weapon>(true))if(weapon.gameObject.activeSelf){weapon.gameObject.SetActive(false);evacuationCaptureWeapons.Add(weapon.gameObject);}
                }
                CreateEvacuationRopes();
            }
            if(evacuationCapturePinAt>=0&&evacuationCaptureClock-evacuationCapturePinAt>=EvacuationCapturePinSeconds+EvacuationCaptureTieSeconds)
                Mission.World.story.Fail("Nam đã bị địch khống chế và trói. Nhiệm vụ thất bại.");
            else if(evacuationCapturePinAt<0&&evacuationCaptureClock>25)
                Mission.World.story.Fail("Nam đã bị địch phát hiện. Không thể tiếp tục nhiệm vụ.");
        }
        void PresentEvacuationCapture()
        {
            Mission.Player.SetPositionAndRotation(evacuationCaptureNam,evacuationCaptureNamRotation);
            float travel=Mathf.SmoothStep(0,1,evacuationCaptureClock/EvacuationCaptureCameraSeconds);
            var rearRotation=Quaternion.LookRotation(evacuationCaptureNam+Vector3.up-evacuationCaptureRear);
            if(evacuationCaptureClock<EvacuationCaptureCameraSeconds)
            {
                var eye=Vector3.Lerp(evacuationCaptureCamera,evacuationCaptureRear,travel)+Vector3.up*(Mathf.Sin(travel*Mathf.PI)*.5f);
                rig.SetCinematicView(eye,Quaternion.Slerp(evacuationCaptureCameraRotation,rearRotation,travel),Mathf.Lerp(evacuationCaptureFov,50,travel),1);
            }
            else
            {
                var side=Vector3.Cross(Vector3.up,evacuationCaptureForward);
                var eye=evacuationCaptureNam-evacuationCaptureForward*2.7f+side*2.2f+Vector3.up*2.6f;
                float blend=Mathf.SmoothStep(0,1,(evacuationCaptureClock-EvacuationCaptureCameraSeconds)/3);
                var focus=evacuationCaptureNam+Vector3.up*(evacuationCapturePinAt<0?1:.45f);
                var position=Vector3.Lerp(evacuationCaptureRear,eye,blend);
                rig.SetCinematicView(position,Quaternion.Slerp(rearRotation,Quaternion.LookRotation(focus-position),blend),50,1);
            }
            evacuationNamPose?.Sample(evacuationNamIdle,0);
            if(evacuationCapturePinAt<0)return;
            float elapsed=evacuationCaptureClock-evacuationCapturePinAt;
            float pin=Mathf.SmoothStep(0,1,elapsed/EvacuationCapturePinSeconds);
            if(evacuationProneTake!=null&&evacuationProneBones!=null)
                for(int i=0;i<evacuationProneBones.Length;i++)if(evacuationProneBones[i]!=null)evacuationProneTake.bones[i].At(1).Apply(evacuationProneBones[i],pin);
            if(evacuationNamAnimator==null||!evacuationNamAnimator.isHuman)return;
            var hips=evacuationNamAnimator.GetBoneTransform(HumanBodyBones.Hips);
            var sideDirection=Mission.Player.right;var wristCenter=hips.position+Vector3.up*.22f-Mission.Player.forward*.1f;
            float tied=Mathf.SmoothStep(0,1,(elapsed-.6f)/1.4f);
            Map01ArmContact.Reach(evacuationNamAnimator,true,wristCenter-sideDirection*.07f,hips.position-sideDirection*.4f,tied);
            Map01ArmContact.Reach(evacuationNamAnimator,false,wristCenter+sideDirection*.07f,hips.position+sideDirection*.4f,tied);
            for(int i=0;i<evacuationCaptors.Length;i++)
            {
                var actor=evacuationCaptors[i].GetComponentInChildren<Animator>();if(actor==null||!actor.isHuman)continue;
                evacuationCaptors[i].transform.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(evacuationCaptureNam-evacuationCaptors[i].transform.position,Vector3.up));
                evacuationCaptorPoses[i]?.Sample(evacuationTieClip!=null?evacuationTieClip:evacuationNamIdle,Mathf.Min(elapsed,2),false);
                var handTarget=i==0?evacuationNamAnimator.GetBoneTransform(HumanBodyBones.Chest).position+Vector3.up*.12f:wristCenter;
                Map01ArmContact.Reach(actor,true,handTarget-sideDirection*.08f,actor.transform.position-actor.transform.right*.5f+Vector3.up*.8f,pin);
                Map01ArmContact.Reach(actor,false,handTarget+sideDirection*.08f,actor.transform.position+actor.transform.right*.5f+Vector3.up*.8f,pin);
            }
            if(evacuationRopes!=null)evacuationRopes.SetActive(elapsed>=EvacuationCapturePinSeconds);
            if(evacuationWristRopes!=null)
                for(int i=0;i<2;i++)
                {
                    var hand=evacuationNamAnimator.GetBoneTransform(i==0?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
                    for(int j=0;j<25;j++){float angle=j*Mathf.PI*2/24;evacuationWristRopes[i].SetPosition(j,hand.position+hand.up*(Mathf.Cos(angle)*.045f)+hand.right*(Mathf.Sin(angle)*.045f));}
                }
        }
        void CreateEvacuationRopes()
        {
            evacuationRopes=new GameObject("Dây trói cổ tay Nam");evacuationRopes.transform.SetParent(root.transform);evacuationRopes.SetActive(false);
            evacuationRopeMaterial=new Material(Map01Fx.Lit){color=new Color(.38f,.27f,.12f)};evacuationWristRopes=new LineRenderer[2];
            for(int i=0;i<2;i++)
            {
                var go=new GameObject("Vòng dây cổ tay "+i);go.transform.SetParent(evacuationRopes.transform);
                var line=go.AddComponent<LineRenderer>();line.useWorldSpace=true;line.positionCount=25;line.startWidth=line.endWidth=.018f;line.sharedMaterial=evacuationRopeMaterial;evacuationWristRopes[i]=line;
            }
        }
        void DrawEvacuationCapture()
        {
            var color=GUI.color;GUI.color=Color.black;GUI.DrawTexture(new Rect(0,0,1600,70),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(0,830,1600,70),Texture2D.whiteTexture);GUI.color=color;
            if(!Mission.World.story.failed)GUI.Label(new Rect(400,835,800,50),evacuationCapturePinAt<0?"Địch đã phát hiện Nam…":"Nam bị khống chế…",new GUIStyle(GUI.skin.label){fontSize=23,alignment=TextAnchor.MiddleCenter,normal={textColor=Color.white}});
        }
        void CancelEvacuationCapture()
        {
            if(!EvacuationCaptureActive)return;
            evacuationNamPose?.Dispose();evacuationNamPose=null;
            if(evacuationCaptorPoses!=null)foreach(var pose in evacuationCaptorPoses)pose?.Dispose();
            foreach(var weapon in evacuationCaptureWeapons)if(weapon!=null)weapon.SetActive(true);evacuationCaptureWeapons.Clear();
            foreach(var torch in evacuationCaptureTorches)if(torch!=null){torch.enabled=true;if(torch.Emitter!=null)torch.Emitter.parent.gameObject.SetActive(true);}evacuationCaptureTorches.Clear();
            if(evacuationRopes!=null)Destroy(evacuationRopes);if(evacuationRopeMaterial!=null)Destroy(evacuationRopeMaterial);
            if(health!=null)health.CinematicInvulnerable=evacuationCaptureProtected;
            combat?.SetHandsOccupied(false);combat?.RestoreAfterCinematic();rig?.ClearCinematicView();
            evacuationCaptureClock=evacuationCapturePinAt=-1;evacuationProneBones=null;evacuationProneTake=null;
            Mission?.RequireInteractionRelease();ForestMenu.SuppressKeysAfterCutscene();
        }
    }
}
