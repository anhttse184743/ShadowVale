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
        private AnimationClip interrogatorWalk;
        private bool canApproach;

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
            captivePose=new Map01CinematicPose(captiveVisual.actor);interrogatorPose=new Map01CinematicPose(enemy.GetComponentInChildren<Animator>());
            RenderStory();
        }
        public bool ShowIntro(Map01Rescue owner,Map01EnemyController overseer){
            if(IsPlaying||Map01CinematicPose.Clip("Hung_Captive_Recoil")==null||Map01CinematicPose.Clip("Enemy_Interrogate_Strike")==null)return false;
            enemy=overseer;Begin(owner,Mode.Intro,Mathf.Max(12,owner.Layout.introSeconds));
            interrogatorStart=enemy.transform.position;
            var direction=Vector3.ProjectOnPlane(interrogatorStart-mission.hung.position,Vector3.up).normalized;
            var desired=mission.hung.position+direction*1.05f;
            canApproach=NavMesh.SamplePosition(desired,out var contact,.4f,NavMesh.AllAreas)&&!NavMesh.Raycast(interrogatorStart,contact.position,out _,NavMesh.AllAreas);
            interrogatorClose=canApproach?contact.position:interrogatorStart;
            interrogatorWalk=Map01CinematicPose.Clip("Enemy_Interrogate_Walk");
            OwnStoryActors();owner.MarkIntroSeen();return true;
        }
        public void ExecuteHostage(Map01Rescue owner,Map01EnemyController overseer,Map01EnemyController reporter=null){
            if(IsPlaying)return;
            enemy=overseer;Begin(owner,Mode.Execution,20);OwnStoryActors();
            if(reporter!=null&&reporter!=overseer&&reporter.Alive)Voice().Play("m1_rescue_alarm");
            else{storyBeat=1;Voice().Play("m1_rescue_execute_order");}
            overseer.FaceImmediate(mission.hung.position);
        }
        void UpdateStory(){
            if(CurrentMode==Mode.Intro){
                if(storyBeat==0&&Elapsed>=2){Voice().Play("m1_rescue_interrogate");storyBeat=1;}
                else if(storyBeat==1&&Elapsed>=6.4f&&!Voice().HasSpeech){Voice().Play("m1_rescue_intro_nam");storyBeat=2;}
                else if(storyBeat==2&&Elapsed>=duration&&!Voice().HasSpeech)Finish();
                return;
            }
            if(storyBeat==0&&!Voice().HasSpeech){storyBeat=1;Voice().Play("m1_rescue_execute_order");}
            else if(storyBeat==1&&!Voice().HasSpeech){storyBeat=2;beatAt=Elapsed;}
            else if(storyBeat==2&&Elapsed-beatAt>=rescue.Layout.executionCameraSeconds+rescue.Layout.executionAimSeconds){
                shot=true;impactAt=Elapsed;storyBeat=3;
                enemy.FireAtHostage(captiveVisual.actor.GetBoneTransform(HumanBodyBones.Chest).position,0,true);
                if(rescue.Layout.gunshot!=null)AudioSource.PlayClipAtPoint(rescue.Layout.gunshot,enemy.transform.position,.65f);
                rescue.CompleteExecution();
            }else if(storyBeat==3&&Elapsed-impactAt>=3+rescue.Layout.executionHoldSeconds)Finish();
        }
        void RenderStory(){
            bool walking=false;float travel=0;
            if(CurrentMode==Mode.Intro&&canApproach){
                float amount=Elapsed<5?Mathf.SmoothStep(0,1,Elapsed/2):1-Mathf.SmoothStep(0,1,(Elapsed-9)/3);
                enemy.transform.position=Vector3.Lerp(interrogatorStart,interrogatorClose,amount);
                walking=Elapsed<2||(Elapsed>9&&Elapsed<12);travel=Elapsed<2?Elapsed:Elapsed-9;
            }
            enemy.FaceImmediate(mission.hung.position);
            float captiveTime=CurrentMode==Mode.Execution?(shot?Elapsed-impactAt:0):Mathf.Clamp(Elapsed-2,0,3);
            captivePose.Sample(Map01CinematicPose.Clip(shot?"Hung_Bound_Execution":"Hung_Captive_Recoil"),captiveTime);
            interrogatorPose.Sample(walking&&interrogatorWalk!=null?interrogatorWalk:Map01CinematicPose.Clip("Enemy_Interrogate_Strike"),walking?travel*.65f:CurrentMode==Mode.Intro?Mathf.Clamp(Elapsed-2,0,3):0,walking);
            captivePose.Ground(mission.hung.position.y);interrogatorPose.Ground(enemy.transform.position.y);captiveVisual.SampleCaptiveContacts();
            if(CurrentMode==Mode.Execution&&storyBeat>=2)AimExecution();
            var target=CurrentMode==Mode.Intro?mission.hung.position+Vector3.up*.95f:Vector3.Lerp(enemy.transform.position,mission.hung.position,.65f)+Vector3.up*.8f;
            var close=ClearStoryView(target,3);
            Vector3 desired;float blend;
            if(CurrentMode==Mode.Intro){
                var wide=ClearStoryView(target,4.5f);float detail=Mathf.SmoothStep(0,1,(Elapsed-2)/2);
                desired=Vector3.Lerp(wide,close,detail);blend=Mathf.SmoothStep(0,1,Elapsed/2);
            }else{desired=close;blend=storyBeat<2?0:Mathf.SmoothStep(0,1,(Elapsed-beatAt)/Mathf.Max(1,rescue.Layout.executionCameraSeconds));}
            var position=Vector3.Lerp(cameraStart,desired,blend);
            var rotation=Quaternion.Slerp(cameraRotation,Quaternion.LookRotation(target-position),blend);
            float fov=Mathf.Lerp(cameraFov,50,blend);
            if(CurrentMode==Mode.Intro){
                // The interrogation belongs to Hung's close view. Nam's quiet
                // thought starts only after a gentle return to the player's view.
                float returning=Mathf.SmoothStep(0,1,(Elapsed-5)/1.4f);
                position=Vector3.Lerp(position,cameraStart,returning);rotation=Quaternion.Slerp(rotation,cameraRotation,returning);fov=Mathf.Lerp(fov,cameraFov,returning);
            }
            rig.SetCinematicView(position,rotation,fov,1);
        }
        void AimExecution(){
            var actor=enemy.GetComponentInChildren<Animator>();var rifle=actor.GetComponent<Map01Rifle>();if(rifle==null||rifle.weapon==null)return;
            var target=captiveVisual.actor.GetBoneTransform(HumanBodyBones.Chest).position;
            var chest=actor.GetBoneTransform(HumanBodyBones.Chest).position;var direction=(target-chest).normalized;
            Map01BushTakedown.SolveArm(actor,HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand,chest+actor.transform.right*.13f+direction*.25f,1);
            var hand=actor.GetBoneTransform(HumanBodyBones.RightHand);var muzzle=rifle.weapon.Muzzle;
            if(muzzle!=null)for(int i=0;i<3;i++)hand.rotation=Quaternion.FromToRotation(muzzle.forward,(target-muzzle.position).normalized)*hand.rotation;
            if(rifle.support!=null)Map01BushTakedown.SolveArm(actor,HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftHand,rifle.support.position,-1);
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
            if (!IsPlaying || mission.Paused || ForestMenu.Visible) return;
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
        private void Cleanup()
        {
            var mode=CurrentMode;CurrentMode=Mode.None;
            namPose?.Dispose();hungPose?.Dispose();namPose=hungPose=null;
            storyVoice?.Stop();captivePose?.Dispose();interrogatorPose?.Dispose();captivePose=interrogatorPose=null;
            if(mode==Mode.Intro||mode==Mode.Execution){
                if(captiveVisual!=null)captiveVisual.EndCaptivePresentation(mode==Mode.Execution);
                if(mission!=null&&mission.player!=null&&mission.ModernPlayer!=null&&mission.ModernCombat!=null){
                    var cc=mission.player.GetComponent<CharacterController>();if(cc!=null)cc.enabled=storyCCEnabled;
                    if(storyActions!=null)storyActions.enabled=storyActionsEnabled;mission.ModernPlayer.enabled=storyPlayerEnabled;mission.ModernCombat.enabled=storyCombatEnabled;
                    mission.ModernPlayer.RestoreMotion(storySneaking);
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
        }
        private void OnGUI()
        {
            if(CurrentMode!=Mode.Shelter&&CurrentMode!=Mode.Execution&&CurrentMode!=Mode.Intro)return;
            var color=GUI.color;int depth=GUI.depth;GUI.depth=-90;GUI.color=Color.black;
            GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height*.09f),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0,Screen.height*.91f,Screen.width,Screen.height*.09f),Texture2D.whiteTexture);
            GUI.color=Color.white;GUI.Label(new Rect(Screen.width-245,14,240,30),"Giữ Esc 1 giây để bỏ qua");GUI.color=color;GUI.depth=depth;
            if(storyVoice!=null&&!string.IsNullOrEmpty(storyVoice.CurrentCaption)){
                var subtitle=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,wordWrap=true,fontSize=Mathf.RoundToInt(Screen.height*.026f)};
                GUI.color=Color.white;GUI.Label(new Rect(Screen.width*.12f,Screen.height*.91f,Screen.width*.76f,Screen.height*.08f),storyVoice.CurrentCaption,subtitle);GUI.color=color;
            }
        }

        private void OnDisable(){if(IsPlaying)Cleanup();}
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
                foreach(var hit in Physics.RaycastAll(characterRoot.position+Vector3.up*1.2f,Vector3.down,2.5f,~0,QueryTriggerInteraction.Ignore))
                    if(hit.normal.y>.6f&&hit.point.y<=characterRoot.position.y+.25f&&hit.transform.GetComponentInParent<Health>()==null
                        &&!hit.transform.IsChildOf(characterRoot)&&hit.transform.GetComponentInParent<Map01HungVisual>()==null)
                        ground=Mathf.Max(ground,hit.point.y);
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
