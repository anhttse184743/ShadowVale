using System.Linq;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Player;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace ShadowVale.Map01 {
    [DefaultExecutionOrder(110)]
    public sealed class Map01BushTakedown : MonoBehaviour {
        public enum Phase { None, Pull, Struggle, Finish, Escape }
        public Phase CurrentPhase{get;private set;}
        public bool Active=>CurrentPhase!=Phase.None;
        public float PhaseTime=>clock;
        public float SampledPhaseTime{get;private set;}
        public Map01EnemyController Victim{get;private set;}
        public Map01BushTiming Timing{get;private set;}
        public Avatar GameplayAvatar=>namPose?.OriginalAvatar;
        public bool Paused{get;private set;}
        Map01Mission mission;
        ThirdPersonCamera camera;
        Map01CinematicPose namPose,enemyPose;
        Map01NamActions actions;
        bool playerEnabled,combatEnabled,characterEnabled,actionsEnabled,wasSneaking,immune,victimImmune,armed,hit;
        float clock;
        Vector3 startNam,startEnemy,endNam,endEnemy,recoverNam,recoverEnemy,grabNam;
        float approachTravel;Map01GrappleFeet namFeet,enemyFeet;
        Quaternion initialNam,initialEnemy,facing;
        Vector3 cameraStart;
        Vector3 cameraOffset;
        Quaternion cameraRotation;
        float cameraFov;
        Map01GrappleContact contact;
        Map01HugHands hugHands;
        (Transform bone,Quaternion neutral,Quaternion previous)[] fingerPoses;
        public float HeadPenetration {get;private set;}
        public const float FinishSeconds=2.4f, StrikeAt=1.02f, EscapeSeconds=1.8f,PullSeconds=2.6f,GripAt=1.2f;
        Weapon knife;
        bool knifeWasVisible;
        string interactionCheckpoint;
        public struct Capture { public ForestPoint bush;public Vector3 nam,enemy,grab;public Quaternion facing; }
        void Awake(){mission=GetComponent<Map01Mission>();}
        public bool OffersPullHint(Map01EnemyController enemy)=>mission!=null&&!Active&&!mission.Cinematic&&!mission.Stopped&&mission.Crouched
            &&mission.ModernCombat.EquippedKind==WeaponKind.Knife&&enemy!=null&&enemy.Alive&&!enemy.IsBoss&&!enemy.Engaged
            &&Vector3.Distance(mission.player.position,enemy.transform.position)<=2.6f&&mission.TryGetHideSpot(mission.player.position,out _);
        public bool Qualifies(Map01EnemyController enemy,out Capture capture) {
            capture=default;
            if(Active||mission==null||mission.Cinematic||mission.Stopped||!mission.Crouched||mission.ModernCombat.EquippedKind!=WeaponKind.Knife
                ||enemy==null||!enemy.Alive||enemy.Engaged||enemy.IsBoss)return false;
            var delta=Vector3.ProjectOnPlane(mission.player.position-enemy.transform.position,Vector3.up);
            if(delta.magnitude>2||Mathf.Abs(mission.player.position.y-enemy.transform.position.y)>.65f||Vector3.Angle(enemy.transform.forward,delta)<90)return false;
            if(!mission.TryGetHideSpot(mission.player.position,out var bush))return false;
            var ray=enemy.transform.position+Vector3.up;
            if(Physics.Linecast(mission.player.position+Vector3.up,ray,out var obstruction,mission.ObstructionMask,QueryTriggerInteraction.Ignore)
                &&!obstruction.transform.IsChildOf(enemy.transform)&&!obstruction.transform.IsChildOf(mission.player))return false;
            var direction=Vector3.ProjectOnPlane(enemy.transform.position-mission.player.position,Vector3.up).normalized;
            if(direction.sqrMagnitude<.1f)direction=enemy.transform.forward;
            if(!NavMesh.SamplePosition(bush.transform.position,out var center,.7f,NavMesh.AllAreas))return false;
            // Behind the left shoulder, both arms wrap below the shoulders.
            var guard=center.position+direction*.16f;
            var nam=guard-direction*.43f-Vector3.Cross(Vector3.up,direction)*.15f;
            if(bush.radius<.72f||Vector3.Distance(nam,bush.transform.position)>.0f+bush.radius-.27f||Vector3.Distance(guard,bush.transform.position)>bush.radius-.27f)return false;
            if(!NavMesh.SamplePosition(nam,out var n,.35f,NavMesh.AllAreas)||!NavMesh.SamplePosition(guard,out var g,.35f,NavMesh.AllAreas))return false;
            if(Vector3.Distance(n.position,bush.transform.position)>bush.radius-.27f||Vector3.Distance(g.position,bush.transform.position)>bush.radius-.27f
                ||Vector3.Distance(n.position,g.position)<.45f)return false;
            if(!PullClear(mission.player.position,n.position,enemy)||!PullClear(enemy.transform.position,g.position,enemy))return false;
            var reach=enemy.transform.position-direction*.43f-Vector3.Cross(Vector3.up,direction)*.15f;
            if(!NavMesh.SamplePosition(reach,out var grab,.3f,NavMesh.AllAreas)||!PullClear(mission.player.position,grab.position,enemy)||!PullClear(grab.position,n.position,enemy))return false;
            capture=new Capture{bush=bush,nam=n.position,enemy=g.position,grab=grab.position,facing=Quaternion.LookRotation(direction)};return true;
        }
        bool Clear(Vector3 root,Map01EnemyController enemy) {
            return !Physics.OverlapCapsule(root+Vector3.up*.35f,root+Vector3.up*1.2f,.25f,mission.ObstructionMask,QueryTriggerInteraction.Ignore)
                .Any(c=>!c.transform.IsChildOf(mission.player)&&!c.transform.IsChildOf(enemy.transform)&&c.GetComponentInParent<Health>()==null);
        }
        bool PullClear(Vector3 start,Vector3 end,Map01EnemyController enemy){
            if(Map01Rescue.Path(start,end).Length<2||NavMesh.Raycast(start,end,out _,NavMesh.AllAreas))return false;
            float length=Vector3.Distance(start,end);
            for(float d=0;d<=length+.15f;d+=.15f){
                var at=Vector3.Lerp(start,end,length>.001f?Mathf.Clamp01(d/length):1);
                if(!NavMesh.SamplePosition(at,out var ground,.18f,NavMesh.AllAreas)||Mathf.Abs(ground.position.y-at.y)>.15f||!Clear(at,enemy))return false;
            }
            return true;
        }
        public bool TryBegin(Map01EnemyController enemy) {
            if(!Qualifies(enemy,out var plan))return false;
            foreach(var who in new[]{"Nam","Enemy"})foreach(var clip in new[]{"GrabPull","StruggleLoop","Finish","Escape","Recovery"})
                if(Map01CinematicPose.Clip(who+"_Bush_"+clip)==null)return false;
            var cinematicAvatar=Resources.Load<Avatar>("Rescue/Story/Nam_Bush_Avatar");if(cinematicAvatar==null||!cinematicAvatar.isValid)return false;
            interactionCheckpoint=GetComponent<Map01SaveSystem>().CaptureInteractionCheckpoint();
            Victim=enemy;Timing=new Map01BushTiming();CurrentPhase=Phase.Pull;clock=0;armed=hit=false;
            mission.CloseGameplayPanel();GetComponent<Map01StoneThrow>()?.Cancel();GetComponent<Map01Scouting>()?.HoldBinoculars(false);DialogueVoice.StopAll();
            startNam=mission.player.position;startEnemy=enemy.transform.position;endNam=plan.nam;endEnemy=plan.enemy;grabNam=plan.grab;facing=plan.facing;approachTravel=0;
            initialNam=mission.player.rotation;initialEnemy=enemy.transform.rotation;
            knife=mission.ModernCombat.GetComponentsInChildren<Weapon>(true).FirstOrDefault(w=>w.Kind==WeaponKind.Knife);
            knifeWasVisible=knife!=null&&knife.gameObject.activeSelf;
            if(knife!=null)knife.gameObject.SetActive(false);
            camera=mission.gameCamera.GetComponent<ThirdPersonCamera>();cameraStart=mission.gameCamera.transform.position;cameraRotation=mission.gameCamera.transform.rotation;cameraFov=mission.gameCamera.fieldOfView;
            cameraOffset=new Vector3(2.4f,.9f,.6f);
            var shotFocus=(endNam+endEnemy)*.5f+Vector3.up*.9f;
            foreach(var candidate in new[]{new Vector3(2.4f,.9f,.6f),new Vector3(-2.4f,.9f,.6f),new Vector3(2.5f,1.2f,-1.8f),new Vector3(-2.5f,1.2f,-1.8f)})
                if(ShotClear(shotFocus,shotFocus+facing*candidate)){cameraOffset=candidate;break;}
            actions=Map01NamActions.For(mission);actionsEnabled=actions.enabled;actions.enabled=false;
            playerEnabled=mission.ModernPlayer.enabled;combatEnabled=mission.ModernCombat.enabled;wasSneaking=mission.Crouched;
            var cc=mission.player.GetComponent<CharacterController>();characterEnabled=cc.enabled;cc.enabled=false;
            immune=mission.ModernHealth.CinematicInvulnerable;mission.ModernHealth.CinematicInvulnerable=true;
            victimImmune=enemy.GetComponent<Health>().CinematicInvulnerable;enemy.GetComponent<Health>().CinematicInvulnerable=true;
            mission.ModernPlayer.enabled=mission.ModernCombat.enabled=false;mission.Cinematic=true;
            foreach(var guard in mission.Enemies)guard.PauseForCinematic(true);
            enemy.BeginGrapple();
            var nam=mission.player.GetComponentInChildren<Animator>();
            namFeet=new Map01GrappleFeet(nam,mission.player,mission);enemyFeet=new Map01GrappleFeet(enemy.GetComponentInChildren<Animator>(),enemy.transform,mission);
            contact=new Map01GrappleContact(enemy.GetComponentInChildren<Animator>());
            var neutral=nam.avatar.humanDescription.skeleton.ToDictionary(b=>b.name,b=>b.rotation);
            fingerPoses=nam.GetComponentsInChildren<Transform>(true).Where(t=>t.name.Contains("Hand")&&new[]{"Thumb","Index","Middle","Ring","Pinky"}.Any(f=>t.name.Contains(f))&&neutral.ContainsKey(t.name))
                .Select(t=>(t,neutral[t.name],t.localRotation)).ToArray();
            namPose=new Map01CinematicPose(nam,cinematicAvatar);enemyPose=new Map01CinematicPose(enemy.GetComponentInChildren<Animator>());
            hugHands=new Map01HugHands(nam);
            GetComponent<Map01FoliageReveal>().ExtraFocus=enemy.transform;return true;
        }
        void Update() {
            if(!Active)return;
            if(Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame)PauseInteraction(!Paused);
            if(Paused||mission.Paused)return;
            float dt=Time.deltaTime;clock+=dt;
            if(CurrentPhase==Phase.Pull) {
                var previous=mission.player.position;
                float reach=Mathf.SmoothStep(0,1,clock/.85f),drag=Mathf.SmoothStep(0,1,(clock-GripAt)/(PullSeconds-GripAt));
                mission.player.position=clock<GripAt?Vector3.Lerp(startNam,grabNam,reach):Vector3.Lerp(grabNam,endNam,drag);
                if(clock<GripAt)approachTravel+=Vector3.Distance(previous,mission.player.position);
                // The guard remains at his real position until Nam has caught
                // him. Both then travel as a held pair, not as two vacuum tweens.
                Victim.transform.position=Vector3.Lerp(startEnemy,endEnemy,drag);
                mission.player.rotation=Quaternion.Slerp(initialNam,facing,reach);Victim.transform.rotation=Quaternion.Slerp(initialEnemy,facing,Mathf.SmoothStep(0,1,(clock-GripAt)/.8f));
                if(clock>=PullSeconds){CurrentPhase=Phase.Struggle;clock=0;}
            }else if(CurrentPhase==Phase.Struggle) {
                if(Mouse.current==null||!Mouse.current.leftButton.isPressed)armed=true;
                bool pressed=armed&&Mouse.current!=null&&Mouse.current.leftButton.wasPressedThisFrame;
                if(pressed)armed=false;
                Timing.Tick(dt,pressed);
                if(Timing.Complete){
                    CurrentPhase=Timing.Won?Phase.Finish:Phase.Escape;clock=0;
                    if(!Timing.Won){endNam=mission.player.position;endEnemy=Victim.transform.position;recoverNam=RecoveryStep(endNam,-(facing*Vector3.forward)*.28f);recoverEnemy=RecoveryStep(endEnemy,(facing*Vector3.forward)*.55f);}
                }
            }else if(CurrentPhase==Phase.Finish) {
                if(!hit&&clock>=StrikeAt){hit=true;Victim.GetComponent<Health>().CinematicInvulnerable=victimImmune;Victim.CommitBushKill();}
                if(clock>=FinishSeconds)End(true);
            }else{
                mission.player.position=Vector3.Lerp(endNam,recoverNam,Mathf.SmoothStep(0,1,(clock-.25f)/.95f));
                Victim.transform.position=Vector3.Lerp(endEnemy,recoverEnemy,Mathf.SmoothStep(0,1,(clock-.15f)/.95f));
                Victim.SetGrappleWeaponReady(clock>=1.05f);
                if(clock>=EscapeSeconds)End(false);
            }
        }
        Vector3 RecoveryStep(Vector3 origin,Vector3 delta){
            if(NavMesh.SamplePosition(origin+delta,out var hit,.35f,NavMesh.AllAreas)&&PullClear(origin,hit.position,Victim))return hit.position;
            return origin;
        }
        void LateUpdate() {
            if(!Active||Paused||mission.Paused)return;
            string suffix=CurrentPhase==Phase.Pull?"GrabPull":CurrentPhase==Phase.Struggle?"StruggleLoop":CurrentPhase==Phase.Finish?"Finish":"Escape";
            if(CurrentPhase==Phase.Escape){
                // Recovery clips were authored for the gameplay Avatar. Restore
                // its arm axes before blending out of the broken hold.
                namPose.RestoreGameplayAvatar();
                float recoveryBlend=Mathf.SmoothStep(0,1,(clock-.75f)/.65f);
                if(clock>=1.4f){
                    // Drop the escape stream completely: generic joint channels
                    // from that clip must not remain bound at zero mixer weight.
                    namPose.Sample(Map01CinematicPose.Clip("Nam_Bush_Recovery"),clock-.75f,true);
                    enemyPose.Sample(Map01CinematicPose.Clip("Enemy_Bush_Recovery"),clock-.75f,true);
                }else{
                    namPose.SampleBlend(Map01CinematicPose.Clip("Nam_Bush_Escape"),clock,Map01CinematicPose.Clip("Nam_Bush_Recovery"),clock-.75f,recoveryBlend);
                    enemyPose.SampleBlend(Map01CinematicPose.Clip("Enemy_Bush_Escape"),clock,Map01CinematicPose.Clip("Enemy_Bush_Recovery"),clock-.75f,recoveryBlend);
                }
            }else if(CurrentPhase==Phase.Pull){
                var approach=Map01CinematicPose.Clip("Nam_Bush_Approach");
                namPose.SampleBlend(approach,approachTravel/1.94f,Map01CinematicPose.Clip("Nam_Bush_GrabPull"),clock,Mathf.SmoothStep(0,1,(clock-.35f)/.8f));
                enemyPose.Sample(Map01CinematicPose.Clip("Enemy_Bush_GrabPull"),clock);
            }else{
                namPose.Sample(Map01CinematicPose.Clip("Nam_Bush_"+suffix),clock,CurrentPhase==Phase.Struggle);
                enemyPose.Sample(Map01CinematicPose.Clip("Enemy_Bush_"+suffix),clock,CurrentPhase==Phase.Struggle);
            }
            SampledPhaseTime=clock;
            namPose.Ground(mission.player.position.y);enemyPose.Ground(Victim.transform.position.y);
            if(CurrentPhase==Phase.Pull||CurrentPhase==Phase.Struggle){namFeet.Sample(Time.deltaTime);enemyFeet.Sample(Time.deltaTime);}
            // Closed fists are authored as generic phalange curves in these
            // paired clips only. The equipped knife stays stowed until handoff.
            if(knife!=null)knife.gameObject.SetActive(knifeWasVisible&&CurrentPhase==Phase.Escape&&clock>=1.35f);
            if(CurrentPhase==Phase.Struggle||CurrentPhase==Phase.Pull||CurrentPhase==Phase.Finish&&clock<1.65f)
                Contacts(CurrentPhase==Phase.Pull?Mathf.SmoothStep(0,1,(clock-.75f)/.4f):1);
            var target=(mission.player.position+Victim.transform.position)*.5f+Vector3.up*.9f;
            var desired=target+facing*cameraOffset;
            var offset=desired-target;
            var hits=Physics.SphereCastAll(target,.15f,offset.normalized,offset.magnitude,mission.ObstructionMask,QueryTriggerInteraction.Ignore)
                .Where(h=>h.transform.GetComponentInParent<Health>()==null).OrderBy(h=>h.distance).ToArray();
            if(hits.Length>0)desired=target+offset.normalized*Mathf.Max(.8f,hits[0].distance-.2f);
            float blend=Mathf.SmoothStep(0,1,CurrentPhase==Phase.Pull?clock/1f:1);
            var eye=Vector3.Lerp(cameraStart,desired,blend);
            camera.SetCinematicView(eye,Quaternion.Slerp(cameraRotation,Quaternion.LookRotation(target-eye),blend),Mathf.Lerp(cameraFov,48,blend),1);
        }
        bool ShotClear(Vector3 target,Vector3 position){
            var delta=position-target;
            return !Physics.SphereCastAll(target,.15f,delta.normalized,delta.magnitude,mission.ObstructionMask,QueryTriggerInteraction.Ignore)
                .Any(h=>h.transform.GetComponentInParent<Health>()==null);
        }
        void Contacts(float weight) {
            if(weight<=.0001f)return;
            var nam=mission.player.GetComponentInChildren<Animator>();var enemy=Victim.GetComponentInChildren<Animator>();
            float release=CurrentPhase==Phase.Finish?Mathf.SmoothStep(0,1,(clock-.95f)/.75f):0;
            float grip=weight*(1-release);
            if(grip<=.0001f)return;
            var spine=nam.GetBoneTransform(HumanBodyBones.Spine);var head=nam.GetBoneTransform(HumanBodyBones.Head);var headRotation=head.rotation;
            var leanAxis=Vector3.Cross(Vector3.up,Vector3.ProjectOnPlane(Victim.transform.position-mission.player.position,Vector3.up).normalized);
            spine.rotation=Quaternion.AngleAxis(6*grip,leanAxis)*spine.rotation;head.rotation=headRotation;
            ContactPalm(nam,true,EmbracePoint(enemy,Victim.transform,true),grip);
            ContactPalm(nam,false,EmbracePoint(enemy,Victim.transform),grip);
            // Both defending fists resist the actual forearms after retargeting.
            var left=Vector3.Lerp(nam.GetBoneTransform(HumanBodyBones.LeftLowerArm).position,nam.GetBoneTransform(HumanBodyBones.LeftHand).position,.62f);
            ContactArm(enemy,false,left+Victim.transform.forward*.015f-Vector3.up*.02f,grip,contact);
            var right=Vector3.Lerp(nam.GetBoneTransform(HumanBodyBones.RightLowerArm).position,nam.GetBoneTransform(HumanBodyBones.RightHand).position,.78f);
            ContactArm(enemy,true,right-Victim.transform.right*.025f+Vector3.up*.015f,grip,contact);
            HeadPenetration=0;
            foreach(var actor in new[]{nam,enemy})foreach(var side in new[]{true,false})
                HeadPenetration=Mathf.Max(HeadPenetration,contact.ArmPenetration(actor.GetBoneTransform(side?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm).position,actor.GetBoneTransform(side?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm).position,actor.GetBoneTransform(side?HumanBodyBones.LeftHand:HumanBodyBones.RightHand).position));
        }
        public static Vector3 EmbracePoint(Animator actor,Transform root,bool left=false)=>
            (actor.GetBoneTransform(HumanBodyBones.LeftUpperArm).position+actor.GetBoneTransform(HumanBodyBones.RightUpperArm).position)*.5f
            +root.forward*.12f+root.right*(left?-.12f:.16f)-Vector3.up*(left?.20f:.18f);
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Animator,Transform> fingers=new();
        static Transform MiddleFinger(Animator actor)=>fingers.GetValue(actor,a=>a.GetBoneTransform(HumanBodyBones.LeftMiddleProximal)??a.GetComponentsInChildren<Transform>(true).First(t=>t.name=="LeftHandMiddle1"));
        public static Vector3 PalmPoint(Animator actor)=>Vector3.Lerp(actor.GetBoneTransform(HumanBodyBones.LeftHand).position,MiddleFinger(actor).position,.6f);
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Animator,Transform> rightFingers=new();
        public static Vector3 RightPalmPoint(Animator actor)=>Vector3.Lerp(actor.GetBoneTransform(HumanBodyBones.RightHand).position,rightFingers.GetValue(actor,a=>a.GetBoneTransform(HumanBodyBones.RightMiddleProximal)??a.GetComponentsInChildren<Transform>(true).First(t=>t.name=="RightHandMiddle1")).position,.6f);
        void ContactPalm(Animator actor,bool left,Vector3 target,float weight){
            var hand=actor.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
            for(int i=0;i<3;i++)ContactArm(actor,left,target-((left?PalmPoint(actor):RightPalmPoint(actor))-hand.position),weight,contact);
        }
        static void ContactArm(Animator actor,bool left,Vector3 target,float weight,Map01GrappleContact obstacle=null){
            var hand=left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand;
            SolveArm(actor,left?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm,left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm,hand,
                Vector3.Lerp(actor.GetBoneTransform(hand).position,target,weight),left?-1:1,obstacle);
        }
        public static void SolveArm(Animator actor,HumanBodyBones upperId,HumanBodyBones lowerId,HumanBodyBones endId,Vector3 target,float side,Map01GrappleContact obstacle=null) {
            var a=actor.GetBoneTransform(upperId);var b=actor.GetBoneTransform(lowerId);var end=actor.GetBoneTransform(endId);
            var origin=a.position;float first=Vector3.Distance(origin,b.position),second=Vector3.Distance(b.position,end.position);
            var axis=(target-origin).normalized;float d=Mathf.Clamp(Vector3.Distance(origin,target),Mathf.Abs(first-second)+.001f,(first+second)*.98f);
            var pole=actor.transform.right*side+actor.transform.forward*.5f-actor.transform.up*.6f;
            var bend=Vector3.ProjectOnPlane(pole,axis).normalized;
            float x=(first*first+d*d-second*second)/(2*d),height=Mathf.Sqrt(Mathf.Max(0,first*first-x*x));
            var elbow=origin+axis*x+bend*height;var wristTarget=origin+axis*d;
            if(obstacle!=null){
                float best=float.PositiveInfinity;
                for(int n=0;n<25;n++){
                    float angle=n==0?0:((n+1)/2)*15*(n%2==0?-1:1);
                    var candidate=origin+axis*x+Quaternion.AngleAxis(angle,axis)*bend*height;
                    float score=obstacle.ArmPenetration(origin,candidate,wristTarget)*1000+Mathf.Abs(angle)*.0001f;
                    if(score<best){best=score;elbow=candidate;}
                    if(score<.00001f)break;
                }
            }
            var wrist=Quaternion.Inverse(b.rotation)*end.rotation;a.rotation=Quaternion.FromToRotation(b.position-origin,elbow-origin)*a.rotation;
            b.rotation=Quaternion.FromToRotation(end.position-b.position,wristTarget-b.position)*b.rotation;end.rotation=b.rotation*wrist;
        }
        void End(bool won,bool alarm=true) {
            if(!Active)return;var victim=Victim;CurrentPhase=Phase.None;contact?.Dispose();contact=null;hugHands?.Dispose();hugHands=null;
            namPose?.Dispose();enemyPose?.Dispose();namPose=enemyPose=null;
            namFeet=enemyFeet=null;
            if(fingerPoses!=null)foreach(var f in fingerPoses)if(f.bone!=null)f.bone.localRotation=f.previous;
            if(knife!=null)knife.gameObject.SetActive(knifeWasVisible);
            if(victim!=null){victim.GetComponent<Health>().CinematicInvulnerable=victimImmune;victim.EndGrapple(won);}
            if(mission==null||mission.player==null||mission.ModernPlayer==null||mission.ModernCombat==null)return;
            mission.Cinematic=false;mission.ModernHealth.CinematicInvulnerable=immune;mission.SetPaused(false);Paused=false;
            var cc=mission.player.GetComponent<CharacterController>();cc.enabled=characterEnabled;
            actions.enabled=actionsEnabled;mission.ModernPlayer.enabled=playerEnabled;mission.ModernCombat.enabled=combatEnabled;
            mission.ModernPlayer.RestoreMotion(wasSneaking);mission.ModernCombat.RestoreAfterCinematic();camera.ClearCinematicView();camera.ClearRecoil();
            var reveal=GetComponent<Map01FoliageReveal>();if(reveal!=null)reveal.ExtraFocus=null;
            Cursor.visible=false;Cursor.lockState=CursorLockMode.Locked;ForestMenu.SuppressKeysAfterCutscene();
            if(!won&&alarm&&victim!=null&&victim.Alive)victim.ReportGrappleEscape();
        }
        public void PauseInteraction(bool value){if(!Active)return;Paused=value;mission.SetPaused(value);Cursor.visible=value;Cursor.lockState=value?CursorLockMode.None:CursorLockMode.Locked;}
        public void RetryCheckpoint(){
            if(!Active)return;End(hit,false);var saves=GetComponent<Map01SaveSystem>();
            if(mission.Stage<=Map01Quest.EscortStage)saves.RestartRescue(mission.Stage==Map01Quest.EscortStage);
            else saves.RestartInteractionCheckpoint(interactionCheckpoint);
        }
        void OnDisable(){if(Active&&mission!=null)End(hit,false);}
        void OnGUI() {
            if(!Active)return;
            var style=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontSize=20,wordWrap=true};
            GUI.color=new Color(0,0,0,.8f);GUI.DrawTexture(new Rect(Screen.width*.22f,Screen.height*.77f,Screen.width*.56f,Screen.height*.16f),Texture2D.whiteTexture);GUI.color=Color.white;
            if(Paused) {
                GUI.Label(new Rect(Screen.width*.25f,Screen.height*.78f,Screen.width*.5f,30),"TẠM DỪNG GIẰNG CO",style);
                if(GUI.Button(new Rect(Screen.width/2-155,Screen.height*.84f,145,35),"Tiếp tục"))PauseInteraction(false);
                if(GUI.Button(new Rect(Screen.width/2+10,Screen.height*.84f,145,35),"Về checkpoint"))RetryCheckpoint();return;
            }
            GUI.Label(new Rect(Screen.width*.24f,Screen.height*.78f,Screen.width*.52f,32),CurrentPhase==Phase.Struggle?"CHUỘT TRÁI khi kim vào vùng sáng · "+Timing.Successes+"/3 · Lỗi "+Timing.Misses+"/2":"Kéo vào bụi — giữ im lặng",style);
            if(CurrentPhase!=Phase.Struggle)return;
            var bar=new Rect(Screen.width*.3f,Screen.height*.86f,Screen.width*.4f,18);GUI.color=new Color(.22f,.25f,.2f);GUI.DrawTexture(bar,Texture2D.whiteTexture);
            GUI.color=new Color(.65f,.9f,.35f);GUI.DrawTexture(new Rect(bar.x+(Timing.Center-.12f)*bar.width,bar.y,bar.width*.24f,bar.height),Texture2D.whiteTexture);
            GUI.color=Color.white;GUI.DrawTexture(new Rect(bar.x+Timing.Cursor*bar.width-2,bar.y-5,4,bar.height+10),Texture2D.whiteTexture);
        }
    }
}
