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
    public sealed partial class Map01RescueCinematic : MonoBehaviour
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
        private float introReturnAt,finalBurstAt=-1,lastRoundAt=-10,dyingReleaseAt=-1;
        private int executionRounds;
        private bool introImpact,introRightImpact;
        private float introRightImpactAt;
        private float leftContactError=-1,rightContactError=-1;
        private float leftLean,rightLean;
        private Quaternion leftFacing,rightFacing;
        private Vector3 leftBodyTarget,rightBodyTarget;
        public Vector3 LeftBodyDelta{get;private set;}
        public Vector3 RightBodyDelta{get;private set;}
        private AnimationClip interrogationLeftPunch;
        public int InterrogationHits=>(introImpact?1:0)+(introRightImpact?1:0);
        public float LeftBodyContactError=>leftContactError;
        public float RightBodyContactError=>rightContactError;
        private float introImpactAt;
        private AudioSource contactSource;
        private AudioClip contactThud;
        private Vector3 strikeTarget;
        private AnimationClip interrogationPunch,interrogationWalk,interrogationIdle;
        private Map01InterrogationSling interrogationSling;
        private Vector3 introContactPoint;
        private float interrogationLean;
        private Quaternion interrogationFacing;
        private (Transform bone,Vector3 position,Quaternion rotation)[] interrogationStanding;
        public float InterrogatorRifleBehindPack=>interrogationSling?.BehindChest??0;
        public bool InterrogatorRifleSlung=>interrogationSling!=null&&interrogationSling.Weight>.95f;
        public int ExecutionRounds=>executionRounds;
        public float InterrogationContactError{get;private set;}
        public bool HungSpeakingLastWords=>CurrentMode==Mode.Execution&&storyBeat==4;
        public bool IntroReturning=>CurrentMode==Mode.Intro&&introReturnAt>0;
        static readonly float[] OpeningRounds={0,.12f,.24f,.95f,1.07f,1.19f};
        static readonly float[] ClosingRounds={0,.12f,.24f,.36f};
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
        private Vector3 executionView,executionFocus,reactionView,lastWordsView;
        const float CollapseViewSeconds=3.3f,ReactionViewSeconds=1.7f,FailureFadeSeconds=.8f;
        public bool ExecutionShowsCaptive=>CurrentMode==Mode.Execution&&storyBeat>=2&&(dyingReleaseAt<0||Elapsed-dyingReleaseAt<.65f);
        public bool ExecutionShowsReaction=>CurrentMode==Mode.Execution&&dyingReleaseAt>=0&&Elapsed-dyingReleaseAt>=.65f;
        public float ExecutionTension=>CurrentMode==Mode.Execution?Mathf.Clamp01(Elapsed/3):0;
        private Map01CinematicPose introNamPose;
        private AnimationClip introNamIdle;
        private Vector3 introNamPosition;
        private Quaternion introNamRotation;
        private float introRadioAt,introReleaseAt;
        private (Transform bone,Vector3 position,Quaternion rotation)[] introLowerBody;
        private readonly System.Collections.Generic.List<GameObject> introHiddenWeapons=new();
        private Vector3 introCaptiveView,introCaptiveFocus;
        const float IntroPanAt=.65f,IntroCutIn=1.3f,IntroWalkAt=2f,IntroStrikeAt=3.35f,IntroHitAt=3.95f,IntroRightStrikeAt=4.65f,IntroRightHitAt=5.25f,IntroWalkBackAt=6.05f;
        float IntroCutOut=>introReturnAt>0?introReturnAt+.85f:float.PositiveInfinity;
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
            if(mode==Mode.Execution){shot=true;rescue.CompleteExecution();}
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
            tensionSource?.Stop();contactSource?.Stop();
            if(mode==Mode.Intro&&enemy!=null)enemy.transform.position=interrogatorStart;
            storyVoice?.Stop();interrogationSling?.Dispose();interrogationSling=null;interrogationStanding=null;captivePose?.Dispose();interrogatorPose?.Dispose();captivePose=interrogatorPose=null;
            introNamPose?.Dispose();introNamPose=null;introLowerBody=null;interrogationSling?.Dispose();interrogationSling=null;
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
                if(dyingReleaseAt>=0){GUI.color=new Color(0,0,0,Mathf.SmoothStep(0,1,(Elapsed-dyingReleaseAt-.65f-ReactionViewSeconds)/FailureFadeSeconds));GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);}
                GUI.color=color;
            }
            if((CurrentMode==Mode.Intro||CurrentMode==Mode.Execution)&&storyVoice!=null&&!string.IsNullOrEmpty(storyVoice.CurrentCaption)){
                var subtitle=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,wordWrap=true,fontSize=Mathf.RoundToInt(Screen.height*.031f)};
                GUI.color=Color.white;GUI.Label(new Rect(Screen.width*.12f,Screen.height*.91f,Screen.width*.76f,Screen.height*.08f),storyVoice.CurrentCaption,subtitle);GUI.color=color;
            }
            if(CurrentMode==Mode.Intro){
                // The pan carries the look; a brief blink hides only the distant edit.
                float alpha=Mathf.Max(1-Mathf.Abs(Elapsed-IntroCutIn)/.12f,1-Mathf.Abs(Elapsed-IntroCutOut)/.12f);
                if(alpha>0){GUI.color=new Color(0,0,0,Mathf.Clamp01(alpha));GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);GUI.color=color;}
            }
        }

        private void OnDisable(){if(IsPlaying)Cleanup();}
        private void OnDestroy(){if(heartbeat!=null)Destroy(heartbeat);if(tensionSource!=null)Destroy(tensionSource);if(contactThud!=null)Destroy(contactThud);if(contactSource!=null)Destroy(contactSource);}
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
