using System;
using System.Collections.Generic;
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
    /// <summary>Map 1 only. Cinematic phases are transient; only the extraction objective and
    /// deterministic enemy identities are checkpointed. Completion is committed after departure.</summary>
    [DefaultExecutionOrder(80)]
    public sealed partial class Map01Extraction : MonoBehaviour
    {
        public AnimationClip radio, seatedReady, seatedFire, seatedReload, lowerWeapon, board, sit, travel;
        public AudioClip engine; // Serialized legacy field; no motor playback.
        public AnimationClip rifleStow, oarPickup, rowStart, rowLoop, rowStop;
        public AudioClip paddleAudio, rifleAudio;
        public AnimationClip[] hungClips;
        public Vector3 approach = new(-.85f, 1.48f, 87f);
        public Vector3[] boardingAnchors = { new(-.68f,1.48f,87), new(-.35f,1.31f,87),
            new(-.10f,1.08f,87), new(.15f,.85f,87), new(.35f,.65f,87), new(1f,.145f,87), new(1.55f,-.13f,87) };
        public Vector3[] departurePath;
        public Vector3[] blockingPosts;
        public enum Phase { Dormant, Radio, Extraction, Approach, Boarding, Seating, Departing, Complete }
        public Phase CurrentPhase { get; private set; }
        private bool preparationReady;
        public bool Prepared => preparationReady;
        public bool HasDeparted => CurrentPhase == Phase.Complete;
        public Vector3 ObjectivePosition => approach;
        private Map01Mission mission;
        private Map01Quest quest;
        private ThirdPersonCamera cameraRig;
        private Transform boat, commander, oar;
        private readonly List<GameObject> pursuit=new();
        private readonly Vector3[] lastTargets=new Vector3[2];
        private float lastStroke=-1, lastSplash, nextEnemyShot;
        private bool oarHeld;
        private readonly float[] aimUntil=new float[2];
        private readonly Transform[] coverTargets=new Transform[2];
        private readonly float[] nextCoverShot=new float[2], pendingCoverShot=new float[2], coverReloadUntil=new float[2];
        private readonly int[] coverRounds=new int[2];
        private readonly float[] lastCoverShot={-100,-100};
        private readonly float[] coverAcquiredAt=new float[2];
        private readonly List<Map01EnemyController> shoreEnemies=new();
        public int NamCoverShots { get; private set; }
        public int CommanderCoverShots { get; private set; }
        public int CoverKills { get; private set; }
        [Header("Manual animation tuning")]
        public AnimationClip gameplayRifleRun;
        [Range(.2f,2f)] public float hungTurnSeconds=.85f;
        [Range(.2f,2f)] public float paddleReachSeconds=.75f;
        public bool applyHandContacts=true;
        private AnimationClip urgentRun,urgentJump;
        public AnimationClip BoardingRunClip => urgentRun;
        public const float ApproachSeconds=.55f, BoardingSeconds=2.9f, SeatingSeconds=2.1f;
        private Animator namActor, hungActor, commanderActor;
        private PosePlayer namPose, hungPose, commanderPose;
        private Map01PaddleGrip paddleGrip;
        public void SetPaddleGrip(float left,float right)=>paddleGrip?.Set(left,right);
        private readonly List<Map01EnemyController> blockers = new();
        private float clock, skipHeld;
        private bool skipReleased;
        private Vector3 initialPlayerPosition, boatOrigin;
        private Quaternion initialPlayerRotation;
        private AudioSource motor;
        private ParticleSystem wake;
        private Material wakeMaterial;
        private Texture2D wakeTexture;
        private string subtitle;
        private DialogueVoice speech;
        private float radioReplyAt,radioEndsAt;
        private bool radioAnswered;
        private int radioVoiceIndex;
        private float nextRadioAt;
        private readonly List<GameObject> radioHiddenWeapons=new();
        private bool radioActionsEnabled;
        private Map01NamActions radioActions;
        private static readonly string[] RadioVoiceIds={"m1_v2_extraction_01","m1_v2_extraction_02","m1_v2_extraction_03"};
        private int departureVoiceIndex;
        private float nextDepartureVoiceAt;
        private static readonly string[] DepartureVoiceIds = {
            "m1_depart_destination_question", "m1_depart_destination_order", "m1_depart_bank_reply"
        };
        private readonly List<(Animator actor, Weapon rifle)> seatedRifles = new();
        Map01BoatCarry namBoatCarry;


        [Header("Campaign transition")]
        public bool autoContinueToMap2=true;
        private float completionClock;
        private bool transitionStarted;
        public Transform BoatRoot => boat;
        public Transform NamRoot => mission!=null?mission.player:namActor.transform;
        public Transform HungRoot => hungActor.transform.parent;
        public Transform CommanderRoot => commander;
        public Animator NamAnimator => namActor;
        public Animator HungAnimator => hungActor;
        public Animator CommanderAnimator => commanderActor;
        public Transform Paddle => oar;

        public static Map01Extraction Get(Map01Mission owner)
        {
            var existing = owner.GetComponentInChildren<Map01Extraction>();
            if (existing != null) return existing;
            var prefab = Resources.Load<Map01Extraction>("Cutscenes/Map01Extraction");
            if (prefab == null) { Debug.LogError("Run ShadowVale/Cutscene/Prepare extraction before playing."); return null; }
            var instance = Instantiate(prefab, owner.transform);
            instance.name = "Map 1 extraction";
            instance.mission = owner; instance.quest = owner.GetComponent<Map01Quest>();
            instance.cameraRig = owner.gameCamera.GetComponent<ThirdPersonCamera>();
            return instance;
        }

        public void Begin(bool showRadio)
        {
            if (CurrentPhase != Phase.Dormant || quest==null || quest.Stage!=Map01Quest.ExtractionStage) return;
            if (!Prepare()) { Debug.LogError("Extraction setup incomplete; completion remains locked."); return; }
            CurrentPhase = showRadio ? Phase.Radio : Phase.Extraction;
            clock = skipHeld = 0; skipReleased = false;
            if (showRadio) {
                speech=DialogueVoice.For(gameObject);
                radioVoiceIndex=0;nextRadioAt=DialogueVoice.Length(RadioVoiceIds[0])+.35f;
                DialogueVoice.StopAll();speech.CanSpeak=()=>mission!=null&&!mission.Paused&&!ForestMenu.Visible;
                speech.Play(RadioVoiceIds[0]);
                mission.CloseGameplayPanel();mission.Cinematic=true;
                initialPlayerPosition=mission.player.position;initialPlayerRotation=mission.player.rotation;
                radioActions=Map01NamActions.For(mission);radioActionsEnabled=radioActions!=null&&radioActions.enabled;
                if(radioActions!=null)radioActions.enabled=false;
                foreach(var weapon in namActor.GetComponentsInChildren<Weapon>(true))if(weapon.gameObject.activeSelf){radioHiddenWeapons.Add(weapon.gameObject);weapon.gameObject.SetActive(false);}
                SetPlayerDrivers(false);
                namPose=new PosePlayer(namActor);namPose.Play(Resources.Load<AnimationClip>("Rescue/Nam_Shelter_Idle"));
                subtitle=DialogueVoice.Caption(RadioVoiceIds[0]);
            }
        }

        private bool Prepare()
        {
            if (boat != null) return preparationReady;
            var hull = FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)
                .FirstOrDefault(f => f.sharedMesh != null && f.name == "Moored wooden sampan");
            if (hull == null || board == null || sit == null || radio == null || seatedReady == null || travel == null
                || blockingPosts == null || blockingPosts.Length != 4 || departurePath == null || departurePath.Length < 2) return false;
            // Validate the actors before publishing Prepared or moving scene geometry.
            // A checkpoint can request extraction before the briefing visual has spawned.
            namActor=mission.player.GetComponentInChildren<Animator>();
            hungActor=mission.hung.GetComponentInChildren<Animator>();
            commanderActor=FindObjectsByType<Animator>(FindObjectsSortMode.None).FirstOrDefault(a=>a.name==Map01OpeningCutscene.CommanderVisualName);
            if(namActor==null||hungActor==null||commanderActor==null)return false;
            boat = new GameObject("Extraction boat root").transform;
            boat.position = boatOrigin = new Vector3(1.55f, 0, 87);
            hull.transform.SetParent(boat, true);
            foreach(var item in FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t=>t.name.StartsWith("Oar ")))
                item.SetParent(boat,true);
            foreach(var rope in FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t=>t.name=="Mooring line"))
                rope.gameObject.SetActive(false);
            foreach (var bench in FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t.name == "Boat bench" && Mathf.Abs(t.position.z - 87) < 3))
                bench.SetParent(boat, true);
            foreach(var movingRenderer in boat.GetComponentsInChildren<MeshRenderer>())Map01MovingPropRendering.Prepare(movingRenderer);
            namActor = mission.player.GetComponentInChildren<Animator>();
            var hungVisual = mission.hung.GetComponentInChildren<Map01HungVisual>();
            if (hungVisual != null) {hungVisual.enabled = false;hungVisual.transform.localPosition=Vector3.zero;}
            hungActor = mission.hung.GetComponentInChildren<Animator>();
            // Reuse the established briefing commander, including his insignia and tint.
            commanderActor = FindObjectsByType<Animator>(FindObjectsSortMode.None)
                .FirstOrDefault(a => a.name == Map01OpeningCutscene.CommanderVisualName);
            if (commanderActor == null || hungActor == null || namActor == null) return false;
            var opening = commanderActor.GetComponentInParent<Map01OpeningCutscene>();
            if (opening != null) opening.ReleaseCommanderForExtraction();
            commander = commanderActor.transform;
            StopAgent(mission.hung);
            Seat(mission.hung, 1.6f); Seat(commander, -1.6f);
            hungActor.transform.localPosition=Vector3.zero;
            hungPose = new PosePlayer(hungActor,hungClips); commanderPose = new PosePlayer(commanderActor);
            hungPose.Play(travel); commanderPose.Play(travel);

            EnsureRifle(hungActor); EnsureRifle(commanderActor);
            HidePassengerGuns();
            paddleGrip=new Map01PaddleGrip(commanderActor);
            BuildOar();
            SpawnBlockers();
            motor = boat.gameObject.AddComponent<AudioSource>();
            motor.clip = paddleAudio; motor.loop = false; motor.spatialBlend = 1; motor.minDistance = 8; motor.volume = .5f;
            var water = new GameObject("Extraction wake"); water.transform.SetParent(boat, false);
            water.transform.localPosition = new Vector3(0, -.12f, -2.8f);
            water.transform.localRotation = Quaternion.Euler(0,180,0);
            wake = water.AddComponent<ParticleSystem>(); wake.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = wake.main; main.startLifetime = 2.4f; main.startSpeed = .3f; main.startSize = .25f;
            main.startColor = new Color(.8f,.88f,.9f,.4f); main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = wake.emission; emission.rateOverTime = 22;
            var shape = wake.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 35;
            var renderer = wake.GetComponent<ParticleSystemRenderer>(); wakeMaterial = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            wakeTexture = new Texture2D(32,32,TextureFormat.RGBA32,false);
            for(int y=0;y<32;y++) for(int x=0;x<32;x++) {
                float r=Vector2.Distance(new Vector2(x,y),new Vector2(15.5f,15.5f))/15.5f;
                wakeTexture.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01(1-r)*.45f));
            }
            wakeTexture.Apply();wakeMaterial.SetTexture("_BaseMap",wakeTexture);
            wakeMaterial.SetColor("_BaseColor",new Color(.7f,.85f,.88f,.45f));
            wakeMaterial.SetFloat("_Surface",1);wakeMaterial.SetFloat("_SrcBlend",5);wakeMaterial.SetFloat("_DstBlend",10);wakeMaterial.SetFloat("_ZWrite",0);
            wakeMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");wakeMaterial.renderQueue=3000;
            renderer.sharedMaterial = wakeMaterial;
            preparationReady=true;
            return true;
        }

        private void SpawnBlockers()
        {
            var template = mission.Enemies.Where(e => e.name.StartsWith("Outpost guard ")).OrderBy(e => e.SaveId, StringComparer.Ordinal).First();
            for (int i = 0; i < blockingPosts.Length; i++) {
                string id = "Extraction guard " + i;
                var guard = mission.Enemies.FirstOrDefault(e => e.name == id);
                if (guard == null) {
                    var clone = Instantiate(template.gameObject, blockingPosts[i], Quaternion.Euler(0,180,0), mission.transform);
                    clone.name = id;
                    var inheritedLoot = clone.GetComponent<ForestPoint>();
                    if (inheritedLoot != null) { inheritedLoot.enabled = false; Destroy(inheritedLoot); }
                    guard = clone.GetComponent<Map01EnemyController>();
                    guard.carriesSupplyKey=false;guard.operationIndex=0;
                    guard.Configure(Array.Empty<Vector3>()); guard.BindMission(mission);
                    clone.GetComponent<Health>().RestoreHealth(clone.GetComponent<Health>().Max);
                    mission.AddEnemy(guard);
                }
                guard.carriesSupplyKey=false;guard.operationIndex=0;
                var staleLoot=guard.GetComponent<ForestPoint>();
                if(staleLoot!=null){staleLoot.items=staleLoot.items.Where(item=>item.item_id!=Map01Story.KeyItem).ToArray();if(staleLoot.label=="Lính gác kho — chìa khóa kho tiếp tế")staleLoot.label="Túi lính tuần tra";}
                blockers.Add(guard);
            }
        }

        private static void StopAgent(Transform actor)
        {
            var agent = actor.GetComponent<NavMeshAgent>();
            if (agent == null) return;
            if (agent.isOnNavMesh) { agent.ResetPath(); agent.isStopped = true; }
            agent.enabled = false;
        }
        private void Seat(Transform actor, float z)
        {
            actor.SetParent(boat, true);
            actor.localPosition = new Vector3(0,-.13f,z);
            actor.localRotation = Quaternion.Euler(0,270,0); // Both aim to port, never through the other benches.
        }
        private void EnsureRifle(Animator actor)
        {
            var prop=Map01Rifle.Attach(actor);
            if(prop!=null)prop.weapon.gameObject.SetActive(true);
            if(prop!=null && !seatedRifles.Any(p=>p.actor==actor)) {
                // The replacement commander has different wrist/hand proportions. Put the visible
                // pistol grip into his palm before solving the supporting arm, instead of using an
                // offset authored for another character's wrist.
                var hand=actor.GetBoneTransform(HumanBodyBones.RightHand);
                var tip=hand.Cast<Transform>().FirstOrDefault(t=>t.name.EndsWith("_end",StringComparison.OrdinalIgnoreCase));
                var handle=prop.weapon.GetComponentsInChildren<Renderer>().FirstOrDefault(t=>t.name.StartsWith("bl_grip_low"));
                if(tip!=null && handle!=null) {
                    Vector3 grip=prop.weapon.transform.InverseTransformPoint(handle.bounds.center);
                    prop.weapon.transform.localPosition=hand.InverseTransformPoint(Vector3.Lerp(hand.position,tip.position,.45f))
                        -prop.weapon.transform.localRotation*Vector3.Scale(grip,prop.weapon.transform.localScale);
                }
                var foregrip=prop.weapon.GetComponentsInChildren<Renderer>().FirstOrDefault(t=>t.name.StartsWith("r_woodLower_low"));
                var magazine=prop.weapon.GetComponentsInChildren<Renderer>().FirstOrDefault(t=>t.name.StartsWith("bm_magazine_low"));
                if(foregrip!=null && handle!=null) {
                    // This imported rifle's local +Z is not its barrel axis: the former
                    // (0,0,.25) marker sat below the magazine, not on the wooden foregrip.
                    Vector3 barrel=(prop.weapon.Muzzle.position-handle.bounds.center).normalized;
                    Vector3 down=magazine!=null?Vector3.ProjectOnPlane(magazine.bounds.center-handle.bounds.center,barrel).normalized:-actor.transform.up;
                    prop.support.position=foregrip.bounds.center+down*.02f;
                }
                seatedRifles.Add((actor,prop.weapon));
            }
        }
        private void BuildOar()
        {
            oar=new GameObject("Hung paddle pivot").transform;oar.SetParent(boat,false);
            foreach(var part in boat.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Oar ")).ToArray())
                part.SetParent(oar,true);
            // Reuse both existing meshes, resizing them into a working single paddle.
            var shaft=oar.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="Oar shaft");
            var blade=oar.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="Oar blade");
            if(shaft!=null) {shaft.localPosition=new Vector3(0,0,.45f);shaft.localRotation=Quaternion.identity;shaft.localScale=new Vector3(.035f,.035f,1.4f);}
            if(blade!=null) {blade.localPosition=new Vector3(0,0,1.3f);blade.localRotation=Quaternion.identity;blade.localScale=new Vector3(.18f,.045f,.55f);}
            oar.localPosition=new Vector3(.35f,.35f,-1.6f);oar.localRotation=Quaternion.identity;
        }

        // Retained for callers displaying the old status; it never gates boarding.
        public bool BoardingAreaSecure => true;

        private void Update()
        {
            if (CurrentPhase == Phase.Complete) {
                if(autoContinueToMap2 && !transitionStarted && !ForestMenu.Visible) {
                    completionClock+=Time.unscaledDeltaTime;
                    if(completionClock>=3) ContinueToMap2();
                }
                return;
            }
            if (mission == null || CurrentPhase == Phase.Dormant) return;
            if (mission.Paused || Map01SaveSystem.IsRestoring) return;
            float dt=Mathf.Min(Time.unscaledDeltaTime,.05f);
            namPose?.Tick(dt); hungPose?.Tick(dt); commanderPose?.Tick(dt);
            if (CurrentPhase == Phase.Extraction) {
                if (mission.Stopped) return;
                // Hùng is injured; the commander is already at the oar seat. Nam alone covers the bank.
                if (Vector3.Distance(mission.player.position,approach)<1.1f) BeginBoarding();
                return;
            }
            clock += dt;
            UpdateRowing();
            bool held = Keyboard.current != null && Keyboard.current.escapeKey.isPressed;
            if (!held) skipReleased=true;
            skipHeld = held && skipReleased ? skipHeld+Time.unscaledDeltaTime : 0;
            if(skipHeld>=1) { Skip(); return; }
            switch(CurrentPhase) {
                case Phase.Radio:
                    mission.player.SetPositionAndRotation(initialPlayerPosition,initialPlayerRotation);
                    if(clock>=nextRadioAt&&!speech.HasSpeech){
                        if(radioVoiceIndex+1<RadioVoiceIds.Length){radioVoiceIndex++;speech.Play(RadioVoiceIds[radioVoiceIndex]);nextRadioAt=clock+DialogueVoice.Length(RadioVoiceIds[radioVoiceIndex])+.35f;}
                        else {EndRadio();break;}
                    }
                    subtitle=DialogueVoice.Caption(RadioVoiceIds[radioVoiceIndex]);
                    var focus=mission.player.position+Vector3.up*1.35f;
                    View(focus-mission.player.forward*2.2f+mission.player.right*1.5f,focus,49);
                    break;
                case Phase.Approach:
                    float p=Mathf.Clamp01(clock/ApproachSeconds);
                    mission.player.SetPositionAndRotation(Vector3.Lerp(initialPlayerPosition,boardingAnchors[0],p),
                        Quaternion.Slerp(initialPlayerRotation,Quaternion.Euler(0,90,0),p));
                    BoardingView();
                    if(p>=1) { ChangePhase(Phase.Boarding); namPose.Play(urgentRun??board); }
                    break;
                case Phase.Boarding:
                    if(clock<1.65f) {
                        mission.player.position=Along(boardingAnchors.Take(5).ToArray(),clock/1.65f);
                    } else {
                        float hop=Mathf.Clamp01((clock-1.65f)/.8f);
                        namPose.Play(urgentJump??board);namPose.Rate((urgentJump??board).length/.8f);
                        mission.player.position=Vector3.Lerp(boardingAnchors[4],boardingAnchors[5],hop)+Vector3.up*(.20f*Mathf.Sin(Mathf.PI*hop));
                    }
                    BoardingView();
                    if(clock>=BoardingSeconds) { ChangePhase(Phase.Seating); namPose.Play(sit);namPose.Rate(sit.length/SeatingSeconds); }
                    break;
                case Phase.Seating:
                    mission.player.rotation=Quaternion.Slerp(Quaternion.Euler(0,90,0),Quaternion.Euler(0,270,0),Mathf.SmoothStep(0,1,clock/SeatingSeconds));
                    BoardingView();
                    mission.player.position=Vector3.Lerp(boardingAnchors[5],boardingAnchors[6],Mathf.SmoothStep(0,1,clock/SeatingSeconds));
                    if(clock>=SeatingSeconds) StartDeparture();
                    break;
                case Phase.Departing:
                    ApplyDeparture(Mathf.Clamp01(clock/18f));
                    CoverDeparture();
                    bool dialogueFinished=UpdateDepartureDialogue();
                    if(clock>=18 && dialogueFinished) FinishDeparture();
                    break;
            }
        }

        private void LateUpdate()
        {
            if(CurrentPhase==Phase.Complete || (mission!=null && mission.Paused))return;
            // Re-sample without advancing time. A manual camera capture can run before
            // Unity's animation update; do not suppress the real LateUpdate in that frame.
            namPose?.Sample();hungPose?.Sample();commanderPose?.Sample();
            if(CurrentPhase==Phase.Radio)Map01ArmContact.Radio(namActor,Mathf.SmoothStep(0,1,clock/.7f));
            if(CurrentPhase==Phase.Departing) AimAndFireCover(0);
            if(Prepared) {
                ApplyInjuredPassenger();
                if(applyHandContacts && CurrentPhase>=Phase.Radio && CurrentPhase<Phase.Departing) {
                    if(CurrentPhase==Phase.Boarding||CurrentPhase==Phase.Seating||CurrentPhase==Phase.Approach){namBoatCarry?.Draw();HoldSeatedRifle(namActor,.14f);}
                    if(!oarHeld)ArrivalRestHands(commanderActor,clock);
                }
            }
            UpdateShoreActors();
            if(oar==null || !oarHeld || !applyHandContacts){SetPaddleGrip(0,0);return;}
            // Correct Humanoid arm-length retargeting at the prop contacts; keep the authored torso/legs.
            float phase=CurrentPhase==Phase.Departing?Mathf.Min(clock,16)*Mathf.PI*2/2.4f:0;
            float reach=.30f+.10f*Mathf.Cos(phase);
            // Catch -> submerged pull -> lift -> recovery. The inboard hand is the top grip.
            float lift=Mathf.Max(0,-Mathf.Sin(phase))*.035f;
            if(CurrentPhase==Phase.Departing && clock>16)lift+=Mathf.SmoothStep(0,.10f,(clock-16)/2);
            float contact=CurrentPhase==Phase.Boarding?Mathf.SmoothStep(0,1,(clock-hungTurnSeconds)/paddleReachSeconds):1;
            var rowingChest=commanderActor.GetBoneTransform(HumanBodyBones.Chest);
            rowingChest.rotation=Quaternion.AngleAxis((7+2*Mathf.Cos(phase))*contact,boat.up)*rowingChest.rotation;
            PaddleArm(HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftHand,
                boat.TransformPoint(new Vector3(.12f,.99f+lift,-1.6f+reach)),contact,-1);
            PaddleArm(HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand,
                boat.TransformPoint(new Vector3(.48f,.73f+lift,-1.55f+reach)),contact,1);
            SetPaddleGrip(contact,contact);
            var a=PaddleGrip(commanderActor.GetBoneTransform(HumanBodyBones.LeftHand));
            var b=PaddleGrip(commanderActor.GetBoneTransform(HumanBodyBones.RightHand));
            oar.SetPositionAndRotation(Vector3.Lerp(oar.position,a,contact),Quaternion.Slerp(oar.rotation,Quaternion.LookRotation((b-a).normalized,boat.forward),contact));
            var blade=oar.TransformPoint(new Vector3(0,0,1.4f));
            if(CurrentPhase==Phase.Departing && blade.y<.06f && Time.unscaledTime-lastSplash>.16f) {
                lastSplash=Time.unscaledTime;
                var splash=new ParticleSystem.EmitParams {position=new Vector3(blade.x,.02f,blade.z),velocity=Vector3.up*.25f,startLifetime=.55f,startSize=.09f};
                wake.Emit(splash,3);
            }
        }
        private void OnGUI()
        {
            if(!mission || !mission.Cinematic || CurrentPhase==Phase.Dormant) return;
            var old=GUI.color; var depth=GUI.depth; GUI.depth=-91;
            GUI.color=Color.black;
            GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height*.08f),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0,Screen.height*.85f,Screen.width,Screen.height*.15f),Texture2D.whiteTexture);
            GUI.color=Color.white;
            GUI.Label(new Rect(Screen.width-260,15,250,30),"Hold Esc for 1 second to skip");
            var style=new GUIStyle(GUI.skin.label) { alignment=TextAnchor.MiddleCenter,wordWrap=true,fontSize=Mathf.Max(16,Screen.height/40) };
            GUI.Label(new Rect(Screen.width*.1f,Screen.height*.87f,Screen.width*.8f,Screen.height*.1f),subtitle??"",style);
            GUI.color=old; GUI.depth=depth;
        }
        private void OnDestroy() { paddleGrip?.Dispose(); foreach(var enemy in shoreEnemies)if(enemy!=null)enemy.enabled=true; if(wakeMaterial!=null)Destroy(wakeMaterial);if(wakeTexture!=null)Destroy(wakeTexture); namPose?.Dispose(); hungPose?.Dispose(); commanderPose?.Dispose(); if(mission!=null && mission.ModernHealth!=null)mission.ModernHealth.CinematicInvulnerable=false; }

        public sealed class PosePlayer : IDisposable
        {
            private PlayableGraph graph;
            private AnimationPlayableOutput output;
            private AnimationClipPlayable playable;
            private AnimationClipPlayable previous;
            private AnimationMixerPlayable mixer;
            private AnimationLayerMixerPlayable freeArmMixer;
            private AnimationClipPlayable freeArmClip;
            private AnimationClip freeArmSource;
            private AvatarMask freeArmMask;
            private float blend;
            private AnimationClip selected;
            private readonly Animator actor;
            private readonly bool rootMotion;
            private readonly AnimatorCullingMode culling;
            private readonly RuntimeAnimatorController controller;
            private readonly AnimationClip[] variants;
            public PosePlayer(Animator target,AnimationClip[] variants=null) {
                this.variants=variants;
                actor=target; rootMotion=actor.applyRootMotion; culling=actor.cullingMode;
                controller=actor.runtimeAnimatorController; actor.runtimeAnimatorController=null;
                actor.applyRootMotion=false; actor.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                graph=PlayableGraph.Create("Extraction "+target.name); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                output=AnimationPlayableOutput.Create(graph,"Authored pose",target);
                mixer=AnimationMixerPlayable.Create(graph,2);output.SetSourcePlayable(mixer);graph.Play();
            }
            // Only arms/fingers come from the existing unarmed walk. Boarding's feet/root and C1 stay authored.
            public void FreeArms(bool enabled=true) {
                if(freeArmSource!=null){freeArmMixer.SetInputWeight(1,enabled?1:0);return;}
                if(!enabled)return;
                freeArmSource=Resources.Load<AnimationClip>("Cutscenes/Map02/Nam_Boat_Free_Arms");
                if(freeArmSource==null){Debug.LogError("Boat free-arm clip is missing.");return;}
                freeArmMixer=AnimationLayerMixerPlayable.Create(graph,2);
                graph.Connect(mixer,0,freeArmMixer,0);freeArmMixer.SetInputWeight(0,1);
                freeArmClip=AnimationClipPlayable.Create(graph,freeArmSource);freeArmClip.SetApplyFootIK(false);freeArmClip.SetSpeed(0);
                graph.Connect(freeArmClip,0,freeArmMixer,1);freeArmMixer.SetInputWeight(1,1);
                freeArmMask=new AvatarMask();for(int i=0;i<(int)AvatarMaskBodyPart.LastBodyPart;i++)freeArmMask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i,false);
                foreach(var part in new[]{AvatarMaskBodyPart.LeftArm,AvatarMaskBodyPart.RightArm,AvatarMaskBodyPart.LeftFingers,AvatarMaskBodyPart.RightFingers})freeArmMask.SetHumanoidBodyPartActive(part,true);
                freeArmMixer.SetLayerMaskFromAvatarMask(1,freeArmMask);output.SetSourcePlayable(freeArmMixer);
            }
            void SampleFreeArms(){if(freeArmClip.IsValid()&&playable.IsValid()&&selected!=null)freeArmClip.SetTime(playable.GetTime()/Mathf.Max(.01f,selected.length)*freeArmSource.length);}
            public void Play(AnimationClip clip,bool restart=false) {
                if(clip==null) return;
                if(variants!=null) clip=variants.FirstOrDefault(c=>c.name==clip.name.Replace("Nam_","Hung_"))??clip;
                if(selected==clip && !restart) return;
                if(previous.IsValid()) {graph.Disconnect(mixer,0);graph.DestroyPlayable(previous);}
                if(playable.IsValid()) {graph.Disconnect(mixer,1);previous=playable;graph.Connect(previous,0,mixer,0);}
                selected=clip; playable=AnimationClipPlayable.Create(graph,clip);
                playable.SetApplyFootIK(false);graph.Connect(playable,0,mixer,1);
                blend=previous.IsValid()?0:1;mixer.SetInputWeight(0,1-blend);mixer.SetInputWeight(1,blend);graph.Evaluate(0);
            }
            public void Tick(float dt) {
                if(!playable.IsValid()) return;
                blend=Mathf.Min(1,blend+dt/.18f);mixer.SetInputWeight(0,1-blend);mixer.SetInputWeight(1,blend);
                if(!selected.isLooping && playable.GetTime()+dt*playable.GetSpeed()>=selected.length) { playable.SetTime(selected.length); playable.SetSpeed(0); }
                graph.Evaluate(dt);if(freeArmClip.IsValid()){SampleFreeArms();graph.Evaluate(0);}
            }
            public void Rate(float rate) {if(playable.IsValid())playable.SetSpeed(rate);}
            public double Time => playable.IsValid()?playable.GetTime():0;
            public void Sample() { if(graph.IsValid()){SampleFreeArms();graph.Evaluate(0);} }
            public void Settle(bool end=false) {
                blend=1;mixer.SetInputWeight(0,0);mixer.SetInputWeight(1,1);
                if(playable.IsValid()) {playable.SetTime(end?selected.length:0);playable.SetSpeed(0);}
                graph.Evaluate(0);
            }
            public void Dispose() {
                if(graph.IsValid()) graph.Destroy();
                if(freeArmMask!=null)UnityEngine.Object.Destroy(freeArmMask);
                if(actor!=null) { actor.runtimeAnimatorController=controller;actor.applyRootMotion=rootMotion; actor.cullingMode=culling; }
            }
        }
    }
}
