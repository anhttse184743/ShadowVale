using System;
using System.Linq;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Player;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

namespace ShadowVale.Map01
{
    /// <summary>Short kill shot, hostage execution, and the physical return into the existing shelter.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class Map01RescueCinematic : MonoBehaviour
    {
        public enum Mode { None, Takedown, Execution, Shelter, Intro }
        public Mode CurrentMode { get; private set; }
        public float Elapsed { get; private set; }
        public bool IsPlaying => CurrentMode != Mode.None;
        private Map01Mission mission;
        private Map01Rescue rescue;
        private Map01EnemyController enemy;
        private ThirdPersonCamera rig;
        private Vector3 cameraStart, killPosition;
        private Vector3 shelterCameraPosition, cameraVelocity;
        private bool shelterRoomShot;
        private Quaternion cameraRotation;
        private float cameraFov, duration, skipHeld;
        private bool clearShot, shot, playerEnabled, combatEnabled, characterEnabled, hungVisualEnabled, invulnerable;
        private Vector3[] namPath, hungPath;
        private Vector3[] hungYieldPath;
        private float namDistance,hungDistance,yieldDistance,namCovered,hungCovered,namWeight,hungWeight,namInsideDistance;
        private float namCycleSpeed;
        private NavMeshAgent hungAgent;
        private bool hungUpdatesPosition,hungUpdatesRotation;
        private PosePlayer namPose, hungPose;
        private Map01HungVisual hungVisual;
        private bool hungFirst;
        private DialogueVoice storyVoice;
        private Map01CinematicPose captivePose,interrogatorPose;
        private Map01HungVisual captiveVisual;
        private int storyBeat;
        private float beatAt,impactAt;
        private bool storyPlayerEnabled,storyCombatEnabled,storyActionsEnabled,storyCCEnabled,storySneaking;
        private Map01NamActions storyActions;
        private Vector3 interrogatorStart,interrogatorClose;
        private AnimationClip interrogatorWalk, executionAim,boundExecution;
        private bool canApproach;
        private AudioSource tensionSource;
        private AudioClip heartbeat;
        private float nextHeartbeat;
        private Vector3 alarmRetreat;
        private AnimationClip alarmWalk,alarmCrouch;
        private bool alarmReverseWalk;
        private Vector3 executionView,executionFocus,reactionView;
        const float CollapseViewSeconds=3.3f,ReactionViewSeconds=1.7f,FailureFadeSeconds=.8f;
        public bool ExecutionShowsCaptive=>CurrentMode==Mode.Execution&&storyBeat>=2&&(!shot||Elapsed-impactAt<CollapseViewSeconds);
        public bool ExecutionShowsReaction=>CurrentMode==Mode.Execution&&shot&&Elapsed-impactAt>=CollapseViewSeconds;
        public float ExecutionTension=>CurrentMode==Mode.Execution?Mathf.Clamp01(Elapsed/3):0;
        private Map01CinematicPose introNamPose;
        private AnimationClip introNamIdle;
        private Vector3 introNamPosition;
        private Quaternion introNamRotation;
        private float introRadioAt,introReleaseAt;
        private (Transform bone,Vector3 position,Quaternion rotation)[] introLowerBody;
        private readonly System.Collections.Generic.List<GameObject> introHiddenWeapons=new();
        private Vector3 introCaptiveView,introCaptiveFocus;
        const float IntroCutIn=.65f,IntroCutOut=4.5f;
        public bool IntroShowsCaptive=>CurrentMode==Mode.Intro&&Elapsed>=IntroCutIn&&Elapsed<IntroCutOut;

        public bool ControlsEnemy(Map01EnemyController guard) => (CurrentMode == Mode.Execution || CurrentMode==Mode.Intro) && guard == enemy;

        private void Begin(Map01Rescue owner, Mode mode, float seconds)
        {
            rescue = owner; mission = owner.GetComponent<Map01Mission>();
            rig = mission.gameCamera.GetComponent<ThirdPersonCamera>();
            cameraStart = mission.gameCamera.transform.position; cameraRotation = mission.gameCamera.transform.rotation;
            cameraFov = mission.gameCamera.fieldOfView;
            shelterCameraPosition=cameraStart;cameraVelocity=Vector3.zero;shelterRoomShot=false;
            CurrentMode = mode; Elapsed = skipHeld = 0; duration = seconds;
            mission.CloseGameplayPanel(); mission.GetComponent<Map01StoneThrow>()?.Cancel();
            mission.GetComponent<Map01Scouting>()?.HoldBinoculars(false);
            rig.SetBinoculars(false, 1.6f);
            mission.Cinematic = true;
            foreach(var guard in mission.Enemies)if(guard!=null)guard.PauseForCinematic(true);
            invulnerable = mission.ModernHealth.CinematicInvulnerable;
            mission.ModernHealth.CinematicInvulnerable = true;
        }
        public void ShowTakedown(Map01Rescue owner, Map01EnemyController victim)
        {
            if (IsPlaying || owner == null) return;
            enemy = victim; Begin(owner, Mode.Takedown, Map01NamActions.TakedownSeconds);
            var focus = mission.player.position + Vector3.up * 1.2f + victim.transform.forward * .4f;
            clearShot = false;
            foreach (float side in new[] { 1f, -1f })
            {
                var p = focus + victim.transform.right * side * 2.1f - victim.transform.forward * .4f + Vector3.up * .3f;
                if (CameraClear(focus, p)) { killPosition = p; clearShot = true; break; }
            }
        }
        DialogueVoice Voice(){
            if(storyVoice==null){var host=new GameObject("Rescue cinematic dialogue");host.transform.SetParent(transform,false);storyVoice=DialogueVoice.For(host);}
            storyVoice.CanSpeak=()=>IsPlaying&&!mission.Paused&&!ForestMenu.Visible;return storyVoice;
        }
        void OwnStoryActors(){
            DialogueVoice.StopAll();storyBeat=0;beatAt=impactAt=0;shot=false;
            storyPlayerEnabled=mission.ModernPlayer.enabled;storyCombatEnabled=mission.ModernCombat.enabled;storySneaking=mission.Crouched;
            var cc=mission.player.GetComponent<CharacterController>();storyCCEnabled=cc.enabled;cc.enabled=false;
            storyActions=Map01NamActions.For(mission);storyActionsEnabled=storyActions.enabled;storyActions.enabled=false;
            mission.ModernPlayer.enabled=mission.ModernCombat.enabled=false;
            captiveVisual=mission.hung.GetComponentInChildren<Map01HungVisual>();captiveVisual.BeginCaptivePresentation();
            var guardActor=enemy.GetComponentInChildren<Animator>();
            executionAim=guardActor.runtimeAnimatorController?.animationClips.FirstOrDefault(c=>c.name=="Enemy_Rifle_AimIdle");
            boundExecution=CurrentMode==Mode.Execution?Map01CinematicPose.Clip("Hung_Bound_Execution"):null;
            captivePose=new Map01CinematicPose(captiveVisual.actor);interrogatorPose=new Map01CinematicPose(guardActor);
            if(CurrentMode==Mode.Intro || CurrentMode==Mode.Execution){
                introNamPosition=mission.player.position;introNamRotation=mission.player.rotation;
                introRadioAt=introReleaseAt=0;
                var actor=mission.player.GetComponentInChildren<Animator>();
                introNamIdle=ownerIdle();
                introNamPose=new Map01CinematicPose(actor);
                introNamPose.Sample(introNamIdle,0,true);
                var lower=new[]{HumanBodyBones.Hips,HumanBodyBones.LeftUpperLeg,HumanBodyBones.LeftLowerLeg,HumanBodyBones.LeftFoot,HumanBodyBones.LeftToes,
                    HumanBodyBones.RightUpperLeg,HumanBodyBones.RightLowerLeg,HumanBodyBones.RightFoot,HumanBodyBones.RightToes};
                introLowerBody=lower.Select(actor.GetBoneTransform).Where(t=>t!=null).Select(t=>(t,t.localPosition,t.localRotation)).ToArray();
                foreach(var weapon in mission.player.GetComponentsInChildren<Weapon>(true))if(CurrentMode==Mode.Intro&&weapon.gameObject.activeSelf){introHiddenWeapons.Add(weapon.gameObject);weapon.gameObject.SetActive(false);}
            }
            RenderStory();
        }
        AnimationClip ownerIdle()=>rescue.Layout.namIdle??Resources.Load<AnimationClip>("Rescue/Nam_Shelter_Idle");
        public bool ShowIntro(Map01Rescue owner,Map01EnemyController overseer){
            if(IsPlaying||Map01CinematicPose.Clip("Hung_Captive_Recoil")==null||Map01CinematicPose.Clip("Enemy_Interrogate_Strike")==null)return false;
            enemy=overseer;Begin(owner,Mode.Intro,Mathf.Max(8,DialogueVoice.Length("m1_v2_rescue_approach_01")+DialogueVoice.Length("m1_v2_rescue_approach_02")+DialogueVoice.Length("m1_v2_rescue_approach_03")+1.8f));
            interrogatorStart=enemy.transform.position;
            var direction=Vector3.ProjectOnPlane(interrogatorStart-mission.hung.position,Vector3.up).normalized;
            var desired=mission.hung.position+direction*1.05f;
            canApproach=NavMesh.SamplePosition(desired,out var contact,.4f,NavMesh.AllAreas)&&!NavMesh.Raycast(interrogatorStart,contact.position,out _,NavMesh.AllAreas);
            interrogatorClose=canApproach?contact.position:interrogatorStart;
            interrogatorWalk=Map01CinematicPose.Clip("Enemy_Interrogate_Walk");
            OwnStoryActors();
            var hungChest=captiveVisual.actor.GetBoneTransform(HumanBodyBones.Chest).position;
            var guardChest=enemy.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Chest).position;
            introCaptiveFocus=Vector3.Lerp(hungChest,guardChest,.5f);
            var side=Vector3.Cross(Vector3.up,direction);
            float distance=Mathf.Clamp(3+Vector3.Distance(hungChest,guardChest)*.5f,3.6f,7);
            introCaptiveView=FindStoryLens(introCaptiveFocus,side,distance*1.45f,.55f);
            bool found=false;
            foreach(float scale in new[]{1f,1.2f,1.45f}){
                foreach(float turn in new[]{0f,25f,-25f,50f,-50f,180f,90f,-90f}){
                    var candidate=introCaptiveFocus+Quaternion.Euler(0,turn,0)*side*distance*scale+Vector3.up*.55f;
                    if(IntroFrameContains(candidate,hungChest)&&IntroFrameContains(candidate,guardChest)
                        &&ShelterCameraClear(hungChest,candidate)&&ShelterCameraClear(guardChest,candidate)
                        &&!Physics.CheckSphere(candidate,.16f,mission.ObstructionMask,QueryTriggerInteraction.Ignore)){
                        introCaptiveView=candidate;found=true;break;
                    }
                }
                if(found)break;
            }
            owner.MarkIntroSeen();return true;
        }
        bool IntroFrameContains(Vector3 lens,Vector3 point){
            var rotation=Quaternion.LookRotation(introCaptiveFocus-lens);
            var local=Quaternion.Inverse(rotation)*(point-lens);
            if(local.z<.1f)return false;
            float halfHeight=local.z*Mathf.Tan(23*Mathf.Deg2Rad);
            return Mathf.Abs(local.x)<halfHeight*mission.gameCamera.aspect*.72f&&Mathf.Abs(local.y)<halfHeight*.72f;
        }
        public void ExecuteHostage(Map01Rescue owner,Map01EnemyController overseer,Map01EnemyController reporter=null){
            if(IsPlaying)return;
            enemy=overseer;Begin(owner,Mode.Execution,20);
            // The cinematic pose owns and detaches the controller; collect its
            // locomotion clips before that handoff, never from the detached actor.
            var actor=mission.player.GetComponentInChildren<Animator>();var clips=actor.runtimeAnimatorController!=null?actor.runtimeAnimatorController.animationClips:Array.Empty<AnimationClip>();
            OwnStoryActors();storyBeat=1;nextHeartbeat=0;
            bool knife=mission.ModernCombat.EquippedKind==WeaponKind.Knife;
            alarmCrouch=knife?Map01CinematicPose.Clip("Nam_Bush_Recovery"):clips.FirstOrDefault(c=>c.name=="Nam_Crouch_Idle");
            alarmCrouch??=introNamIdle;
            alarmWalk=clips.FirstOrDefault(c=>c.name.Contains(storySneaking?"Crouch_Walk_B":"Rifle_Walk_B"));alarmReverseWalk=false;
            if(alarmWalk==null&&storySneaking){alarmWalk=clips.FirstOrDefault(c=>c.name=="Nam_Crouch_Walk_F");alarmReverseWalk=alarmWalk!=null;}
            alarmWalk??=clips.FirstOrDefault(c=>c.name.Contains("Walk_B"))??introNamIdle;
            alarmRetreat=introNamPosition;
            foreach(float side in new[]{0f,.55f,-.55f}){
                var direction=(-mission.player.forward+mission.player.right*side).normalized;
                var desired=introNamPosition+direction*.75f;
                if(NavMesh.SamplePosition(introNamPosition,out var from,.4f,NavMesh.AllAreas)&&NavMesh.SamplePosition(desired,out var to,.25f,NavMesh.AllAreas)
                    &&!NavMesh.Raycast(from.position,to.position,out _,NavMesh.AllAreas)){
                    var delta=to.position-from.position;
                    bool blocked=Physics.CapsuleCastAll(from.position+Vector3.up*.45f,from.position+Vector3.up*1.3f,.22f,delta.normalized,delta.magnitude,mission.ObstructionMask,QueryTriggerInteraction.Ignore)
                        .Any(h=>!h.transform.IsChildOf(mission.player)&&h.transform.GetComponentInParent<Health>()==null);
                    if(!blocked){alarmRetreat=Map01OperationSession.GroundPoint(to.position,mission.player);break;}
                }
            }
            Voice().Play(reporter!=null&&reporter!=overseer&&reporter.Alive?"m1_rescue_alarm":"m1_rescue_execute_order");
            overseer.FaceImmediate(mission.hung.position);
            var axis=Vector3.ProjectOnPlane(enemy.transform.position-mission.hung.position,Vector3.up).normalized;
            if(axis.sqrMagnitude<.01f)axis=enemy.transform.forward;
            var lensSide=Vector3.Cross(Vector3.up,axis);
            executionFocus=Vector3.Lerp(mission.hung.position,enemy.transform.position,.35f)+Vector3.up*.82f;
            if(Vector3.Dot(lensSide,cameraStart-executionFocus)<0)lensSide=-lensSide;
            executionView=FindStoryLens(executionFocus,lensSide,3.1f,.65f);
            var namFocus=introNamPosition+Vector3.up*1.1f;
            if(!ShelterCameraClear(namFocus,cameraStart)||Physics.CheckSphere(cameraStart,.16f,mission.ObstructionMask,QueryTriggerInteraction.Ignore)){
                cameraStart=FindStoryLens(namFocus,-mission.player.forward+mission.player.right*.5f,2.5f,.6f);
                cameraRotation=Quaternion.LookRotation(namFocus-cameraStart);
            }
            reactionView=Vector3.zero; // resolved after Nam's retreat, at the cut
        }
        void UpdateStory(){
            if(CurrentMode==Mode.Intro){
                if(storyBeat==0&&Elapsed>=.7f){introRadioAt=Elapsed;Voice().Play("m1_v2_rescue_approach_01");storyBeat=2;}
                else if(storyBeat==2&&!Voice().HasSpeech){Voice().Play("m1_v2_rescue_approach_02");storyBeat=3;}
                else if(storyBeat==3&&!Voice().HasSpeech){Voice().Play("m1_v2_rescue_approach_03");storyBeat=4;}
                else if(storyBeat==4&&!Voice().HasSpeech){introReleaseAt=Elapsed;storyBeat=5;}
                else if(storyBeat==5&&Elapsed>=Mathf.Max(duration,introReleaseAt+.8f))Finish();
                return;
            }
            UpdateExecutionSound();
            if(storyBeat==1&&!Voice().HasSpeech&&Elapsed>=1.4f){storyBeat=2;beatAt=Elapsed;}
            else if(storyBeat==2&&Elapsed-beatAt>=Mathf.Max(1.6f,rescue.Layout.executionAimSeconds)){
                shot=true;impactAt=Elapsed;storyBeat=3;
                enemy.FireAtHostage(captiveVisual.actor.GetBoneTransform(HumanBodyBones.Chest).position,0,true);
                rescue.CompleteExecution();
            }else if(storyBeat==3&&Elapsed-impactAt>=CollapseViewSeconds+ReactionViewSeconds+FailureFadeSeconds)Finish();
        }
        void RenderStory(){
            RenderIntroNam();
            bool walking=false;float travel=0;
            enemy.FaceImmediate(mission.hung.position);
            float captiveTime=CurrentMode==Mode.Execution?(shot?Elapsed-impactAt:0):Mathf.Clamp(Elapsed-2,0,3);
            // Both sides of the shot share the same bound pose and retarget
            // bindings. The recoil clip's rest wrists can sit inside the torso;
            // switching from it at impact made the hands suddenly reappear.
            captivePose.Sample(CurrentMode==Mode.Execution?boundExecution:Map01CinematicPose.Clip("Hung_Captive_Recoil"),captiveTime);
            if(CurrentMode==Mode.Intro&&executionAim!=null)interrogatorPose.Sample(executionAim,Elapsed,true);
            else interrogatorPose.Sample(walking&&interrogatorWalk!=null?interrogatorWalk:Map01CinematicPose.Clip("Enemy_Interrogate_Strike"),walking?travel*.65f:0,walking);
            captivePose.Ground(mission.hung.position.y);interrogatorPose.Ground(enemy.transform.position.y);captiveVisual.SampleCaptiveContacts();
            if(CurrentMode==Mode.Intro||CurrentMode==Mode.Execution&&storyBeat>=2){
                if(executionAim!=null)interrogatorPose.Sample(executionAim,Elapsed-beatAt,true);
                AimExecution();
            }
            // Establish Nam first, then cut to the real bound captive and back.
            // Never fly the camera across the forest or move either actor to a stage.
            if(IntroShowsCaptive){
                float push=.12f*Mathf.SmoothStep(0,1,(Elapsed-IntroCutIn)/(IntroCutOut-IntroCutIn));
                var lens=Vector3.MoveTowards(introCaptiveView,introCaptiveFocus,push);
                SetStoryView(lens,Quaternion.LookRotation(introCaptiveFocus-lens),46);return;
            }
            if(ExecutionShowsCaptive){
                float push=Mathf.SmoothStep(0,1,(Elapsed-beatAt)/4.5f)*.18f;
                var lens=Vector3.MoveTowards(executionView,executionFocus,push);
                SetStoryView(lens,Quaternion.LookRotation(executionFocus-lens),44);return;
            }
            if(ExecutionShowsReaction){
                var target=mission.player.position+Vector3.up*1.03f;
                if(reactionView==Vector3.zero)reactionView=FindStoryLens(target,mission.player.forward+mission.player.right*.5f,2.05f,.45f);
                SetStoryView(reactionView,Quaternion.LookRotation(target-reactionView),42);return;
            }
            float tension=ExecutionTension;
            float recoil=CurrentMode==Mode.Execution&&shot?Mathf.Exp(-(Elapsed-impactAt)*18):0;
            var sway=Quaternion.Euler(tension*Mathf.Sin(Elapsed*4.3f)*.08f-recoil*.45f,tension*Mathf.Sin(Elapsed*3.1f)*.07f,0);
            var follow=CurrentMode==Mode.Execution?mission.player.position-introNamPosition:Vector3.zero;
            SetStoryView(cameraStart+follow,cameraRotation*sway,cameraFov-5*tension);
        }
        void SetStoryView(Vector3 position,Quaternion rotation,float fov){
            rig.SetCinematicView(position,rotation,fov,1);
            // This director runs after the gameplay camera's LateUpdate. Apply
            // the cut now as well, so a gunshot cannot precede its camera cut.
            mission.gameCamera.transform.SetPositionAndRotation(position,rotation);mission.gameCamera.fieldOfView=fov;
        }
        Vector3 FindStoryLens(Vector3 focus,Vector3 preferred,float distance,float height){
            preferred=Vector3.ProjectOnPlane(preferred,Vector3.up).normalized;
            foreach(float turn in new[]{0f,30f,-30f,60f,-60f,90f,-90f,180f}){
                var candidate=focus+Quaternion.Euler(0,turn,0)*preferred*distance+Vector3.up*height;
                if(ShelterCameraClear(focus,candidate)&&!Physics.CheckSphere(candidate,.16f,mission.ObstructionMask,QueryTriggerInteraction.Ignore))return candidate;
            }
            return ClearStoryView(focus,distance);
        }
        void RenderIntroNam(){
            if(introNamPose==null||introNamIdle==null)return;
            float step=CurrentMode==Mode.Execution?Mathf.SmoothStep(0,1,(Elapsed-.2f)/.9f):0;
            mission.player.SetPositionAndRotation(Vector3.Lerp(introNamPosition,alarmRetreat,step),introNamRotation);
            float idleTime=Mathf.Repeat(Elapsed,Mathf.Max(.01f,introNamIdle.length));
            if(CurrentMode==Mode.Execution&&Elapsed>.2f){
                float walkTime=Mathf.Repeat(Vector3.Distance(introNamPosition,mission.player.position)/1.4f,Mathf.Max(.01f,alarmWalk.length));
                if(alarmReverseWalk)walkTime=alarmWalk.length-walkTime;
                introNamPose.SampleBlend(alarmWalk,walkTime,alarmCrouch,Mathf.Max(0,Elapsed-1.1f),Mathf.SmoothStep(0,1,(Elapsed-.85f)/.65f));
            }else introNamPose.Sample(introNamIdle,idleTime,true);
            if(CurrentMode==Mode.Intro&&storyBeat>=2){
                float weight=storyBeat==5?1-Mathf.SmoothStep(0,1,(Elapsed-introReleaseAt)/.8f):Mathf.SmoothStep(0,1,(Elapsed-introRadioAt)/.7f);
                Map01ArmContact.Radio(mission.player.GetComponentInChildren<Animator>(),weight);
            }
            if(CurrentMode==Mode.Execution){
                // A restrained look toward the alarm, distributed through the
                // upper body. Hands, hips and planted feet keep the authored pose.
                var actor=mission.player.GetComponentInChildren<Animator>();
                if(shot){
                    float shock=Mathf.Exp(-Mathf.Max(0,Elapsed-impactAt)*7);
                    var spine=actor.GetBoneTransform(HumanBodyBones.Spine);if(spine!=null)spine.rotation=Quaternion.AngleAxis(5*shock,mission.player.right)*spine.rotation;
                }
                var direction=Vector3.ProjectOnPlane(mission.hung.position-mission.player.position,Vector3.up);
                if(direction.sqrMagnitude>.01f){
                    var turn=Quaternion.RotateTowards(Quaternion.identity,Quaternion.FromToRotation(mission.player.forward,direction.normalized),30);
                    float reaction=Mathf.SmoothStep(0,1,Elapsed/.9f);
                    foreach(var part in new[]{(HumanBodyBones.Chest,.15f),(HumanBodyBones.Neck,.35f),(HumanBodyBones.Head,.5f)}){
                        var bone=actor.GetBoneTransform(part.Item1);if(bone!=null)bone.rotation=Quaternion.Slerp(Quaternion.identity,turn,reaction*part.Item2)*bone.rotation;
                    }
                }
            }
            introNamPose.Ground(mission.player.position.y);
        }
        void UpdateExecutionSound(){
            if(tensionSource==null){
                tensionSource=gameObject.AddComponent<AudioSource>();tensionSource.playOnAwake=false;tensionSource.spatialBlend=0;tensionSource.volume=.12f;tensionSource.priority=80;
                var samples=new float[12000];
                for(int i=0;i<samples.Length;i++){
                    float t=i/24000f;
                    float Pulse(float at)=>at<0?0:Mathf.Sin(at*2*Mathf.PI*65)*Mathf.Exp(-at*32);
                    samples[i]=(Pulse(t)+.6f*Pulse(t-.16f))*.7f;
                }
                heartbeat=AudioClip.Create("Rescue tension heartbeat",samples.Length,1,24000,false);heartbeat.SetData(samples,0);tensionSource.clip=heartbeat;
            }
            if(!shot&&Elapsed>=nextHeartbeat){tensionSource.Play();nextHeartbeat=Elapsed+Mathf.Lerp(.9f,.6f,ExecutionTension);}
            if(shot)tensionSource.Stop();
        }
        void AimExecution(){
            var actor=enemy.GetComponentInChildren<Animator>();
            var rifle=actor.GetComponent<Map01Rifle>();var grip=actor.GetComponent<Map01EnemyGrip>();
            if(rifle==null||rifle.weapon==null||grip==null)return;
            grip.Apply();
            var target=captiveVisual.actor.GetBoneTransform(HumanBodyBones.Chest).position;
            var muzzle=rifle.weapon.Muzzle;var upper=actor.GetBoneTransform(HumanBodyBones.RightUpperArm);
            if(muzzle!=null&&upper!=null){
                var neutral=upper.rotation;
                // Rotating the arm also moves the muzzle: solve again after each
                // grip update instead of leaving the barrel pointed past Hung.
                for(int pass=0;pass<5;pass++){
                    var correction=Quaternion.FromToRotation(muzzle.forward,(target-muzzle.position).normalized);
                    upper.rotation=Quaternion.RotateTowards(neutral,correction*upper.rotation,75);
                    grip.Apply();
                    if(Vector3.Angle(muzzle.forward,target-muzzle.position)<1)break;
                }
            }
        }
        Vector3 ClearStoryView(Vector3 target,float radius){
            foreach(var side in new[]{new Vector3(-1,.65f,-1),new Vector3(1,.65f,-1),new Vector3(-1,.7f,1),new Vector3(1,.7f,1)}){
                var candidate=target+side*radius;
                var delta=candidate-target;
                if(!Physics.SphereCastAll(target,.15f,delta.normalized,delta.magnitude,mission.ObstructionMask,QueryTriggerInteraction.Ignore)
                    .Any(h=>h.transform.GetComponentInParent<Health>()==null))return candidate;
            }
            return target+new Vector3(-radius,radius*1.4f,-radius);
        }
        public bool RunHome(Map01Rescue owner,bool hungArrivedFirst=false)
        {
            if (IsPlaying || owner.Layout == null) return false;
            var layout = owner.Layout;
            var player=owner.GetComponent<Map01Mission>();hungFirst=hungArrivedFirst;
            float gap=Mathf.Max(1.4f,layout.shelterFollowGap);
            var leader=hungFirst?player.hung:player.player;var follower=hungFirst?player.player:player.hung;
            var leaderPath=RouteThrough(leader.position,layout.shelterRun.Select(t=>t.position).ToArray());
            if(leaderPath.Length<2)return false;
            var followerEnd=AtDistance(leaderPath,Mathf.Max(0,Length(leaderPath)-gap),out _);
            var entranceHeading=layout.shelterRun.Length>1?layout.shelterRun[1].position-layout.shelterDoor.position:leaderPath[1]-leaderPath[0];
            hungYieldPath=hungFirst?null:YieldRoute(player.player.position,player.hung.position,leaderPath,entranceHeading,gap);
            var followerStart=hungYieldPath!=null?hungYieldPath[hungYieldPath.Length-1]:follower.position;
            var followerPath=RouteThrough(followerStart,layout.shelterRun.Take(layout.shelterRun.Length-1).Select(t=>t.position).Append(followerEnd).ToArray());
            namPath=hungFirst?followerPath:leaderPath;hungPath=hungFirst?leaderPath:followerPath;
            // Stop at Nam's first interior marker, independent of Hung's progress.
            var insideRoute=RouteThrough(player.player.position,layout.shelterRun.Take(Mathf.Min(3,layout.shelterRun.Length)).Select(t=>t.position).ToArray());
            if(insideRoute.Length<2)return false;
            namInsideDistance=Mathf.Min(Length(namPath),Length(insideRoute));
            bool rifle=player.ModernCombat.EquippedKind==WeaponKind.Rifle;
            var namClip=rifle?layout.namWalk:layout.namKnifeWalk;
            if(namClip==null)namClip=Resources.Load<AnimationClip>("Rescue/"+(rifle?"Nam_Shelter_Walk":"Nam_Shelter_Knife_Walk"));
            var hungClip=layout.hungWalk??Resources.Load<AnimationClip>("Rescue/Hung_Shelter_Walk");
            if(hungPath.Length<2||namClip==null||hungClip==null){Debug.LogError("Shelter return requires connected ground routes and walk clips.",this);return false;}
            float walkingSeconds=(Mathf.Max(Length(namPath),Length(hungPath))+(hungYieldPath!=null?Length(hungYieldPath):0)+gap)/Mathf.Max(.5f,layout.shelterWalkSpeed)+2;
            Begin(owner,Mode.Shelter,Mathf.Max(layout.shelterSeconds,walkingSeconds));
            namDistance=hungDistance=yieldDistance=namCovered=hungCovered=namWeight=hungWeight=0;
            namCycleSpeed=rifle?layout.namWalkCycleSpeed:layout.namKnifeWalkCycleSpeed;
            playerEnabled = mission.ModernPlayer.enabled; combatEnabled = mission.ModernCombat.enabled;
            mission.ModernCombat.PrepareCinematicCarry();
            var character = mission.player.GetComponent<CharacterController>(); characterEnabled = character != null && character.enabled;
            mission.ModernPlayer.enabled = mission.ModernCombat.enabled = false;
            if (character != null) character.enabled = false;
            hungAgent=mission.hung.GetComponent<NavMeshAgent>();
            if(hungAgent!=null){
                hungUpdatesPosition=hungAgent.updatePosition;hungUpdatesRotation=hungAgent.updateRotation;
                if(hungAgent.isOnNavMesh){hungAgent.ResetPath();hungAgent.isStopped=true;}
                hungAgent.updatePosition=hungAgent.updateRotation=false;
            }
            hungVisual = mission.hung.GetComponentInChildren<Map01HungVisual>();
            hungVisualEnabled = hungVisual != null && hungVisual.enabled;
            if(hungVisual!=null){
                // A checkpoint may reach the trigger before the follower's next Update.
                // Set the freed standing state and hide the captive rope before taking over.
                hungVisual.RestoreAfterCinematic();
                if(hungAgent!=null&&hungAgent.isOnNavMesh)hungAgent.isStopped=true;
                hungVisual.enabled=false;
            }
            namPose = new PosePlayer(mission.player.GetComponentInChildren<Animator>(),mission.player,namClip,rifle?layout.namIdle:layout.hungIdle);
            hungPose = new PosePlayer(mission.hung.GetComponentInChildren<Animator>(),mission.hung,hungClip,layout.hungIdle);
            Voice().Play("m1_rescue_shelter");
            return true;
        }
        private static Vector3[] RouteThrough(Vector3 start, Vector3[] markers)
        {
            var route = new System.Collections.Generic.List<Vector3> { start };
            foreach (var marker in markers)
            {
                var path = Map01Rescue.Path(route[route.Count - 1], marker);
                if (path.Length == 0) return Array.Empty<Vector3>();
                foreach (var p in path.Skip(1)) if (Vector3.Distance(route[route.Count - 1], p) > .02f) route.Add(p);
            }
            return route.ToArray();
        }
        private static Vector3[] YieldRoute(Vector3 nam,Vector3 hung,Vector3[] route,Vector3 entranceHeading,float gap)
        {
            bool inWay=Vector3.Distance(nam,hung)<gap;
            float travelled=0;
            for(int i=1;i<route.Length&&travelled<4;i++){
                var delta=route[i]-route[i-1];
                float u=Mathf.Clamp01(Vector3.Dot(hung-route[i-1],delta)/Mathf.Max(.001f,delta.sqrMagnitude));
                if(Vector3.Distance(hung,route[i-1]+delta*u)<gap)inWay=true;
                travelled+=delta.magnitude;
            }
            if(!inWay)return null;
            var forward=Vector3.ProjectOnPlane(entranceHeading,Vector3.up).normalized;
            var right=Vector3.Cross(Vector3.up,forward);
            float side=Vector3.Dot(nam-hung,right)>0?-1:1;
            // Prefer stepping back along the approach. Sideways bank points are fallbacks,
            // since its steep earth shoulders cannot support a comfortable walk.
            foreach(float lateral in new[]{0,side*1.6f,-side*1.6f,side*2.2f}){
                var candidate=hung+right*lateral-forward*2.2f;
                // The ramp top is lower than the surrounding dry bank. A small 3D sample
                // radius rejects every yielding point even though the bank is connected.
                if(!NavMesh.SamplePosition(candidate,out var hit,2f,NavMesh.AllAreas)||Vector3.Distance(hit.position,nam)<gap+.3f)continue;
                var path=Map01Rescue.Path(hung,hit.position);if(path.Length<2)continue;
                float initial=Vector3.Distance(nam,hung);bool clear=true;
                for(float d=.2f;d<Length(path);d+=.2f)
                    if(Vector3.Distance(AtDistance(path,d,out _),nam)<Mathf.Min(gap,initial)-.05f){clear=false;break;}
                if(clear)return path;
            }
            return null;
        }
        private void Update()
        {
            if (!IsPlaying) return;
            if(mission.Paused||ForestMenu.Visible){if(tensionSource!=null)tensionSource.Stop();return;}
            Elapsed += Time.deltaTime;
            if (CurrentMode == Mode.Shelter || CurrentMode == Mode.Execution || CurrentMode==Mode.Intro)
                skipHeld = Keyboard.current != null && Keyboard.current.escapeKey.isPressed ? skipHeld + Time.unscaledDeltaTime : 0;
            if(CurrentMode==Mode.Intro||CurrentMode==Mode.Execution){
                if(skipHeld>=1)Finish();else UpdateStory();return;
            }
            if (CurrentMode == Mode.Shelter)
            {
                WalkShelter(Time.deltaTime);
                if(skipHeld>=1||namDistance>=namInsideDistance-.001f)Finish();
                return;
            }
            if (Elapsed >= duration || skipHeld >= 1)
            {
                var actions = mission.player.GetComponent<Map01NamActions>();
                if (CurrentMode == Mode.Takedown && actions != null && actions.Busy) return;
                Finish();
            }
        }
        private void WalkShelter(float dt)
        {
            float speed=Mathf.Clamp(rescue.Layout.shelterWalkSpeed,.5f,1.65f);
            if(hungYieldPath!=null){
                yieldDistance=Mathf.Min(Length(hungYieldPath),yieldDistance+speed*dt);
                WalkAlong(mission.hung,hungYieldPath,yieldDistance,hungPose,ref hungCovered,ref hungWeight,rescue.Layout.hungWalkCycleSpeed);
                namPose.Sample(namCovered/Mathf.Max(.1f,namCycleSpeed),0);
                if(yieldDistance>=Length(hungYieldPath)-.001f)hungYieldPath=null;
                return;
            }
            float gap=Mathf.Max(1.4f,rescue.Layout.shelterFollowGap);
            if(hungFirst){
                float h=Advance(hungDistance,Length(hungPath),speed,dt);
                WalkAlong(mission.hung,hungPath,h,hungPose,ref hungCovered,ref hungWeight,rescue.Layout.hungWalkCycleSpeed);hungDistance=h;
                float n=Advance(namDistance,Length(namPath),speed,dt);
                if(Vector3.Distance(AtDistance(namPath,n,out _),mission.hung.position)<gap-.025f)n=namDistance;
                WalkAlong(mission.player,namPath,n,namPose,ref namCovered,ref namWeight,namCycleSpeed);namDistance=n;
            }else{
                float n=Advance(namDistance,Length(namPath),speed,dt);
                WalkAlong(mission.player,namPath,n,namPose,ref namCovered,ref namWeight,namCycleSpeed);namDistance=n;
                float h=Advance(hungDistance,Length(hungPath),speed,dt);
                if(Vector3.Distance(AtDistance(hungPath,h,out _),mission.player.position)<gap-.025f)h=hungDistance;
                WalkAlong(mission.hung,hungPath,h,hungPose,ref hungCovered,ref hungWeight,rescue.Layout.hungWalkCycleSpeed);hungDistance=h;
            }
        }
        private static float Advance(float distance,float length,float speed,float dt)=>
            Mathf.Min(length,distance+speed*Mathf.Clamp((length-distance)/.6f,.25f,1)*dt);
        private void WalkAlong(Transform root,Vector3[] points,float distance,PosePlayer pose,ref float covered,ref float weight,float cycleSpeed)
        {
            var position=AtDistance(points,distance,out var delta);
            float moved=Vector3.Distance(root.position,position);covered+=moved;root.position=position;
            var facing=Vector3.ProjectOnPlane(delta,Vector3.up);
            if(moved>.001f&&facing.sqrMagnitude>.001f)root.rotation=Quaternion.RotateTowards(root.rotation,Quaternion.LookRotation(facing),180*Time.deltaTime);
            weight=Mathf.MoveTowards(weight,moved>.001f?1:0,Time.deltaTime/.18f);
            pose.Sample(covered/Mathf.Max(.1f,cycleSpeed),weight);
        }
        private static Vector3 AtDistance(Vector3[] points,float distance,out Vector3 direction)
        {
            float d=Mathf.Clamp(distance,0,Length(points));
            int segment = 1;
            while (segment < points.Length - 1 && d > Vector3.Distance(points[segment-1], points[segment]))
            { d -= Vector3.Distance(points[segment-1], points[segment]); segment++; }
            var delta = points[segment]-points[segment-1];
            direction=delta;
            return Vector3.Lerp(points[segment-1],points[segment],delta.magnitude>.001f?d/delta.magnitude:1);
        }
        private static float Length(Vector3[] p) { float d=0;for(int i=1;i<p.Length;i++)d+=Vector3.Distance(p[i-1],p[i]);return d; }
        private bool CameraClear(Vector3 focus, Vector3 camera)
        {
            if (mission.SightCover != null && mission.SightCover.Blocks(focus, camera)) return false;
            return !Physics.Linecast(focus, camera, mission.ObstructionMask, QueryTriggerInteraction.Ignore);
        }
        private bool ShelterScenery(Transform obstacle)=>
            !obstacle.IsChildOf(mission.player)&&!obstacle.IsChildOf(mission.hung)
            &&obstacle.GetComponentInParent<Health>()==null
            &&obstacle.GetComponentInParent<Animator>()==null
            &&obstacle.GetComponentInChildren<Animator>()==null;
        private bool ShelterCameraClear(Vector3 focus,Vector3 position)
        {
            var delta=position-focus;
            return delta.sqrMagnitude>.001f&&!Physics.SphereCastAll(focus,.18f,delta.normalized,delta.magnitude,mission.ObstructionMask,QueryTriggerInteraction.Ignore)
                .Any(h=>ShelterScenery(h.transform));
        }
        private void LateUpdate()
        {
            if (!IsPlaying) return;
            if (CurrentMode == Mode.Takedown)
            {
                float u = (Elapsed - .20f) / rescue.Layout.killCameraSeconds;
                if (!clearShot || u <= 0 || u >= 1) { rig.ClearCinematicView(); return; }
                var actor = mission.player.GetComponentInChildren<Animator>();
                var chest = actor.GetBoneTransform(HumanBodyBones.Chest).position;
                var target = chest + mission.player.forward * .35f;
                float blend = Mathf.Sin(u*Mathf.PI);
                if (CameraClear(target,killPosition)) rig.SetCinematicView(killPosition,Quaternion.LookRotation(target-killPosition),46,blend);
                else rig.ClearCinematicView();
            }
            else if(CurrentMode==Mode.Execution||CurrentMode==Mode.Intro){RenderStory();}

            else
            {
                // Unity evaluates the gameplay Animator between Update and LateUpdate.
                // Apply the cinematic pose afterwards, before skin contacts and camera rendering,
                // so the idle controller cannot replace a moving character's run cycle.
                namPose?.Sample(); hungPose?.Sample();
                namPose?.Ground(); hungPose?.Ground();
                var lead=hungFirst?mission.hung:mission.player;var follow=hungFirst?mission.player:mission.hung;
                var target=(Vector3.Distance(lead.position,follow.position)>4?lead.position:Vector3.Lerp(lead.position,follow.position,.5f))+Vector3.up;
                var layout=rescue.Layout;
                var outside=target+lead.rotation*layout.shelterCameraOffset;
                // Keep the view along the narrow ramp instead of looking through its retaining wall.
                var corridor=target-lead.forward*2.5f+Vector3.up*1.25f;
                float entering=1-Mathf.SmoothStep(0,1,(lead.position.y-.6f)/1.8f);
                var desired=Vector3.Lerp(outside,corridor,entering);
                bool indoors=lead.position.y<.6f
                    &&Vector3.Distance(lead.position,layout.reportPoint.position)<8;
                if(indoors){
                    desired=layout.reportPoint.position+layout.interiorCameraOffset;
                    // Frame Nam from the room as he enters. Waiting for the follower on
                    // the ramp put the midpoint inside the doorway's retaining wall.
                    if(follow.position.y>=.6f)target=lead.position+Vector3.up;
                    if(!shelterRoomShot&&ShelterCameraClear(target,desired)){
                        // The outside and inside shots are on opposite sides of the walkers.
                        // Make a camera cut at the doorway; a damped crossing passes through Nam.
                        shelterCameraPosition=desired;cameraVelocity=Vector3.zero;shelterRoomShot=true;
                    }
                }
                shelterCameraPosition=Vector3.SmoothDamp(shelterCameraPosition,desired,ref cameraVelocity,Mathf.Max(.08f,layout.cameraDamping),25,Time.deltaTime);
                // Validate the blended position as well as its goal, so the camera cannot cross a wall.
                var offset=shelterCameraPosition-target;
                if(offset.sqrMagnitude>.001f){
                    // The focus is between two passengers. Their capsules must never push
                    // the lens into a body; only scenery obstructs this cinematic view.
                    var blocking=Physics.SphereCastAll(target,.18f,offset.normalized,offset.magnitude,mission.ObstructionMask,QueryTriggerInteraction.Ignore)
                        .Where(h=>ShelterScenery(h.transform))
                        .OrderBy(h=>h.distance).ToArray();
                    if(blocking.Length>0){
                        // Cut to the clear room shot if the damped transition crosses its
                        // doorway wall, rather than squeezing the lens into a passenger.
                        if(indoors&&ShelterCameraClear(target,desired)){shelterCameraPosition=desired;cameraVelocity=Vector3.zero;}
                        else shelterCameraPosition=target+offset.normalized*Mathf.Max(.3f,blocking[0].distance-.2f);
                    }
                }
                float blend=Mathf.SmoothStep(0,1,Elapsed/.45f);
                rig.SetCinematicView(shelterCameraPosition,Quaternion.Slerp(cameraRotation,Quaternion.LookRotation(target-shelterCameraPosition),blend),Mathf.Lerp(cameraFov,layout.shelterCameraFov,blend),1);
            }
        }
        public void Skip() { if(CurrentMode==Mode.Shelter||CurrentMode==Mode.Execution||CurrentMode==Mode.Intro)Finish(); }
        private void Finish()
        {
            if (!IsPlaying) return;
            var mode=CurrentMode;
            if(mode==Mode.Execution&&!shot){shot=true;rescue.CompleteExecution();}
            if(mode==Mode.Shelter){
                mission.player.position=namPath[namPath.Length-1];mission.hung.position=hungPath[hungPath.Length-1];
                namPose.Sample(namCovered/Mathf.Max(.1f,namCycleSpeed),0);hungPose.Sample(hungCovered/Mathf.Max(.1f,rescue.Layout.hungWalkCycleSpeed),0);
                namPose.Sample();hungPose.Sample();
            }
            Cleanup();
            if(mode==Mode.Shelter){
                rescue.CompleteDelivery();
                hungVisual?.RestoreAfterCinematic();
                GetComponent<Map01SaveSystem>().SaveSlot(ForestSaveSlots.AutoSlot,out _,true);
            }
            ForestMenu.SuppressKeysAfterCutscene();
        }
        /// <summary>Release animation/camera ownership without committing a skipped outcome.</summary>
        public void CancelForRestore(){if(IsPlaying)Cleanup(true);}
        private void Cleanup(bool restoring=false)
        {
            var mode=CurrentMode;CurrentMode=Mode.None;
            namPose?.Dispose();hungPose?.Dispose();namPose=hungPose=null;
            tensionSource?.Stop();
            storyVoice?.Stop();captivePose?.Dispose();interrogatorPose?.Dispose();captivePose=interrogatorPose=null;
            introNamPose?.Dispose();introNamPose=null;introLowerBody=null;
            foreach(var weapon in introHiddenWeapons)if(weapon!=null)weapon.SetActive(true);introHiddenWeapons.Clear();
            if(mode==Mode.Intro||mode==Mode.Execution){
                if(captiveVisual!=null)captiveVisual.EndCaptivePresentation(mode==Mode.Execution&&!restoring);
                if(mission!=null&&mission.player!=null&&mission.ModernPlayer!=null&&mission.ModernCombat!=null){
                    var cc=mission.player.GetComponent<CharacterController>();if(cc!=null)cc.enabled=storyCCEnabled;
                    if(storyActions!=null)storyActions.enabled=storyActionsEnabled;mission.ModernPlayer.enabled=storyPlayerEnabled;mission.ModernCombat.enabled=storyCombatEnabled;
                    bool resumeSneaking=storySneaking||(mode==Mode.Intro&&!restoring);
                    mission.Crouched=resumeSneaking;
                    mission.ModernPlayer.RestoreMotion(resumeSneaking);
                }
            }
            if(mission==null)return;
            mission.Cinematic=false;mission.ModernHealth.CinematicInvulnerable=invulnerable;rig?.ClearCinematicView();
            if(mode==Mode.Execution&&enemy!=null){var actor=enemy.GetComponentInChildren<Animator>();if(actor!=null&&actor.parameters.Any(p=>p.name=="Engaged"))actor.SetBool("Engaged",false);}
            if(mode==Mode.Shelter){
                var cc=mission.player.GetComponent<CharacterController>();if(cc!=null)cc.enabled=characterEnabled;
                mission.ModernPlayer.enabled=playerEnabled;mission.ModernCombat.enabled=combatEnabled;
                if(hungVisual!=null)hungVisual.enabled=hungVisualEnabled;
                if(hungAgent!=null){
                    if(hungAgent.enabled){hungAgent.Warp(mission.hung.position);if(hungAgent.isOnNavMesh)hungAgent.ResetPath();}
                    hungAgent.updatePosition=hungUpdatesPosition;hungAgent.updateRotation=hungUpdatesRotation;
                }
                hungVisual?.RestoreAfterCinematic();
            }
            if(mode!=Mode.Takedown)mission.ModernCombat?.RestoreAfterCinematic();
            if(mode==Mode.Intro&&!restoring){
                rescue.CompleteApproachIntro();
                mission.Say("Hùng đang bị giam giữ. Đã chuyển sang đi lén — [C] đổi tư thế. Tận dụng vật che để tiếp cận.",6);
            }
        }
        private void OnGUI()
        {
            if(CurrentMode!=Mode.Shelter&&CurrentMode!=Mode.Execution&&CurrentMode!=Mode.Intro)return;
            var color=GUI.color;int depth=GUI.depth;GUI.depth=-90;GUI.color=Color.black;
            GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height*.09f),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0,Screen.height*.91f,Screen.width,Screen.height*.09f),Texture2D.whiteTexture);
            var skipStyle=new GUIStyle(GUI.skin.label){fontSize=Mathf.RoundToInt(Screen.height*.022f),alignment=TextAnchor.MiddleRight};
            GUI.color=Color.white;GUI.Label(new Rect(Screen.width*.65f,Screen.height*.014f,Screen.width*.33f,Screen.height*.05f),"Giữ Esc 1 giây để bỏ qua",skipStyle);GUI.color=color;GUI.depth=depth;
            if(CurrentMode==Mode.Execution){
                // Directional alarm belongs only to Nam's establishing shot.
                var towards=mission.hung.position-mission.gameCamera.transform.position;
                float side=Vector3.Dot(mission.gameCamera.transform.right,towards.normalized);
                float alpha=shot?.16f:Mathf.Lerp(.15f,.35f,(Mathf.Sin(Elapsed*7)+1)*.5f);
                GUI.color=new Color(.65f,.06f,.035f,ExecutionShowsCaptive||ExecutionShowsReaction?0:alpha);
                GUI.DrawTexture(new Rect(side<-.2f?0:side>.2f?Screen.width-18:0,Screen.height*.09f,Mathf.Abs(side)>.2f?18:Screen.width,Mathf.Abs(side)>.2f?Screen.height*.82f:12),Texture2D.whiteTexture);
                GUI.color=new Color(1,.65f,.3f,ExecutionShowsCaptive||ExecutionShowsReaction?0:.9f);
                var danger=new GUIStyle(GUI.skin.label){fontSize=Mathf.RoundToInt(Screen.height*.05f),fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};
                GUI.Label(new Rect(Screen.width*(.5f+side*.3f)-24,Screen.height*.14f,48,48),"!",danger);
                if(shot){GUI.color=new Color(0,0,0,Mathf.SmoothStep(0,1,(Elapsed-impactAt-CollapseViewSeconds-ReactionViewSeconds)/FailureFadeSeconds));GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);}
                GUI.color=color;
            }
            if(CurrentMode==Mode.Intro&&storyVoice!=null&&!string.IsNullOrEmpty(storyVoice.CurrentCaption)){
                var subtitle=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,wordWrap=true,fontSize=Mathf.RoundToInt(Screen.height*.031f)};
                GUI.color=Color.white;GUI.Label(new Rect(Screen.width*.12f,Screen.height*.91f,Screen.width*.76f,Screen.height*.08f),storyVoice.CurrentCaption,subtitle);GUI.color=color;
            }
            if(CurrentMode==Mode.Intro){
                // A short blink conceals the distant edit; the lens only moves locally at the hostage.
                float alpha=Mathf.Max(1-Mathf.Abs(Elapsed-IntroCutIn)/.12f,1-Mathf.Abs(Elapsed-IntroCutOut)/.12f);
                if(alpha>0){GUI.color=new Color(0,0,0,Mathf.Clamp01(alpha));GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);GUI.color=color;}
            }
        }

        private void OnDisable(){if(IsPlaying)Cleanup();}
        private void OnDestroy(){if(heartbeat!=null)Destroy(heartbeat);if(tensionSource!=null)Destroy(tensionSource);}
        private sealed class PosePlayer : IDisposable
        {
            private readonly Animator actor;
            private readonly Transform characterRoot;
            private readonly RuntimeAnimatorController controller;
            private readonly bool rootMotion;
            private readonly AnimatorCullingMode culling;
            private readonly Vector3 localPosition;
            private readonly Quaternion localRotation;
            private PlayableGraph graph;
            private AnimationClipPlayable run,idle;
            private AnimationMixerPlayable mixer;
            private readonly float runLength,idleLength;
            private float sampleTime,movementWeight;
            private readonly Mesh baked=new Mesh();
            private readonly SkinnedMeshRenderer[] skins;
            private readonly Map01SkinContact contact;
            public PosePlayer(Animator actor,Transform characterRoot,AnimationClip moving,AnimationClip resting)
            {
                this.actor=actor;this.characterRoot=characterRoot;controller=actor.runtimeAnimatorController;rootMotion=actor.applyRootMotion;culling=actor.cullingMode;
                localPosition=actor.transform.localPosition;localRotation=actor.transform.localRotation;
                actor.applyRootMotion=false;actor.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                graph=PlayableGraph.Create("Rescue shelter locomotion");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                run=AnimationClipPlayable.Create(graph,moving);idle=AnimationClipPlayable.Create(graph,resting);run.SetApplyFootIK(true);idle.SetApplyFootIK(true);
                runLength=moving.length;idleLength=resting.length;
                run.SetSpeed(0);idle.SetSpeed(0);
                mixer=AnimationMixerPlayable.Create(graph,2);graph.Connect(idle,0,mixer,0);graph.Connect(run,0,mixer,1);
                AnimationPlayableOutput.Create(graph,"Run and settle",actor).SetSourcePlayable(mixer);graph.Play();
                skins=actor.GetComponentsInChildren<SkinnedMeshRenderer>();
                contact=new Map01SkinContact(actor);
            }
            public void Sample(float time,float weight)
            {
                sampleTime=time;movementWeight=weight;
            }
            public void Sample()
            {
                if(!graph.IsValid())return;
                // Distance controls phase. Wrap explicitly so a manually sampled cycle keeps
                // stepping for the entire route, including after the first clip duration.
                run.SetTime(runLength>.001f?Mathf.Repeat(sampleTime,runLength):0);
                idle.SetTime(idleLength>.001f?Mathf.Repeat(sampleTime,idleLength):0);
                mixer.SetInputWeight(0,1-movementWeight);mixer.SetInputWeight(1,movementWeight);graph.Evaluate(0);
            }
            public void Ground()
            {
                // Keep the lowest sole on the ramp while retaining authored leg motion.
                float ground=float.NegativeInfinity;
                int hits=Map01Probe.Raycast(characterRoot.position+Vector3.up*1.2f,Vector3.down,2.5f);
                for(int i=0;i<hits;i++){
                    var hit=Map01Probe.Hits[i];
                    if(hit.normal.y>.6f&&hit.point.y<=characterRoot.position.y+.25f&&hit.transform.GetComponentInParent<Health>()==null
                        &&!hit.transform.IsChildOf(characterRoot)&&hit.transform.GetComponentInParent<Map01HungVisual>()==null)
                        ground=Mathf.Max(ground,hit.point.y);
                }
                if(float.IsNegativeInfinity(ground))ground=characterRoot.position.y;
                float sole=contact.LowestY();
                if(float.IsInfinity(sole))foreach(var skin in skins){skin.BakeMesh(baked);foreach(var v in baked.vertices)sole=Mathf.Min(sole,skin.transform.TransformPoint(v).y);}
                if(!float.IsInfinity(sole))actor.transform.position+=Vector3.up*Mathf.Clamp(ground-sole,-.35f,.35f);
            }
            public void Dispose()
            {
                if(graph.IsValid())graph.Destroy();
                UnityEngine.Object.Destroy(baked);
                if(actor==null)return;
                actor.runtimeAnimatorController=controller;actor.applyRootMotion=rootMotion;actor.cullingMode=culling;
                actor.transform.SetLocalPositionAndRotation(localPosition,localRotation);actor.Rebind();if(actor.isActiveAndEnabled)actor.Update(0);
            }
        }
    }
}
