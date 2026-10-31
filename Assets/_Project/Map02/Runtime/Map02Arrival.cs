using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Audio;
using ShadowVale.Gameplay.Player;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ShadowVale.Map01
{
    /// <summary>
    /// Map 2's arrival by sampan: loading the village while the boat comes in, docking, and the party stepping
    /// ashore and up the landing steps, their feet planted on the hull, the bank and each stair tread.
    /// </summary>
    [DefaultExecutionOrder(90)]
    public sealed partial class Map02Arrival : MonoBehaviour
    {
        public enum Phase { Loading, Approaching, Mooring, Disembarking, Gameplay }
        public Phase CurrentPhase {get;private set;}
        public const float ApproachDuration=12, MoorDuration=4, ExitDuration=26;
        public Transform[] Passengers {get;private set;}
        public Vector3[] SpawnPoints {get;private set;}
        public readonly Dictionary<string,int> Inventory=new();
        public int Stones {get;private set;}
        public Map01Inventory.Layout CharacterBag {get;private set;}
        public float CharacterStamina {get;private set;}
        public string RifleAmmo {get;private set;} = "ammo_rifle";
        public float CinematicTime=>CurrentPhase==Phase.Approaching?clock:CurrentPhase==Phase.Mooring?ApproachDuration+clock:ApproachDuration+MoorDuration+clock;
        public bool IsPlaying => CurrentPhase!=Phase.Gameplay;
        Map01Extraction source;
        DialogueVoice speech;
        readonly List<Material> partyMaterials=new();
        readonly List<Mesh> boatMeshes=new();
        readonly List<MeshRenderer> boatRenderers=new();
        bool boatVisualsPrepared;
        Animator[] actors;
        Map01Extraction.PosePlayer[] poses;
        AnimationClip[] stand,step,walk,turn;
        AnimationClip stow;
        Vector3 paddleStart;Quaternion paddleStartRotation;
        Camera camera;
        ThirdPersonCamera rig;
        PlayerController player;
        PlayerCombat combat;
        Health health;
        Transform paddle;
        float clock,skipHeld,fade=1,groundHandoffRemaining;
        readonly bool[] ashore=new bool[3];
        readonly float[,] ankleOffsets=new float[3,2];
        readonly Quaternion[,] neutralToes=new Quaternion[3,2];
        readonly Quaternion[,] levelFeet=new Quaternion[3,2];
        readonly (Transform bone,Vector3 point)[,,] solePlane=new (Transform,Vector3)[3,2,2];
        readonly List<(Transform bone,Vector3 point)>[] shoeContacts=new List<(Transform,Vector3)>[3];
        readonly int[,] plantedCycles={{-999,-999},{-999,-999},{-999,-999}};
        readonly Vector3[,] plantedFeet=new Vector3[3,2];
        readonly Vector3[,] plantedNormals=new Vector3[3,2];
        bool released,paused;
        Map02Mission sceneMission;
#if UNITY_EDITOR
        bool shaderCompilationScoped,previousAsyncShaders,previousAllowAsyncShaders;
        void BeginShaderPreparation() {
            if(shaderCompilationScoped)return;
            previousAsyncShaders=UnityEditor.EditorSettings.asyncShaderCompilation;
            previousAllowAsyncShaders=UnityEditor.ShaderUtil.allowAsyncCompilation;
            shaderCompilationScoped=true;
            UnityEditor.EditorSettings.asyncShaderCompilation=false;
            UnityEditor.ShaderUtil.allowAsyncCompilation=false;
        }
        void EndShaderPreparation() {
            if(!shaderCompilationScoped)return;
            UnityEditor.EditorSettings.asyncShaderCompilation=previousAsyncShaders;
            UnityEditor.ShaderUtil.allowAsyncCompilation=previousAllowAsyncShaders;
            shaderCompilationScoped=false;
        }
#endif
        Vector3 paddlePark;
        Quaternion paddleParkRotation;
        GameObject landingSteps;
        Material wood;
        AudioSource rowingAudio;int lastStroke=-1;
        static readonly Quaternion DockRotation=Quaternion.Euler(0,-14.1f,0);
        // The sampan interior floor is -0.15 in hull space; keep it above the canal at 0.045.
        const float WaterlineRoot=.24f, StandDuration=3, PassengerDelay=3;
        const float InteriorFloor=-.15f, StairDuration=4.4f, LaneDuration=2.4f, WalkStride=1;
        // Ankle positions sit near each tread's outer edge: the toe extends
        // forward along -X and must not be placed inside the following riser.
        static readonly float[] StairX={-.55f,-.78f,-1.04f,-1.30f,-1.56f,-1.82f,-2.35f,-2.35f};
        static readonly float[] StairY={.15f,.41f-WaterlineRoot,.62f-WaterlineRoot,.83f-WaterlineRoot,1.04f-WaterlineRoot,1.25f-WaterlineRoot,1.25f-WaterlineRoot,1.25f-WaterlineRoot};
        static readonly Vector3 Docked=new(-11.346f,WaterlineRoot,-84.586f);
        static Vector3 DockPoint(Vector3 local)=>Docked+DockRotation*local;
        static readonly Vector3[] Route={new(-10.15f,WaterlineRoot,-93f),new(-10.45f,WaterlineRoot,-91.3f),new(-11.09f,WaterlineRoot,-89.49f),Docked};
        public void TakeParty(Map01Extraction extraction,Map01Mission mission) {
#if UNITY_EDITOR
            BeginShaderPreparation();
#endif
            source=extraction;camera=mission.gameCamera;rig=camera.GetComponent<ThirdPersonCamera>();
            Passengers=new[]{mission.player,extraction.CommanderRoot,mission.hung};
            actors=new[]{extraction.NamAnimator,extraction.CommanderAnimator,extraction.HungAnimator};
            player=mission.ModernPlayer;combat=mission.ModernCombat;health=mission.ModernHealth;
            foreach(var item in mission.GetComponent<Map01Inventory>().CaptureItems())Inventory[item.item_id]=item.count;
            Stones=mission.GetComponent<Map01Inventory>().Stones;
            CharacterBag=mission.GetComponent<Map01Inventory>().CaptureLayout();CharacterStamina=mission.Stamina;
            RifleAmmo=mission.GetComponent<Map01Inventory>().PrimaryAmmo;
            Inventory["ammo_rifle"]=mission.GetComponent<Map01Inventory>().Count("ammo_rifle");
            foreach(var badge in actors[1].GetComponentsInChildren<MeshRenderer>().Where(r=>r.name=="Commander insignia")) {
                var copy=new Material(badge.sharedMaterial);badge.sharedMaterial=copy;partyMaterials.Add(copy);
            }
            paddle=source.Paddle;
            PrepareBoatRendering();
            HideRowersRifle();
            for(int i=0;i<3;i++) {
                Passengers[i].SetParent(transform,true);
                Passengers[i].localPosition=new Vector3(0,-.13f,i==0?0:i==1?-1.6f:1.6f);
                Passengers[i].localRotation=Quaternion.Euler(0,i==1?0:270,0);
            }
            player.enabled=combat.enabled=false;
            Map01BoatCarry.For(actors[0]).Stow();
            var footsteps=Passengers[0].GetComponent<PlayerFootsteps>();if(footsteps!=null)footsteps.enabled=false;health.CinematicInvulnerable=true;
            var cc=Passengers[0].GetComponent<CharacterController>();if(cc!=null)cc.enabled=false;
            foreach(var actor in Passengers.SelectMany(p=>p.GetComponentsInChildren<MonoBehaviour>(true))) {
                // These Map 1 drivers keep references to the mission that is about to unload.
                if(actor is Map01HungVisual || actor is Map01NamActions || actor is Map01PlayerInteraction)actor.enabled=false;
            }
            foreach(var a in Passengers.SelectMany(p=>p.GetComponentsInChildren<NavMeshAgent>(true)))a.enabled=false;
            rig.InputAllowed=()=>!IsPlaying&&!paused;
            rig.SurfaceCameraFloor=null;
            player.InputAllowed=()=>!IsPlaying&&!paused;player.SurfaceSpeedMultiplier=1;player.SprintAllowed=()=>!paused;
            combat.InputAllowed=()=>!IsPlaying&&!paused;combat.TryConsumeRound=null;
            combat.ReserveRounds=()=>Inventory.TryGetValue(RifleAmmo,out int reserve)?reserve:0;
            combat.DrawRounds=requested=>{int n=Mathf.Min(requested,combat.ReserveRounds());Inventory[RifleAmmo]=combat.ReserveRounds()-n;return n;};
        }
        public IEnumerator LoadVillage() {

            yield return null;
            // The carried camera remains the listener while the village loads.
            // Switch off the village listeners below before rendering its first shot.
            SceneManager.sceneLoaded+=MuteVillageCameras;
            var load=SceneManager.LoadSceneAsync("Map 2");
            if(load==null){SceneManager.sceneLoaded-=MuteVillageCameras;Debug.LogError("Map 2 is missing from build settings.");yield break;}
            while(!load.isDone)yield return null;
            SceneManager.sceneLoaded-=MuteVillageCameras;

            SceneManager.MoveGameObjectToScene(gameObject,SceneManager.GetActiveScene());
            // Let the destination finish its first scene/GPU culling update while
            // the loading fade is still black, before registering moving geometry.
            yield return null;
            // Register fresh renderers in the destination scene, after Map 1's
            // unload callbacks and GPU instance cleanup have finished.
            PrepareBoatRendering(true);
            foreach(var other in FindObjectsByType<Camera>(FindObjectsSortMode.None))if(other!=camera) {
                other.enabled=false;other.tag="Untagged";
                var unusedRig=other.GetComponent<ThirdPersonCamera>();if(unusedRig!=null)unusedRig.enabled=false;
                var listener=other.GetComponent<AudioListener>();if(listener!=null)listener.enabled=false;
            }
            camera.tag="MainCamera";camera.GetComponent<AudioListener>().enabled=true;
            // Keep the village's other boat; the southern empty hull is replaced by the carried sampan.
            foreach(var item in FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if(item.name=="Canal boat" && item.position.z<0)item.gameObject.SetActive(false);
            var dock=FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).First(r=>r.name=="Timber evacuation landing" && r.bounds.center.z<0);
            wood=dock.sharedMaterial;
            BuildLandingSteps();
            SpawnPoints=new Vector3[3];
            for(int i=0;i<3;i++) {
                var candidate=new Vector3(-25,1,-86.2f+(i-1)*1.5f);
                if(!NavMesh.SamplePosition(candidate,out var hit,3,NavMesh.AllAreas))throw new InvalidOperationException("No dry arrival spawn on the village bank.");
                SpawnPoints[i]=hit.position;
                SpawnPoints[i].y=ShoreSurface(SpawnPoints[i]);
            }
            stand=new AnimationClip[3];step=new AnimationClip[3];walk=new AnimationClip[3];turn=new AnimationClip[3];poses=new Map01Extraction.PosePlayer[3];
            for(int i=0;i<3;i++) {
                string who=i==1?"Hung":"Nam";
                stand[i]=Resources.Load<AnimationClip>("Cutscenes/Map02/"+who+"_Boat_Stand");
                step[i]=Resources.Load<AnimationClip>("Cutscenes/Map02/"+who+"_Jetty_StepUp");
                walk[i]=Resources.Load<AnimationClip>("Cutscenes/Map02/"+who+"_Walk_Ashore");
                turn[i]=Resources.Load<AnimationClip>("Cutscenes/Map02/"+who+"_Boat_Turn");
                if(stand[i]==null || step[i]==null || walk[i]==null || turn[i]==null)throw new InvalidOperationException("Map 2 arrival clips have not been imported.");
                poses[i]=new Map01Extraction.PosePlayer(actors[i],i==2?source.hungClips:null);
                poses[i].Play(Resources.Load<AnimationClip>("Cutscenes/Map02/"+who+"_Standing_Guard"));
                PrepareBindFeet(i);
                PrepareShoeContacts(i);
                poses[i].Dispose();poses[i]=new Map01Extraction.PosePlayer(actors[i],i==2?source.hungClips:null);
                if(i==0)poses[i].FreeArms();
                poses[i].Play(i==1?Resources.Load<AnimationClip>("Cutscenes/Map02/Hung_Arrival_Row_Loop"):i==2?source.InjuredTravel:Resources.Load<AnimationClip>("Cutscenes/Map02/Nam_Arrival_Travel"));
                if(i==0)Map01BoatCarry.For(actors[i]).Stow();
            }
            HideRowersRifle();
            stow=Resources.Load<AnimationClip>("Cutscenes/Map02/Hung_Oar_Stow");LoadMisstep();
            // Park longitudinally inside the starboard gunwale, clear of the aisle and stairs.
            paddlePark=new Vector3(.44f,.18f,-2.4f);paddleParkRotation=Quaternion.identity;
            foreach(var ps in GetComponentsInChildren<ParticleSystem>()) {
                ps.transform.localPosition=new Vector3(0,.045f-WaterlineRoot,-2.8f);
                var main=ps.main;main.startLifetime=1.2f;main.startSpeed=.08f;main.startSize=.12f;main.startColor=new Color(.8f,.88f,.9f,.18f);
                var emission=ps.emission;emission.rateOverTime=5;ps.Play();
            }
            rowingAudio=GetComponent<AudioSource>();
            gameObject.name="Map 2 arrival boat";CurrentPhase=Phase.Approaching;clock=0;
            speech=DialogueVoice.For(gameObject);speech.Play("m2_arrival_sight");
            Cursor.lockState=CursorLockMode.None;Cursor.visible=false;
            ApplyArrival(0);yield return null;
        }
        void HideRowersRifle() => source.HidePassengerGuns();
        void Update() {
            if(CurrentPhase==Phase.Loading)return;
            if(CurrentPhase==Phase.Gameplay) {
                if(!chapterOwnsPassengers)for(int i=1;i<3;i++)poses[i]?.Tick(Time.deltaTime);
                if(sceneMission==null && Keyboard.current!=null && Keyboard.current.escapeKey.wasPressedThisFrame) {
                    paused=!paused;Time.timeScale=paused?0:1;Cursor.visible=paused;Cursor.lockState=paused?CursorLockMode.None:CursorLockMode.Locked;
                }
                return;
            }
            float dt=Mathf.Min(Time.unscaledDeltaTime,.05f);clock+=dt;fade=Mathf.Max(0,fade-dt/1.5f);
            foreach(var pose in poses)pose.Tick(dt);
            bool held=Keyboard.current!=null && Keyboard.current.escapeKey.isPressed;
            if(!held)released=true;skipHeld=held&&released?skipHeld+dt:0;
            if(skipHeld>=1){Skip();return;}
            if(CurrentPhase==Phase.Approaching) {
                ApplyArrival(Mathf.Clamp01(clock/ApproachDuration));
                int stroke=Mathf.FloorToInt(clock/2.4f);
                if(stroke!=lastStroke && rowingAudio!=null){lastStroke=stroke;rowingAudio.PlayOneShot(source.paddleAudio,speech!=null&&speech.HasSpeech?.045f:.12f);}
                if(clock>=ApproachDuration){CurrentPhase=Phase.Mooring;clock=0;speech.Play("m2_moor_order");poses[1].Play(stow??source.rowStop);paddleStart=paddle.localPosition;paddleStartRotation=paddle.localRotation;}
            } else if(CurrentPhase==Phase.Mooring) {
                transform.SetPositionAndRotation(Docked,DockRotation);
                if(clock>=MoorDuration){CurrentPhase=Phase.Disembarking;clock=0;speech.Play("m2_ashore_order");}
            } else {
                for(int i=0;i<3;i++)ExitPassenger(i,clock-i*PassengerDelay);
                float reveal=Mathf.SmoothStep(0,1,(clock-RevealStart)/8);
                Vector3 shotFrom=new Vector3(-5.5f,3.2f,-90.8f),shotOn=DockPoint(new Vector3(-1.2f,1.1f,0));
                if(MisstepCamera(out var landingEye,out var landingFocus,out float landing)){shotFrom=Vector3.Lerp(shotFrom,landingEye,landing);shotOn=Vector3.Lerp(shotOn,landingFocus,landing);}
                var pos=Vector3.Lerp(shotFrom,new Vector3(-8,5,-92),reveal);
                var focus=Vector3.Lerp(shotOn,new Vector3(-23,1.5f,-86.2f),reveal);
                float join=Mathf.SmoothStep(0,1,clock/1.1f);
                pos=Vector3.Lerp(Docked+new Vector3(6.2f,3.2f,-4.6f),pos,join);
                focus=Vector3.Lerp(Docked+Vector3.up*.8f,focus,join);
                rig.SetCinematicView(pos,Quaternion.LookRotation(focus-pos),48,1);
                if(clock>=ExitLength)Finish();
            }
        }
        void ApplyArrival(float progress) {
            float p=progress*progress*(3-2*progress)*(Route.Length-1);
            int k=Mathf.Min((int)p,Route.Length-2);var position=Vector3.Lerp(Route[k],Route[k+1],p-k);
            var heading=Quaternion.LookRotation(Route[k+1]-Route[k]);
            transform.SetPositionAndRotation(position,Quaternion.Slerp(heading,DockRotation,Mathf.SmoothStep(0,1,(progress-.7f)/.3f)));
            transform.rotation*=Quaternion.Euler(0,0,Mathf.Sin(clock*2.6f)*.5f*(1-progress));
            var focus=position+Vector3.up*.8f;
            var eye=Vector3.Lerp(position+new Vector3(5,3.4f,-4),Docked+new Vector3(6.2f,3.2f,-4.6f),progress);
            rig.SetCinematicView(eye,Quaternion.LookRotation(focus-eye),51,1);
        }
        void ExitPassenger(int index,float t) {
            if(t<0 || ashore[index])return;
            if(MisstepPassenger(index,t))return;
            var passenger=Passengers[index];var pose=poses[index];float seatLane=index==0?0:index==1?-1.6f:1.6f;
            float lane=Lane(index);
            float stairStart=index==0?StandDuration:StandDuration+LaneDuration;
            if(t<StandDuration) {
                pose.Play(stand[index]);pose.Rate(stand[index].length/StandDuration);
                // Hung rises facing the bow with his feet under him; turn only when taking the first step.
                passenger.localRotation=index==1?Quaternion.identity:Quaternion.Euler(0,270,0);
                passenger.localPosition=new Vector3(0,Mathf.Lerp(-.13f,InteriorFloor,Mathf.SmoothStep(0,1,t/StandDuration)),seatLane);
            } else if(t<stairStart) {
                float moveTime=t-StandDuration;
                var laneHeading=Quaternion.Euler(0,index==1?0:180,0);
                var initialHeading=index==1?Quaternion.identity:Quaternion.Euler(0,270,0);
                float laneProgress=Mathf.Clamp01((moveTime-.55f)/1.2f);
                passenger.localPosition=new Vector3(0,InteriorFloor,Mathf.Lerp(seatLane,lane,laneProgress));
                if(moveTime<.55f) {
                    pose.Play(turn[index]);pose.Rate(turn[index].length/.55f);
                    passenger.localRotation=Quaternion.Slerp(initialHeading,laneHeading,Mathf.SmoothStep(0,1,moveTime/.55f));
                } else if(moveTime<1.75f) {
                    pose.Play(walk[index]);pose.Rate(Mathf.Abs(seatLane-lane)/1.2f*walk[index].length/WalkStride);
                    passenger.localRotation=laneHeading;
                } else {
                    pose.Play(turn[index]);pose.Rate(turn[index].length/.65f);
                    passenger.localRotation=Quaternion.Slerp(laneHeading,Quaternion.Euler(0,270,0),Mathf.SmoothStep(0,1,(moveTime-1.75f)/.65f));
                }
            } else if(t<stairStart+StairDuration) {
                pose.Play(step[index]);pose.Rate(step[index].length/StairDuration);
                float p=(t-stairStart)/StairDuration;
                var center=(StairContact(p,0)+StairContact(p,1))*.5f;
                // Root follows the support feet, including the final gathering step on the landing.
                float supportHeight=(StairSupportHeight(p,0)+StairSupportHeight(p,1))*.5f;
                passenger.position=DockPoint(new Vector3(center.x,supportHeight,lane));
                passenger.rotation=DockRotation*Quaternion.Euler(0,270,0);
            } else {
                pose.Play(walk[index]);
                float p=Mathf.Clamp01((t-stairStart-StairDuration)/9f);
                var start=DockPoint(new Vector3(-2.35f,1.25f-WaterlineRoot,lane));
                pose.Rate(Vector3.Distance(start,SpawnPoints[index])/9f*walk[index].length/WalkStride);
                passenger.position=Vector3.Lerp(start,SpawnPoints[index],p);
                passenger.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(SpawnPoints[index]-start,Vector3.up));
                // The shore slopes gently; never follow the riverbed or an AABB approximation.
                if(Physics.Raycast(passenger.position+Vector3.up*2,Vector3.down,out var hit,5,~0,QueryTriggerInteraction.Ignore)
                    && !hit.transform.IsChildOf(transform) && hit.point.y>.5f)passenger.position=new Vector3(passenger.position.x,hit.point.y,passenger.position.z);
                if(p>=1){
                    pose.Play(Resources.Load<AnimationClip>("Cutscenes/Map02/"+(index==2?"Hung":"Nam")+"_Standing_Guard"));
                    GroundSkin(actors[index],SpawnPoints[index].y);ashore[index]=true;
                }
            }
        }
        void LateUpdate() {
            if(poses==null)return;
            if(CurrentPhase==Phase.Gameplay) {
                if(!chapterOwnsPassengers)for(int i=1;i<3;i++){poses[i]?.Sample();source.HoldSeatedRifle(actors[i],.16f);GroundStandingFeet(i);}
                // Aim and rifle carry use different idle poses. Keep the visual
                // soles planted while those poses settle immediately after handoff.
                var controller=Passengers[0].GetComponent<CharacterController>();
                if(groundHandoffRemaining>0) {
                    groundHandoffRemaining-=Mathf.Min(Time.deltaTime,.05f);
                    if(player.PlanarSpeed<.02f && controller!=null && controller.velocity.y<=.1f)GroundStandingFeet(0);
                    else groundHandoffRemaining=0;
                }
                return;
            }
            foreach(var pose in poses)pose.Sample();
            if(CurrentPhase==Phase.Approaching)source.ArrivalPaddleContacts(clock,1);
            else if(CurrentPhase==Phase.Mooring) {
                float p=Mathf.SmoothStep(0,1,clock/MoorDuration);
                // Raise the shaft clear of the knees, carry it beside the lap, then lay it across the gunwales.
                var carry=new Vector3(.35f,.65f,-1.70f);
                float q=1-p;
                paddle.localPosition=q*q*q*paddleStart+3*q*q*p*(paddleStart+Vector3.up*.18f)+3*q*p*p*carry+p*p*p*paddlePark;
                paddle.localRotation=Quaternion.Slerp(paddleStartRotation,paddleParkRotation,p);
                float leftRelease=Mathf.SmoothStep(0,1,(p-.50f)/.22f);
                float rightRelease=Mathf.SmoothStep(0,1,(p-.74f)/.23f);
                source.ArrivalStowContacts(1-leftRelease,1-rightRelease);source.SetPaddleGrip(1-leftRelease,1-rightRelease);
                source.ArrivalRestHands(actors[1],clock,leftRelease,rightRelease);
            }
            for(int i=0;i<1;i++) {
                // The C1 take owns Nam's hands while he removes the pack and supports Hùng.
                if(MisstepOwnsFeet(i)){if(i==0)SlingRifle();continue;}
                float rise=CurrentPhase==Phase.Disembarking?Mathf.Clamp01((clock-i*PassengerDelay)/StandDuration):0;
                Map01BoatCarry.For(actors[i]).Stow();
                if(CurrentPhase!=Phase.Disembarking)source.ArrivalRestHands(actors[i],clock);
            }
            // Authored hand support/release and arm swing must remain visible while rising/walking.
            if(CurrentPhase==Phase.Disembarking){source.SetPaddleGrip(0,0);if(clock<PassengerDelay)source.ArrivalRestHands(actors[1],clock);}
            // Injured arm stays supported throughout arrival; release to the chapter driver only at handoff.
            source.ApplyInjuredPassenger();
            if(CurrentPhase==Phase.Disembarking)for(int i=0;i<3;i++) {
                if(ashore[i])GroundStandingFeet(i);
                float t=clock-i*PassengerDelay;
                float stairStart=i==0?StandDuration:StandDuration+LaneDuration;
                if(t>=StandDuration && t<stairStart)for(int side=0;side<2;side++)LevelWalkingFoot(i,side,Vector3.up);
                if(MisstepOwnsFeet(i))continue;
                if(t>=stairStart && t<=stairStart+StairDuration)PlantStairFeet(i,(t-stairStart)/StairDuration);
                if(t>stairStart+StairDuration && !ashore[i])PlantShoreFeet(i);
            }
            MisstepContacts();
            SampleApprovedVisuals();
        }
        public void Skip(){if(CurrentPhase!=Phase.Loading && CurrentPhase!=Phase.Gameplay)Finish();}
        bool chapterOwnsPassengers;
        public void ReleasePassengerAnimation()
        {
            if(IsPlaying||chapterOwnsPassengers)return;chapterOwnsPassengers=true;
            for(int i=1;i<3;i++){poses[i]?.Dispose();poses[i]=null;}
        }
        void Finish() {
            if(CurrentPhase==Phase.Gameplay)return;
            speech?.Stop();source.SetPaddleGrip(0,0);EndMisstep();
            transform.SetPositionAndRotation(Docked,DockRotation);
            for(int i=0;i<3;i++) {
                poses[i]?.Dispose();
                Passengers[i].SetParent(null,true);Passengers[i].SetPositionAndRotation(SpawnPoints[i],Quaternion.Euler(0,0,0));
                actors[i].Rebind();actors[i].Update(0);
                poses[i]=null;
                if(i>0) {
                    poses[i]=new Map01Extraction.PosePlayer(actors[i]);
                    poses[i].Play(Resources.Load<AnimationClip>("Cutscenes/Map02/"+(i==2?"Hung":"Nam")+"_Standing_Guard"));
                }
                GroundSkin(actors[i],SpawnPoints[i].y);
                // Nam's weapon belongs to PlayerCombat and its gameplay grip configuration.
                if(i==1){var gun=Map01Rifle.Attach(actors[i]);if(gun!=null)gun.weapon.gameObject.SetActive(true);}
            }
            camera.transform.SetParent(null,true);
            paddle.localPosition=paddlePark;paddle.localRotation=paddleParkRotation;
            foreach(var ps in GetComponentsInChildren<ParticleSystem>())ps.Stop();
            foreach(var audio in GetComponentsInChildren<AudioSource>())audio.Stop();
            CurrentPhase=Phase.Gameplay;groundHandoffRemaining=1;health.CinematicInvulnerable=false;
            sceneMission=FindFirstObjectByType<Map02Mission>();
            var footsteps=Passengers[0].GetComponent<PlayerFootsteps>();if(footsteps!=null)footsteps.enabled=true;
            var cc=Passengers[0].GetComponent<CharacterController>();if(cc!=null){cc.enabled=true;cc.Move(Vector3.down*.08f);}
            Map01BoatCarry.For(actors[0]).Restore();player.enabled=combat.enabled=true;player.RestoreMotion(false);combat.RestoreAfterCinematic();GroundSkin(actors[0],SpawnPoints[0].y);
            rig.SetTarget(Passengers[0]);rig.ClearCinematicView();rig.ClearRecoil();
            Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;ForestMenu.SuppressKeysAfterCutscene();fade=0;
            Debug.Log("Map 2 arrival complete. Party on dry bank; inventory, health and magazine retained.");
#if UNITY_EDITOR
            EndShaderPreparation();
#endif
        }
        static void GroundSkin(Animator actor,float floor) {
            float sole=float.PositiveInfinity;
            foreach(var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>()) {
                var mesh=new Mesh();skin.BakeMesh(mesh);
                foreach(var vertex in mesh.vertices)sole=Mathf.Min(sole,skin.transform.TransformPoint(vertex).y);
                Destroy(mesh);
            }
            if(!float.IsInfinity(sole))actor.transform.position+=Vector3.up*(floor-sole);
        }
        void OnGUI() {
            if(CurrentPhase!=Phase.Gameplay) {
                GUI.color=Color.black;GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height*.08f),Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(0,Screen.height*.86f,Screen.width,Screen.height*.14f),Texture2D.whiteTexture);
                GUI.color=Color.white;GUI.Label(new Rect(Screen.width-280,12,270,35),"Hold Esc for 1 second to skip");
                string line=DialogueVoice.Caption(CurrentPhase==Phase.Approaching?"m2_arrival_sight":CurrentPhase==Phase.Mooring?"m2_moor_order":"m2_ashore_order");
                GUI.Label(new Rect(0,Screen.height*.9f,Screen.width,40),line,new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontSize=18});
                if(fade>0){GUI.color=new Color(0,0,0,fade);GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);GUI.color=Color.white;}
            } else {
                if(sceneMission!=null)return;
                GUI.Label(new Rect(20,20,430,60),paused?"Paused — Esc to resume":"MAP 2 — VILLAGE ARRIVAL\nReach the village command post.");
                if(paused && GUI.Button(new Rect(Screen.width/2-100,Screen.height/2,200,40),"Return to main menu")) {
                    Time.timeScale=1;SceneManager.LoadScene("01_MainMenu");
                }
            }
        }
        void OnDestroy(){
            SceneManager.sceneLoaded-=MuteVillageCameras;
            EndMisstep();
#if UNITY_EDITOR
            EndShaderPreparation();
#endif
            foreach(var mesh in boatMeshes)if(mesh!=null)Destroy(mesh);
            foreach(var material in partyMaterials)if(material!=null)Destroy(material);if(poses!=null)foreach(var pose in poses)pose?.Dispose();Time.timeScale=1;}
    }
}
