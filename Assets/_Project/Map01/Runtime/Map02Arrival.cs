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
    [DefaultExecutionOrder(90)]
    public sealed class Map02Arrival : MonoBehaviour
    {
        public enum Phase { Loading, Approaching, Mooring, Disembarking, Gameplay }
        public Phase CurrentPhase {get;private set;}
        public const float ApproachDuration=12, MoorDuration=4, ExitDuration=26;
        public Transform[] Passengers {get;private set;}
        public Vector3[] SpawnPoints {get;private set;}
        public readonly Dictionary<string,int> Inventory=new();
        public int Stones {get;private set;}
        public float CinematicTime=>CurrentPhase==Phase.Approaching?clock:CurrentPhase==Phase.Mooring?ApproachDuration+clock:ApproachDuration+MoorDuration+clock;
        public bool IsPlaying => CurrentPhase!=Phase.Gameplay;
        Map01Extraction source;
        DialogueVoice speech;
        readonly List<Material> partyMaterials=new();
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
        readonly List<(Transform bone,Vector3 point)>[] shoeContacts=new List<(Transform,Vector3)>[3];
        readonly int[,] plantedCycles={{-999,-999},{-999,-999},{-999,-999}};
        readonly Vector3[,] plantedFeet=new Vector3[3,2];
        bool released,paused;
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
            Inventory["ammo_rifle"]=mission.GetComponent<Map01Inventory>().Count("ammo_rifle");
            foreach(var badge in actors[1].GetComponentsInChildren<MeshRenderer>().Where(r=>r.name=="Commander insignia")) {
                var copy=new Material(badge.sharedMaterial);badge.sharedMaterial=copy;partyMaterials.Add(copy);
            }
            paddle=source.Paddle;
            PrepareBoatRendering();
            HideRowersRifle();
            for(int i=0;i<3;i++) {
                Passengers[i].SetParent(transform,true);
                Passengers[i].localPosition=new Vector3(0,-.13f,i==0?0:i==1?1.6f:-1.6f);
                Passengers[i].localRotation=Quaternion.Euler(0,i==2?0:270,0);
            }
            player.enabled=combat.enabled=false;
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
            combat.ReserveRounds=()=>Inventory.TryGetValue("ammo_rifle",out int reserve)?reserve:0;
            combat.DrawRounds=requested=>{int n=Mathf.Min(requested,combat.ReserveRounds());Inventory["ammo_rifle"]=combat.ReserveRounds()-n;return n;};
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
            // The hull changes scene as well as transform. Re-register only this
            // small moving prop; leave the village's GPU batching untouched.
            PrepareBoatRendering();
            foreach(var other in FindObjectsByType<Camera>(FindObjectsSortMode.None))if(other!=camera) {
                other.enabled=false;other.tag="Untagged";
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
                string who=i==2?"Hung":"Nam";
                stand[i]=Resources.Load<AnimationClip>("Cutscenes/Map02/"+who+"_Boat_Stand");
                step[i]=Resources.Load<AnimationClip>("Cutscenes/Map02/"+who+"_Jetty_StepUp");
                walk[i]=Resources.Load<AnimationClip>("Cutscenes/Map02/"+who+"_Walk_Ashore");
                turn[i]=Resources.Load<AnimationClip>("Cutscenes/Map02/"+who+"_Boat_Turn");
                if(stand[i]==null || step[i]==null || walk[i]==null || turn[i]==null)throw new InvalidOperationException("Map 2 arrival clips have not been imported.");
                poses[i]=new Map01Extraction.PosePlayer(actors[i],i==2?source.hungClips:null);
                poses[i].Play(Resources.Load<AnimationClip>("Cutscenes/Map02/"+who+"_Standing_Guard"));
                for(int side=0;side<2;side++) {
                    var foot=actors[i].GetBoneTransform(side==0?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);
                    FlattenFoot(foot);
                    var toes=actors[i].GetBoneTransform(side==0?HumanBodyBones.LeftToes:HumanBodyBones.RightToes);
                    if(toes!=null)neutralToes[i,side]=toes.localRotation;
                }
                PrepareShoeContacts(i);
                poses[i].Dispose();poses[i]=new Map01Extraction.PosePlayer(actors[i],i==2?source.hungClips:null);
                poses[i].Play(Resources.Load<AnimationClip>("Cutscenes/Map02/"+who+(i==2?"_Arrival_Row_Loop":"_Arrival_Travel")));
                if(i!=2)Map01Rifle.Attach(actors[i]).weapon.gameObject.SetActive(true);
            }
            HideRowersRifle();
            stow=Resources.Load<AnimationClip>("Cutscenes/Map02/Hung_Oar_Stow");
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
        void HideRowersRifle() {
            // Loading a completed Map 1 checkpoint bypasses StartDeparture,
            // which used to be the only place that hid Hung's rifle.
            foreach(var weapon in actors[2].GetComponentsInChildren<Weapon>(true))
                if(weapon.IsGun)weapon.gameObject.SetActive(false);
        }
        void PrepareBoatRendering() {
            foreach(var mesh in GetComponentsInChildren<MeshFilter>(true)) {
                if(mesh.name!="Moored wooden sampan"&&mesh.name!="Boat bench"&&!mesh.name.StartsWith("Oar "))continue;
                var renderer=mesh.GetComponent<MeshRenderer>();if(renderer==null||mesh.sharedMesh==null)continue;
                mesh.gameObject.SetActive(true);renderer.enabled=true;renderer.forceRenderingOff=false;
                // A per-renderer block keeps this moving cargo out of scene-bound
                // GPU Resident Drawer data across the unload/load boundary. Keep
                // its original timber colour and all existing property overrides.
                var properties=new MaterialPropertyBlock();renderer.GetPropertyBlock(properties);
                var material=renderer.sharedMaterial;
                if(material!=null&&material.HasProperty("_BaseColor")&&!properties.HasColor("_BaseColor"))
                    properties.SetColor("_BaseColor",material.GetColor("_BaseColor"));
                renderer.SetPropertyBlock(properties);
            }
        }
        void MuteVillageCameras(Scene scene,LoadSceneMode mode) {
            foreach(var root in scene.GetRootGameObjects())foreach(var other in root.GetComponentsInChildren<Camera>(true))if(other!=camera) {
                other.enabled=false;other.tag="Untagged";
                var listener=other.GetComponent<AudioListener>();if(listener!=null)listener.enabled=false;
            }
        }
        void BuildLandingSteps() {
            landingSteps=new GameObject("Map 2 arrival timber stairs");
            landingSteps.transform.SetParent(transform.parent,false);
            for(int i=0;i<5;i++) {
                var tread=GameObject.CreatePrimitive(PrimitiveType.Cube);tread.name="Arrival tread "+(i+1);
                tread.transform.SetParent(landingSteps.transform,false);
                float top=.41f+i*.21f;
                tread.transform.position=DockPoint(new Vector3(-.95f-i*.26f,top-.08f-WaterlineRoot,0));
                tread.transform.localScale=new Vector3(.28f,.16f,2.5f);
                tread.transform.rotation=DockRotation;
                tread.GetComponent<Renderer>().sharedMaterial=wood;
            }
        }
        void Update() {
            if(CurrentPhase==Phase.Loading)return;
            if(CurrentPhase==Phase.Gameplay) {
                for(int i=1;i<3;i++)poses[i]?.Tick(Time.deltaTime);
                if(Keyboard.current!=null && Keyboard.current.escapeKey.wasPressedThisFrame) {
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
                if(stroke!=lastStroke && rowingAudio!=null){lastStroke=stroke;rowingAudio.PlayOneShot(source.paddleAudio,.35f);}
                if(clock>=ApproachDuration){CurrentPhase=Phase.Mooring;clock=0;speech.Play("m2_moor_order");poses[2].Play(stow??source.rowStop);paddleStart=paddle.localPosition;paddleStartRotation=paddle.localRotation;}
            } else if(CurrentPhase==Phase.Mooring) {
                transform.SetPositionAndRotation(Docked,DockRotation);
                if(clock>=MoorDuration){CurrentPhase=Phase.Disembarking;clock=0;speech.Play("m2_ashore_order");}
            } else {
                for(int i=0;i<3;i++)ExitPassenger(i,clock-i*PassengerDelay);
                float reveal=Mathf.SmoothStep(0,1,(clock-7)/8);
                var pos=Vector3.Lerp(new Vector3(-5.5f,3.2f,-90.8f),new Vector3(-8,5,-92),reveal);
                var focus=Vector3.Lerp(DockPoint(new Vector3(-1.2f,1.1f,0)),new Vector3(-23,1.5f,-86.2f),reveal);
                float join=Mathf.SmoothStep(0,1,clock/1.1f);
                pos=Vector3.Lerp(Docked+new Vector3(6.2f,3.2f,-4.6f),pos,join);
                focus=Vector3.Lerp(Docked+Vector3.up*.8f,focus,join);
                rig.SetCinematicView(pos,Quaternion.LookRotation(focus-pos),48,1);
                if(clock>=ExitDuration)Finish();
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
            var passenger=Passengers[index];var pose=poses[index];float seatLane=index==0?0:index==1?1.6f:-1.6f;
            float lane=index==0?0:index==1?.8f:-.8f;
            float stairStart=index==0?StandDuration:StandDuration+LaneDuration;
            if(t<StandDuration) {
                pose.Play(stand[index]);pose.Rate(stand[index].length/StandDuration);
                // Hung rises facing the bow with his feet under him; turn only when taking the first step.
                passenger.localRotation=index==2?Quaternion.identity:Quaternion.Euler(0,270,0);
                passenger.localPosition=new Vector3(0,Mathf.Lerp(-.13f,InteriorFloor,Mathf.SmoothStep(0,1,t/StandDuration)),seatLane);
            } else if(t<stairStart) {
                float moveTime=t-StandDuration;
                var laneHeading=Quaternion.Euler(0,index==1?180:0,0);
                var initialHeading=index==2?Quaternion.identity:Quaternion.Euler(0,270,0);
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
                for(int i=1;i<3;i++){poses[i]?.Sample();source.HoldSeatedRifle(actors[i],.16f);GroundStandingFeet(i);}
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
                source.ArrivalStowContacts(1-leftRelease,1-rightRelease);
                source.ArrivalRestHands(actors[2],clock,leftRelease,rightRelease);
            }
            for(int i=0;i<2;i++) {
                float rise=CurrentPhase==Phase.Disembarking?Mathf.Clamp01((clock-i*PassengerDelay)/StandDuration):0;
                source.HoldSeatedRifle(actors[i],Mathf.Lerp(.32f,.16f,rise));
            }
            // Authored hand support/release and arm swing must remain visible while rising/walking.
            if(CurrentPhase==Phase.Disembarking && clock<2*PassengerDelay)source.ArrivalRestHands(actors[2],clock);
            if(CurrentPhase==Phase.Disembarking)for(int i=0;i<3;i++) {
                if(ashore[i])GroundStandingFeet(i);
                float t=clock-i*PassengerDelay;
                float stairStart=i==0?StandDuration:StandDuration+LaneDuration;
                if(t>=stairStart && t<=stairStart+StairDuration)PlantStairFeet(i,(t-stairStart)/StairDuration);
                if(t>stairStart+StairDuration && !ashore[i])PlantShoreFeet(i);
            }
        }
        float ShoreSurface(Vector3 position) {
            var hit=Physics.RaycastAll(position+Vector3.up*1.5f,Vector3.down,3,~0,QueryTriggerInteraction.Ignore)
                .Where(h=>h.point.y>.5f && h.normal.y>.5f && !h.transform.IsChildOf(transform) && !Passengers.Any(p=>h.transform.IsChildOf(p)))
                .OrderBy(h=>h.distance).FirstOrDefault();
            return hit.collider!=null?hit.point.y:position.y;
        }
        void GroundStandingFeet(int index) {
            float sole=float.PositiveInfinity;
            for(int side=0;side<2;side++){
                var foot=actors[index].GetBoneTransform(side==0?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);
                FlattenFoot(foot);
            }
            foreach(var sample in shoeContacts[index])if(sample.bone!=null)sole=Mathf.Min(sole,sample.bone.TransformPoint(sample.point).y);
            if(!float.IsInfinity(sole))actors[index].transform.position+=Vector3.up*(SpawnPoints[index].y-sole);
        }
        void PrepareShoeContacts(int index) {
            var feet=new[]{actors[index].GetBoneTransform(HumanBodyBones.LeftFoot),actors[index].GetBoneTransform(HumanBodyBones.RightFoot)};
            var toes=new[]{actors[index].GetBoneTransform(HumanBodyBones.LeftToes),actors[index].GetBoneTransform(HumanBodyBones.RightToes)};
            shoeContacts[index]=new List<(Transform,Vector3)>();
            var soles=new[]{float.PositiveInfinity,float.PositiveInfinity};
            foreach(var skin in actors[index].GetComponentsInChildren<SkinnedMeshRenderer>()) {
                var mesh=new Mesh();skin.BakeMesh(mesh);
                foreach(var vertex in mesh.vertices){
                    var point=skin.transform.TransformPoint(vertex);
                    float left=Vector3.ProjectOnPlane(point-feet[0].position,Vector3.up).sqrMagnitude;
                    float right=Vector3.ProjectOnPlane(point-feet[1].position,Vector3.up).sqrMagnitude;
                    int side=left<right?0:1;
                    if(Mathf.Min(left,right)>.09f || point.y>feet[side].position.y+.03f)continue;
                    soles[side]=Mathf.Min(soles[side],point.y);
                    var bone=toes[side]!=null && (point-toes[side].position).sqrMagnitude<(point-feet[side].position).sqrMagnitude?toes[side]:feet[side];
                    shoeContacts[index].Add((bone,bone.InverseTransformPoint(point)));
                }
                Destroy(mesh);
            }
            for(int side=0;side<2;side++)ankleOffsets[index,side]=float.IsInfinity(soles[side])?.10f:Mathf.Clamp(feet[side].position.y-soles[side],.03f,.24f);
            if(shoeContacts[index].Count==0)for(int side=0;side<2;side++)shoeContacts[index].Add((feet[side],feet[side].InverseTransformPoint(feet[side].position-Vector3.up*ankleOffsets[index,side])));
        }
        void PlantShoreFeet(int index) {
            float cycles=(float)poses[index].Time/Mathf.Max(.1f,walk[index].length);
            for(int side=0;side<2;side++) {
                float phase=cycles+side*.5f;int cycle=Mathf.FloorToInt(phase);
                float contactPhase=phase-cycle;
                if(contactPhase>=.55f)continue;
                var foot=actors[index].GetBoneTransform(side==0?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);
                if(plantedCycles[index,side]!=cycle) {
                    var point=foot.position;
                    var hit=Physics.RaycastAll(point+Vector3.up*1.5f,Vector3.down,3,~0,QueryTriggerInteraction.Ignore)
                        .Where(h=>h.point.y>.5f && !h.transform.IsChildOf(transform) && !Passengers.Any(p=>h.transform.IsChildOf(p)))
                        .OrderBy(h=>h.distance).FirstOrDefault();
                    point.y=(hit.collider!=null?hit.point.y:Passengers[index].position.y)+ankleOffsets[index,side];
                    plantedFeet[index,side]=point;plantedCycles[index,side]=cycle;
                }
                // Blend contact at heel strike/toe off instead of snapping across the swing pose.
                float weight=Mathf.Min(Mathf.SmoothStep(0,1,contactPhase/.07f),Mathf.SmoothStep(0,1,(.55f-contactPhase)/.07f));
                var target=Vector3.Lerp(foot.position,plantedFeet[index,side],weight);
                FlattenFoot(foot);SolveLeg(actors[index],side==0,target);
            }
        }
        public static Vector2 StairContact(float progress,int side) {
            float cycle=Mathf.Clamp(progress,0,.999999f)*StairX.Length;
            int active=(int)cycle;float fraction=cycle-active;
            int completed=active-1;if(completed%2!=side)completed--;
            var from=completed<0?new Vector2(0,InteriorFloor):new Vector2(StairX[completed],StairY[completed]);
            if(active%2!=side)return from;
            var to=new Vector2(StairX[active],StairY[active]);
            // Clear the riser before translating the shoe. A diagonal ankle
            // arc let the toe clip the next tread, especially on Hung's sandals.
            float advance=StepEase((fraction-.22f)/.56f);
            float clearance=Mathf.Max(from.y,to.y)+.14f;
            float height=fraction<.30f?Mathf.Lerp(from.y,clearance,StepEase(fraction/.30f)):
                fraction>.76f?Mathf.Lerp(clearance,to.y,StepEase((fraction-.76f)/.24f)):clearance;
            return new Vector2(Mathf.Lerp(from.x,to.x,advance),height);
        }
        static float StepEase(float value) {
            float p=Mathf.Clamp01(value);return p*p*p*(10+p*(-15+6*p));
        }
        static float StairSupportHeight(float progress,int side) {
            float cycle=Mathf.Clamp(progress,0,.999999f)*StairX.Length;
            int active=(int)cycle,completed=active-1;if(completed%2!=side)completed--;
            float from=completed<0?InteriorFloor:StairY[completed];
            return active%2==side?Mathf.Lerp(from,StairY[active],StepEase(cycle-active)):from;
        }
        void PlantStairFeet(int index,float progress) {
            float lane=index==0?0:index==1?.8f:-.8f;
            var targets=new Vector3[2];float pelvisDrop=0;
            for(int side=0;side<2;side++) {
                var hips=actors[index].GetBoneTransform(HumanBodyBones.Hips);
                var upper=actors[index].GetBoneTransform(side==0?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg);
                float lateral=Mathf.Sign(Vector3.Dot(upper.position-hips.position,DockRotation*Vector3.forward))*.13f;
                var point=StairContact(progress,side);var contact=new Vector3(point.x,point.y,lane+lateral);
                contact.y+=ankleOffsets[index,side];
                targets[side]=DockPoint(contact);
                var lower=actors[index].GetBoneTransform(side==0?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg);
                var foot=actors[index].GetBoneTransform(side==0?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);
                float reach=(Vector3.Distance(upper.position,lower.position)+Vector3.Distance(lower.position,foot.position))*.995f;
                float horizontal=Vector3.ProjectOnPlane(upper.position-targets[side],Vector3.up).magnitude;
                float allowed=targets[side].y+Mathf.Sqrt(Mathf.Max(.000001f,reach*reach-horizontal*horizontal));
                pelvisDrop=Mathf.Max(pelvisDrop,upper.position.y-allowed);
            }
            // During the turn-to-stair blend the outgoing pose can put the hips
            // above leg reach. Transfer weight down before solving both contacts.
            actors[index].GetBoneTransform(HumanBodyBones.Hips).position-=Vector3.up*(pelvisDrop+.0001f);
            for(int side=0;side<2;side++) {
                var foot=actors[index].GetBoneTransform(side==0?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);
                var toes=actors[index].GetBoneTransform(side==0?HumanBodyBones.LeftToes:HumanBodyBones.RightToes);
                if(toes!=null)toes.localRotation=neutralToes[index,side];
                FlattenFoot(foot);
                // Retargeted feet can retain the outgoing turn's yaw. Point
                // both shoes along the stairs while preserving their sole plane.
                if(toes!=null) {
                    var toeDirection=Vector3.ProjectOnPlane(toes.position-foot.position,Vector3.up);
                    if(toeDirection.sqrMagnitude>.00001f)
                        foot.rotation=Quaternion.FromToRotation(toeDirection,DockRotation*Vector3.left)*foot.rotation;
                }
                SolveLeg(actors[index],side==0,targets[side]);
            }
        }
        static void FlattenFoot(Transform foot) {
            if(foot.childCount==0)return;
            var direction=foot.GetChild(0).position-foot.position;
            var flat=Vector3.ProjectOnPlane(direction,Vector3.up);
            if(flat.sqrMagnitude>.00001f)foot.rotation=Quaternion.FromToRotation(direction,flat)*foot.rotation;
        }
        static float MeasureAnkleOffset(Animator actor,Transform foot) {
            var descendants=new HashSet<Transform>(foot.GetComponentsInChildren<Transform>());
            float sole=float.PositiveInfinity;
            foreach(var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>()) {
                if(!skin.sharedMesh.isReadable) {
                    // BakeMesh is readable even when the original imported mesh
                    // is not. Measure the neutral shoe region without changing it.
                    var neutralMesh=new Mesh();skin.BakeMesh(neutralMesh);
                    foreach(var v in neutralMesh.vertices){
                        var point=skin.transform.TransformPoint(v);
                        if(Vector3.ProjectOnPlane(point-foot.position,Vector3.up).sqrMagnitude<.09f)sole=Mathf.Min(sole,point.y);
                    }
                    Destroy(neutralMesh);continue;
                }
                var indexes=skin.bones.Select((bone,i)=>(bone,i)).Where(p=>descendants.Contains(p.bone)).Select(p=>p.i).ToHashSet();
                if(indexes.Count==0)continue;
                var mesh=new Mesh();skin.BakeMesh(mesh);var vertices=mesh.vertices;var weights=skin.sharedMesh.boneWeights;
                for(int i=0;i<weights.Length;i++) {
                    var w=weights[i];float influence=(indexes.Contains(w.boneIndex0)?w.weight0:0)+(indexes.Contains(w.boneIndex1)?w.weight1:0)
                        +(indexes.Contains(w.boneIndex2)?w.weight2:0)+(indexes.Contains(w.boneIndex3)?w.weight3:0);
                    if(influence>.7f)sole=Mathf.Min(sole,skin.transform.TransformPoint(vertices[i]).y);
                }
                Destroy(mesh);
            }
            return !float.IsInfinity(sole)?Mathf.Clamp(foot.position.y-sole,.03f,.24f):.10f;
        }
        static void SolveLeg(Animator actor,bool left,Vector3 target) {
            var upper=actor.GetBoneTransform(left?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg);
            var lower=actor.GetBoneTransform(left?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg);
            var foot=actor.GetBoneTransform(left?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);
            var origin=upper.position;float a=Vector3.Distance(origin,lower.position),b=Vector3.Distance(lower.position,foot.position);
            var axis=(target-origin).normalized;float d=Mathf.Clamp(Vector3.Distance(origin,target),Mathf.Abs(a-b)+.001f,(a+b)*.995f);
            var bend=Vector3.ProjectOnPlane(actor.transform.forward,axis).normalized;
            float along=(a*a+d*d-b*b)/(2*d);
            var knee=origin+axis*along+bend*Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
            var ankleRotation=foot.rotation;
            upper.rotation=Quaternion.FromToRotation(lower.position-origin,knee-origin)*upper.rotation;
            lower.rotation=Quaternion.FromToRotation(foot.position-lower.position,origin+axis*d-lower.position)*lower.rotation;
            foot.rotation=ankleRotation;
        }
        public void Skip(){if(CurrentPhase!=Phase.Loading && CurrentPhase!=Phase.Gameplay)Finish();}
        void Finish() {
            if(CurrentPhase==Phase.Gameplay)return;
            speech?.Stop();
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
                if(i>0){var gun=Map01Rifle.Attach(actors[i]);if(gun!=null)gun.weapon.gameObject.SetActive(true);}
            }
            camera.transform.SetParent(null,true);
            paddle.localPosition=paddlePark;paddle.localRotation=paddleParkRotation;
            foreach(var ps in GetComponentsInChildren<ParticleSystem>())ps.Stop();
            foreach(var audio in GetComponentsInChildren<AudioSource>())audio.Stop();
            CurrentPhase=Phase.Gameplay;groundHandoffRemaining=1;health.CinematicInvulnerable=false;
            var footsteps=Passengers[0].GetComponent<PlayerFootsteps>();if(footsteps!=null)footsteps.enabled=true;
            var cc=Passengers[0].GetComponent<CharacterController>();if(cc!=null){cc.enabled=true;cc.Move(Vector3.down*.08f);}
            player.enabled=combat.enabled=true;player.RestoreMotion(false);combat.RestoreAfterCinematic();GroundSkin(actors[0],SpawnPoints[0].y);
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
                GUI.Label(new Rect(20,20,430,60),paused?"Paused — Esc to resume":"MAP 2 — VILLAGE ARRIVAL\nReach the village command post.");
                if(paused && GUI.Button(new Rect(Screen.width/2-100,Screen.height/2,200,40),"Return to main menu")) {
                    Time.timeScale=1;SceneManager.LoadScene("01_MainMenu");
                }
            }
        }
        void OnDestroy(){
            SceneManager.sceneLoaded-=MuteVillageCameras;
#if UNITY_EDITOR
            EndShaderPreparation();
#endif
            foreach(var material in partyMaterials)if(material!=null)Destroy(material);if(poses!=null)foreach(var pose in poses)pose?.Dispose();Time.timeScale=1;}
    }
}
