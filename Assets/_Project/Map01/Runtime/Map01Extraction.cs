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
    public sealed class Map01Extraction : MonoBehaviour
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
        public bool Prepared => boat != null;
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
        private readonly List<Map01EnemyController> blockers = new();
        private float clock, skipHeld, nextShot;
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
        private int shots;
        private readonly List<(Animator actor, Weapon rifle)> seatedRifles = new();


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
        public void ContinueToMap2() {
            if(transitionStarted || mission==null || quest.Stage!=Map01Quest.CompleteStage)return;
            if(!Prepare())return;
            transitionStarted=true;
            namPose?.Dispose();hungPose?.Dispose();commanderPose?.Dispose();
            namPose=hungPose=commanderPose=null;
            var arrival=boat.gameObject.AddComponent<Map02Arrival>();
            arrival.TakeParty(this,mission);
            transform.SetParent(boat,true);enabled=false;
            boat.SetParent(null,true);DontDestroyOnLoad(boat.gameObject);
            mission.gameCamera.transform.SetParent(boat,true);
            mission.ReleaseForNextMap();
            arrival.StartCoroutine(arrival.LoadVillage());
        }
        public void ArrivalRestHands(Animator actor,float time,float leftWeight=1,float rightWeight=1) {
            var hips=actor.GetBoneTransform(HumanBodyBones.Hips);
            for(int side=-1;side<=1;side+=2) {
                var upper=side<0?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm;
                var lower=side<0?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm;
                var handId=side<0?HumanBodyBones.LeftHand:HumanBodyBones.RightHand;
                var hand=actor.GetBoneTransform(handId);var local=hand.localRotation;
                var target=hips.position+actor.transform.right*(side*.21f)+actor.transform.up*.12f
                    +actor.transform.forward*(.12f+side*.025f*Mathf.Sin(time*5));
                SolveCoverArm(actor,upper,lower,handId,target,hand.rotation,side,side<0?leftWeight:rightWeight);hand.localRotation=local;
            }
        }
        public void ArrivalStowContacts(float leftWeight,float rightWeight) {
            var left=hungActor.GetBoneTransform(HumanBodyBones.LeftHand);
            var right=hungActor.GetBoneTransform(HumanBodyBones.RightHand);
            PaddleArm(HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftHand,
                oar.position-(PaddleGrip(left)-left.position),leftWeight,-1);
            PaddleArm(HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand,
                oar.TransformPoint(new Vector3(0,0,.45f))-(PaddleGrip(right)-right.position),rightWeight,1);
        }
        // Share the tested hand/prop contacts with the arrival, without mission or firing callbacks.
        public void ArrivalPaddleContacts(float time,float weight) {
            float phase=time*Mathf.PI*2/2.4f;
            float reach=.30f+.10f*Mathf.Cos(phase),lift=Mathf.Max(0,-Mathf.Sin(phase))*.035f;
            PaddleArm(HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftHand,
                boat.TransformPoint(new Vector3(.12f,.99f+lift,-1.6f+reach)),weight,-1);
            PaddleArm(HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand,
                boat.TransformPoint(new Vector3(.48f,.73f+lift,-1.55f+reach)),weight,1);
            var a=PaddleGrip(hungActor.GetBoneTransform(HumanBodyBones.LeftHand));
            var b=PaddleGrip(hungActor.GetBoneTransform(HumanBodyBones.RightHand));
            oar.SetPositionAndRotation(a,Quaternion.LookRotation((b-a).normalized,boat.forward));
        }

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
            if (CurrentPhase != Phase.Dormant) return;
            if (!Prepare()) { Debug.LogError("Extraction setup incomplete; completion remains locked."); return; }
            CurrentPhase = showRadio ? Phase.Radio : Phase.Extraction;
            clock = skipHeld = 0; skipReleased = false;
            if (showRadio) {
                speech=DialogueVoice.For(gameObject);
                radioReplyAt=Mathf.Max(4.5f,DialogueVoice.Length("m1_radio_order")+.25f);
                radioEndsAt=Mathf.Max(7,radioReplyAt+DialogueVoice.Length("m1_radio_reply")+.3f);
                radioAnswered=false;speech.PlayOnce("m1_radio_order");
                mission.CloseGameplayPanel(); mission.Cinematic = true;
                SetPlayerDrivers(false);
                namPose = new PosePlayer(namActor); namPose.Play(radio);
                subtitle = DialogueVoice.Caption("m1_radio_order");
            }
        }

        private bool Prepare()
        {
            if (boat != null) return true;
            var hull = FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)
                .FirstOrDefault(f => f.sharedMesh != null && f.name == "Moored wooden sampan");
            if (hull == null || board == null || sit == null || radio == null || seatedReady == null || travel == null
                || blockingPosts == null || blockingPosts.Length != 4 || departurePath == null || departurePath.Length < 2) return false;
            boat = new GameObject("Extraction boat root").transform;
            boat.position = boatOrigin = new Vector3(1.55f, 0, 87);
            hull.transform.SetParent(boat, true);
            foreach(var item in FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t=>t.name.StartsWith("Oar ")))
                item.SetParent(boat,true);
            foreach(var rope in FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t=>t.name=="Mooring line"))
                rope.gameObject.SetActive(false);
            foreach (var bench in FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t.name == "Boat bench" && Mathf.Abs(t.position.z - 87) < 3))
                bench.SetParent(boat, true);
            namActor = mission.player.GetComponentInChildren<Animator>();
            var hungVisual = mission.hung.GetComponentInChildren<Map01HungVisual>();
            if (hungVisual != null) {hungVisual.enabled = false;hungVisual.transform.localPosition=Vector3.zero;}
            hungActor = mission.hung.GetComponentInChildren<Animator>();
            // Reuse the established briefing commander, including his insignia and tint.
            commanderActor = FindObjectsByType<Animator>(FindObjectsSortMode.None)
                .FirstOrDefault(a => a.name == "Commander (shared player mesh)");
            if (commanderActor == null || hungActor == null || namActor == null) return false;
            var opening = commanderActor.GetComponentInParent<Map01OpeningCutscene>();
            if (opening != null) opening.ReleaseCommanderForExtraction();
            commander = commanderActor.transform;
            StopAgent(mission.hung);
            Seat(mission.hung, -1.6f); Seat(commander, 1.6f);
            hungActor.transform.localPosition=Vector3.zero;
            hungPose = new PosePlayer(hungActor,hungClips); commanderPose = new PosePlayer(commanderActor);
            hungPose.Play(seatedReady); commanderPose.Play(seatedReady);
            EnsureRifle(hungActor); EnsureRifle(commanderActor);
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
                    guard.Configure(Array.Empty<Vector3>()); guard.BindMission(mission);
                    clone.GetComponent<Health>().RestoreHealth(clone.GetComponent<Health>().Max);
                    mission.AddEnemy(guard);
                }
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
            if(prop!=null && !seatedRifles.Any(p=>p.actor==actor)) seatedRifles.Add((actor,prop.weapon));
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
            float dt=Time.unscaledDeltaTime;
            namPose?.Tick(dt); hungPose?.Tick(dt); commanderPose?.Tick(dt);
            if (CurrentPhase == Phase.Extraction) {
                if (mission.Stopped) return;
                CoverShore();
                if (Vector3.Distance(mission.player.position,approach)<1.1f) BeginBoarding();
                return;
            }
            clock += dt;
            UpdateRowing();
            bool held = Keyboard.current != null && Keyboard.current.escapeKey.isPressed;
            if (!held) skipReleased=true;
            skipHeld = held && skipReleased ? skipHeld+dt : 0;
            if(skipHeld>=1) { Skip(); return; }
            switch(CurrentPhase) {
                case Phase.Radio:
                    if(clock>=radioReplyAt && !radioAnswered && !speech.HasSpeech){radioAnswered=true;speech.PlayOnce("m1_radio_reply");}
                    subtitle=DialogueVoice.Caption(radioAnswered?"m1_radio_reply":"m1_radio_order");
                    var focus = mission.player.position+Vector3.up*1.35f;
                    View(focus-mission.player.forward*2.2f+mission.player.right*1.5f,focus,49);
                    if(clock>=Mathf.Max(radioEndsAt,radio.length)) EndRadio();
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
                    if(clock>=18) FinishDeparture();
                    break;
            }
        }

        private void LateUpdate()
        {
            if(CurrentPhase==Phase.Complete || (mission!=null && mission.Paused))return;
            // Re-sample without advancing time. A manual camera capture can run before
            // Unity's animation update; do not suppress the real LateUpdate in that frame.
            namPose?.Sample();hungPose?.Sample();commanderPose?.Sample();
            if(CurrentPhase==Phase.Departing) {
                for(int index=0;index<2;index++) AimAndFireCover(index);
            }
            if(applyHandContacts && Prepared && CurrentPhase>=Phase.Radio && CurrentPhase<Phase.Departing) {
                HoldSeatedRifle(commanderActor,.18f);
                if(CurrentPhase==Phase.Boarding && clock>=1.65f) {
                    EnsureRifle(namActor);HoldSeatedRifle(namActor,.16f);
                }
                if(!oarHeld) HoldSeatedRifle(hungActor,CurrentPhase>=Phase.Approach?.30f:.18f);
                if(CurrentPhase==Phase.Boarding && clock<hungTurnSeconds) {
                    foreach(var side in new[]{-1,1}) {
                        var hand=hungActor.GetBoneTransform(side<0?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
                        var localWrist=hand.localRotation;
                        SolveCoverArm(hungActor,side<0?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm,
                            side<0?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm,
                            side<0?HumanBodyBones.LeftHand:HumanBodyBones.RightHand,
                            mission.hung.TransformPoint(new Vector3(side*.20f,.68f,.24f)),hand.rotation,side,1);
                        hand.localRotation=localWrist;
                    }
                }
            }
            UpdateShoreActors();
            if(oar==null || !oarHeld || !applyHandContacts) return;
            // Correct Humanoid arm-length retargeting at the prop contacts; keep the authored torso/legs.
            float phase=CurrentPhase==Phase.Departing?Mathf.Min(clock,16)*Mathf.PI*2/2.4f:0;
            float reach=.30f+.10f*Mathf.Cos(phase);
            // Catch -> submerged pull -> lift -> recovery. The inboard hand is the top grip.
            float lift=Mathf.Max(0,-Mathf.Sin(phase))*.035f;
            if(CurrentPhase==Phase.Departing && clock>16)lift+=Mathf.SmoothStep(0,.10f,(clock-16)/2);
            float contact=CurrentPhase==Phase.Boarding?Mathf.SmoothStep(0,1,(clock-hungTurnSeconds)/paddleReachSeconds):1;
            var rowingChest=hungActor.GetBoneTransform(HumanBodyBones.Chest);
            rowingChest.rotation=Quaternion.AngleAxis((7+2*Mathf.Cos(phase))*contact,boat.up)*rowingChest.rotation;
            PaddleArm(HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftHand,
                boat.TransformPoint(new Vector3(.12f,.99f+lift,-1.6f+reach)),contact,-1);
            PaddleArm(HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand,
                boat.TransformPoint(new Vector3(.48f,.73f+lift,-1.55f+reach)),contact,1);
            var a=PaddleGrip(hungActor.GetBoneTransform(HumanBodyBones.LeftHand));
            var b=PaddleGrip(hungActor.GetBoneTransform(HumanBodyBones.RightHand));
            oar.SetPositionAndRotation(Vector3.Lerp(oar.position,a,contact),Quaternion.Slerp(oar.rotation,Quaternion.LookRotation((b-a).normalized,boat.forward),contact));
            var blade=oar.TransformPoint(new Vector3(0,0,1.4f));
            if(CurrentPhase==Phase.Departing && blade.y<.06f && Time.unscaledTime-lastSplash>.16f) {
                lastSplash=Time.unscaledTime;
                var splash=new ParticleSystem.EmitParams {position=new Vector3(blade.x,.02f,blade.z),velocity=Vector3.up*.25f,startLifetime=.55f,startSize=.09f};
                wake.Emit(splash,3);
            }
        }
        private void PaddleArm(HumanBodyBones upperBone,HumanBodyBones lowerBone,HumanBodyBones handBone,Vector3 target,float weight,float side)
        {
            var upper=hungActor.GetBoneTransform(upperBone);var lower=hungActor.GetBoneTransform(lowerBone);var hand=hungActor.GetBoneTransform(handBone);
            target=Vector3.Lerp(hand.position,target,weight);
            Vector3 origin=upper.position;float a=Vector3.Distance(origin,lower.position),b=Vector3.Distance(lower.position,hand.position);
            Vector3 direction=(target-origin).normalized;float d=Mathf.Clamp(Vector3.Distance(origin,target),Mathf.Abs(a-b)+.001f,(a+b)*.97f);
            target=origin+direction*d;
            Vector3 bend=Vector3.ProjectOnPlane(boat.right*side*.65f+boat.forward*.15f-boat.up*.45f,direction).normalized;
            float along=(a*a+d*d-b*b)/(2*d);float height=Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
            Vector3 elbow=origin+direction*along+bend*height;
            Quaternion wrist=hand.localRotation;
            upper.rotation=Quaternion.FromToRotation(lower.position-origin,elbow-origin)*upper.rotation;
            lower.rotation=Quaternion.FromToRotation(hand.position-lower.position,target-lower.position)*lower.rotation;
            hand.localRotation=wrist;
            if(hand.childCount>0) {
                // The hand end defines the actual mesh finger axis, independent of FBX bone axes.
                Vector3 fingers=hand.GetChild(0).position-hand.position;
                Vector3 forearm=hand.position-lower.position;
                Vector3 shaft=boat.TransformDirection(new Vector3(.36f,-.26f,.05f)).normalized;
                Vector3 gripFacing=Vector3.ProjectOnPlane(forearm,shaft).normalized;
                if(gripFacing.sqrMagnitude>.1f) hand.rotation=Quaternion.Slerp(hand.rotation,Quaternion.FromToRotation(fingers,gripFacing)*hand.rotation,weight);
            }
        }
        private readonly Dictionary<Transform,Vector3> paddlePalmCenters=new();
        private Vector3 PaddleGrip(Transform hand)
        {
            if(!paddlePalmCenters.TryGetValue(hand,out var contact)) {
                // Hand_end is a rig endpoint, not the center of Hung's closed fist.
                // Measure the actual hand surface in bind space once, preserving the source rig.
                Vector3 sum=Vector3.zero;float total=0;
                foreach(var renderer in hungActor.GetComponentsInChildren<SkinnedMeshRenderer>()) {
                    var mesh=renderer.sharedMesh;if(mesh==null || !mesh.isReadable)continue;
                    int bone=Array.IndexOf(renderer.bones,hand);if(bone<0)continue;
                    var vertices=mesh.vertices;var weights=mesh.boneWeights;var bind=mesh.bindposes[bone];
                    for(int i=0;i<weights.Length;i++) {
                        var w=weights[i];float influence=(w.boneIndex0==bone?w.weight0:0)+(w.boneIndex1==bone?w.weight1:0)
                            +(w.boneIndex2==bone?w.weight2:0)+(w.boneIndex3==bone?w.weight3:0);
                        if(influence<.7f)continue;
                        sum+=bind.MultiplyPoint3x4(vertices[i])*influence;total+=influence;
                    }
                }
                contact=total>0?sum/total:hand.InverseTransformPoint(hand.childCount>0?hand.GetChild(0).position:hand.position)*.8f;
                paddlePalmCenters[hand]=contact;
            }
            return hand.TransformPoint(contact);
        }
        private void UpdateRowing()
        {
            if(CurrentPhase==Phase.Approach) hungPose.Play(lowerWeapon);
            if(CurrentPhase==Phase.Boarding) {
                var gun=hungActor.GetComponent<Map01Rifle>();if(gun!=null)gun.weapon.gameObject.SetActive(false);
                // Rotate with hands close to the body before reaching for a stationary paddle.
                mission.hung.localRotation=Quaternion.Slerp(Quaternion.Euler(0,270,0),Quaternion.identity,Mathf.SmoothStep(0,1,clock/hungTurnSeconds));
                hungPose.Play(travel);
                oarHeld=clock>=hungTurnSeconds;
            }
            if(CurrentPhase==Phase.Seating) hungPose.Play(rowStart);
            if(CurrentPhase==Phase.Departing) {
                hungPose.Play(clock>16?rowStop:rowLoop);
                float stroke=Mathf.Floor(clock/2.4f);
                if(stroke!=lastStroke && clock<16) {lastStroke=stroke;motor.PlayOneShot(paddleAudio,.65f);}
            }
        }
        private void SpawnPursuitIfNeeded()
        {
            if(mission.Enemies.Any(e=>e.Alive && SafeShot(commanderActor,commanderActor.GetBoneTransform(HumanBodyBones.Chest).position,e.transform.position+Vector3.up*1.3f))) return;
            var template=mission.Enemies.First(e=>e.name.StartsWith("Outpost guard "));
            for(int i=0;i<2;i++) {
                Vector3 chosen=default;bool found=false;
                for(int attempt=0;attempt<24;attempt++) {
                    var candidate=boatOrigin+new Vector3(-12-attempt*.6f,2,-8+i*3);
                    if(!NavMesh.SamplePosition(candidate,out var hit,6,NavMesh.AllAreas))continue;
                    var view=mission.gameCamera.WorldToViewportPoint(hit.position+Vector3.up);
                    if(view.z>0 && view.x>-.1f && view.x<1.1f && view.y>-.1f && view.y<1.1f)continue;
                    chosen=hit.position;found=true;break;
                }
                if(!found) continue; // Never materialize a soldier in the camera frustum.
                var clone=Instantiate(template.gameObject,chosen,Quaternion.identity,transform);clone.name="Cinematic pursuer "+i;
                foreach(var loot in clone.GetComponents<ForestPoint>())Destroy(loot);
                var enemy=clone.GetComponent<Map01EnemyController>();enemy.enabled=false;
                clone.GetComponent<Health>().Revive();
                var actor=clone.GetComponentInChildren<Animator>();actor.Rebind();actor.Update(0);Map01Rifle.Attach(actor);
                var agent=clone.GetComponent<NavMeshAgent>();
                if(agent.isOnNavMesh && NavMesh.SamplePosition(boatOrigin+new Vector3(-5,2,7+i*2),out var bank,5,NavMesh.AllAreas)) {agent.isStopped=false;agent.SetDestination(bank.position);}
                pursuit.Add(clone);
            }
        }
        private void PrepareShoreActors()
        {
            foreach(var enemy in mission.Enemies.Where(e=>e.Alive && !e.IsBoss && Vector3.Distance(e.transform.position,boat.position)<70)) {
                if(!enemy.enabled)continue;
                shoreEnemies.Add(enemy);enemy.enabled=false;
                var agent=enemy.GetComponent<NavMeshAgent>();
                if(agent.isOnNavMesh) {
                    var destination=boatOrigin+new Vector3(-5,2,2+(shoreEnemies.Count%3)*2);
                    if(NavMesh.SamplePosition(destination,out var hit,5,NavMesh.AllAreas)) {
                        agent.isStopped=false;agent.SetDestination(hit.position);
                    }
                }
            }
        }
        private void UpdateShoreActors()
        {
            foreach(var enemy in shoreEnemies.Where(e=>e!=null).Select(e=>e.gameObject).Concat(pursuit.Where(p=>p!=null))) {
                var health=enemy.GetComponent<Health>();var agent=enemy.GetComponent<NavMeshAgent>();
                if(health.IsDead) {if(agent.isOnNavMesh)agent.isStopped=true;continue;}
                var actor=enemy.GetComponentInChildren<Animator>();
                actor.SetFloat(PlayerCombat.AnimatorParams.Speed,agent.velocity.magnitude);
                if(agent.velocity.sqrMagnitude<.01f)enemy.transform.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(boat.position-enemy.transform.position,Vector3.up));
                if(CurrentPhase==Phase.Departing && clock<5 && Time.unscaledTime>=nextEnemyShot) {
                    nextEnemyShot=Time.unscaledTime+3.5f;actor.SetTrigger(PlayerCombat.AnimatorParams.Attack);
                    var rifle=actor.GetComponent<Map01Rifle>();
                    if(rifle!=null) RetreatTrace(rifle.weapon.Muzzle.position,boat.position+boat.right*2.5f);
                }
            }
        }
        private IEnumerable<Transform> LivingShoreTargets() => mission.Enemies.Where(e=>e.Alive && !e.IsBoss).Select(e=>e.transform)
            .Concat(pursuit.Where(p=>p!=null && !p.GetComponent<Health>().IsDead).Select(p=>p.transform));
        private void CoverDeparture()
        {
            for(int i=0;i<2;i++) {
                var actor=i==0?namActor:commanderActor;var pose=i==0?namPose:commanderPose;
                if(Time.unscaledTime<coverReloadUntil[i] || pendingCoverShot[i]>0)continue;
                if(coverReloadUntil[i]>0) {
                    coverReloadUntil[i]=0;coverAcquiredAt[i]=Time.unscaledTime;
                    nextCoverShot[i]=Time.unscaledTime+.4f;pose.Play(seatedReady);
                }
                var origin=actor.GetBoneTransform(HumanBodyBones.RightUpperArm).position;
                var target=coverTargets[i];
                if(target==null || target.GetComponent<Health>().IsDead || !SafeShot(actor,origin,target.position+Vector3.up*1.3f)) {
                    target=LivingShoreTargets().Where(t=>Vector3.Distance(t.position,boat.position)<65)
                        .OrderBy(t=>Vector3.Distance(t.position,origin)+(t==coverTargets[1-i]?20:0))
                        .FirstOrDefault(t=>SafeShot(actor,origin,t.position+Vector3.up*1.3f));
                    coverTargets[i]=target;coverAcquiredAt[i]=Time.unscaledTime;nextCoverShot[i]=Time.unscaledTime+.4f;
                    if(target!=null)pose.Play(seatedReady);
                }
                if(target==null) {pose.Play(coverRounds[i]>0?lowerWeapon:seatedReady);continue;}
                lastTargets[i]=target.position;aimUntil[i]=Time.unscaledTime+1;
                if(Time.unscaledTime<nextCoverShot[i])continue;
                if(coverRounds[i]>0 && coverRounds[i]%12==0) {
                    pose.Play(seatedReload,true);coverReloadUntil[i]=Time.unscaledTime+seatedReload.length;
                    coverRounds[i]++;continue;
                }
                pose.Play(seatedFire,true);pendingCoverShot[i]=Time.unscaledTime+.06f;
                nextCoverShot[i]=Time.unscaledTime+(++coverRounds[i]%3==0?2.2f:.48f);
            }
        }
        private void AimAndFireCover(int index)
        {
            var target=coverTargets[index];
            if(target==null || target.GetComponent<Health>().IsDead) {
                pendingCoverShot[index]=0;
                var watcher=index==0?namActor:commanderActor;
                HoldSeatedRifle(watcher,.22f);
                var head=watcher.GetBoneTransform(HumanBodyBones.Head);
                head.rotation=Quaternion.AngleAxis(Mathf.Sin(Time.unscaledTime*.7f+index)*8,boat.up)*head.rotation;
                return;
            }
            if(Time.unscaledTime<coverReloadUntil[index])return;
            var actor=index==0?namActor:commanderActor;var gun=actor.GetComponent<Map01Rifle>();
            if(gun==null)return;
            var chest=actor.GetBoneTransform(HumanBodyBones.Chest);var end=target.position+Vector3.up*1.3f;
            // Seated chest bones sit low on this rig. Aim from the shoulders and solve both grips,
            // rather than tipping the entire torso (and lowering the muzzle into the jetty).
            Vector3 direction=(end-actor.GetBoneTransform(HumanBodyBones.RightUpperArm).position).normalized;
            float yaw=Mathf.Clamp(Vector3.SignedAngle(actor.transform.forward,Vector3.ProjectOnPlane(direction,boat.up),boat.up),-45,45);
            float aimWeight=Mathf.SmoothStep(0,1,(Time.unscaledTime-coverAcquiredAt[index])/.35f);
            chest.rotation=Quaternion.AngleAxis(yaw*aimWeight,boat.up)*chest.rotation;
            float recoil=Mathf.Exp(-Mathf.Max(0,Time.unscaledTime-lastCoverShot[index])*22);
            var shoulder=actor.GetBoneTransform(HumanBodyBones.RightUpperArm);
            Vector3 trigger=shoulder.position+direction*(.10f-.025f*recoil)-actor.transform.right*.20f-boat.up*.10f;
            Quaternion rifleRotation=Quaternion.LookRotation(direction,boat.up)*Quaternion.Euler(0,0,-90);
            Quaternion handRotation=rifleRotation*Quaternion.Inverse(gun.weapon.transform.localRotation);
            Vector3 wrist=trigger-handRotation*Vector3.Scale(gun.weapon.transform.localPosition,actor.GetBoneTransform(HumanBodyBones.RightHand).lossyScale);
            SolveCoverArm(actor,HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand,wrist,handRotation,1,aimWeight);
            SupportRifle(actor,gun,aimWeight);
            if(pendingCoverShot[index]<=0 || Time.unscaledTime<pendingCoverShot[index])return;
            pendingCoverShot[index]=0;
            var origin=gun.weapon.Muzzle.position;
            if(!SafeShot(actor,origin,end))return;
            if(coverRounds[index]%3==1) RetreatTrace(origin,end);
            if(rifleAudio!=null)motor.PlayOneShot(rifleAudio,.32f);
            var flash=new GameObject("Cinematic muzzle flash");flash.transform.position=origin;
            var light=flash.AddComponent<Light>();light.color=new Color(1,.7f,.25f);light.range=.65f;light.intensity=.7f;Destroy(flash,.035f);
            lastCoverShot[index]=Time.unscaledTime;
            if(index==0)NamCoverShots++;else CommanderCoverShots++;
            var health=target.GetComponent<Health>();var enemy=target.GetComponent<Map01EnemyController>();
            if(enemy!=null)enemy.ReceiveExtractionHit(health.Max*.38f,end);
            else health.TakeDamage(health.Max*.38f,end,null);
            if(health.IsDead)CoverKills++;
        }
        // Correct contacts during waiting/lowering as well as active fire. The wrist is
        // behind the palm contact, not at the foregrip itself (these rigs have Hand_end).
        private void SupportRifle(Animator actor,Map01Rifle gun,float weight)
        {
            var hand=actor.GetBoneTransform(HumanBodyBones.LeftHand);
            var lower=actor.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            var tip=hand.Cast<Transform>().FirstOrDefault(t=>t.name.IndexOf("end",StringComparison.OrdinalIgnoreCase)>=0);
            Vector3 localPalm=tip!=null?hand.InverseTransformPoint(tip.position)*.45f:Vector3.zero;
            Quaternion neutral=lower.rotation;
            Vector3 localFingers=tip!=null?hand.InverseTransformDirection(tip.position-hand.position).normalized:Vector3.right;
            // Fingers wrap across the barrel; the forearm supplies a stable roll reference.
            Quaternion wrist=Quaternion.FromToRotation(neutral*localFingers,gun.weapon.transform.up)*neutral;
            Vector3 target=gun.support.position-wrist*Vector3.Scale(localPalm,hand.lossyScale);
            SolveCoverArm(actor,HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftHand,target,wrist,-1,weight);
        }
        public void HoldSeatedRifle(Animator actor,float lowering)
        {
            var gun=actor.GetComponent<Map01Rifle>();
            if(gun==null || !gun.weapon.gameObject.activeInHierarchy)return;
            var shoulder=actor.GetBoneTransform(HumanBodyBones.RightUpperArm);
            var hand=actor.GetBoneTransform(HumanBodyBones.RightHand);
            Vector3 direction=(actor.transform.forward-boat.up*.18f).normalized;
            Quaternion rotation=Quaternion.LookRotation(direction,boat.up)*Quaternion.Euler(0,0,-90)*Quaternion.Inverse(gun.weapon.transform.localRotation);
            Vector3 trigger=shoulder.position+direction*.08f-actor.transform.right*.20f-boat.up*lowering;
            Vector3 wrist=trigger-rotation*Vector3.Scale(gun.weapon.transform.localPosition,hand.lossyScale);
            SolveCoverArm(actor,HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand,wrist,rotation,1,1);
            SupportRifle(actor,gun,1);
        }
        private void RetreatTrace(Vector3 from,Vector3 to)
        {
            var trail=new GameObject("Extraction faint tracer");
            var line=trail.AddComponent<LineRenderer>();line.sharedMaterial=mission.trailMaterial;
            line.positionCount=2;line.SetPosition(0,from);line.SetPosition(1,to);
            line.startWidth=.012f;line.endWidth=.003f;
            line.startColor=new Color(.8f,.68f,.4f,.45f);line.endColor=new Color(.8f,.68f,.4f,0);
            Destroy(trail,.045f);
        }
        private void SolveCoverArm(Animator actor,HumanBodyBones upperId,HumanBodyBones lowerId,HumanBodyBones handId,Vector3 target,Quaternion wrist,float side,float weight)
        {
            var upper=actor.GetBoneTransform(upperId);var lower=actor.GetBoneTransform(lowerId);var hand=actor.GetBoneTransform(handId);
            var origin=upper.position;float a=Vector3.Distance(origin,lower.position),b=Vector3.Distance(lower.position,hand.position);
            var axis=(target-origin).normalized;float d=Mathf.Clamp(Vector3.Distance(origin,target),Mathf.Abs(a-b)+.001f,(a+b)*.97f);
            target=origin+axis*d;
            var bend=Vector3.ProjectOnPlane(actor.transform.right*side*.6f-boat.up*.7f,axis).normalized;
            float along=(a*a+d*d-b*b)/(2*d);
            var elbow=origin+axis*along+bend*Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
            upper.rotation=Quaternion.Slerp(upper.rotation,Quaternion.FromToRotation(lower.position-origin,elbow-origin)*upper.rotation,weight);
            lower.rotation=Quaternion.Slerp(lower.rotation,Quaternion.FromToRotation(hand.position-lower.position,target-lower.position)*lower.rotation,weight);
            hand.rotation=Quaternion.Slerp(hand.rotation,wrist,weight);
        }
        private bool SafeShot(Animator actor,Vector3 from,Vector3 to)
        {
            if(Vector3.Distance(from,to)>65)return false;
            if(Vector3.Angle(actor.transform.forward,Vector3.ProjectOnPlane(to-from,boat.up))>105)return false;
            foreach(var other in new[]{namActor,hungActor,commanderActor}) {
                if(other==actor)continue;
                var d=to-from;
                foreach(var bone in new[]{HumanBodyBones.Hips,HumanBodyBones.Chest,HumanBodyBones.Head}) {
                    var p=other.GetBoneTransform(bone).position;
                    float t=Mathf.Clamp01(Vector3.Dot(p-from,d)/Mathf.Max(.001f,d.sqrMagnitude));
                    if(Vector3.Distance(p,from+d*t)<.33f)return false;
                }
            }
            foreach(var hit in Physics.RaycastAll(from,(to-from).normalized,Vector3.Distance(from,to),mission.ObstructionMask,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance)) {
                if(hit.transform.IsChildOf(actor.transform))continue;
                var hitEnemy=hit.collider.GetComponentInParent<Map01EnemyController>();
                if(hitEnemy!=null) {
                    if(!hitEnemy.Alive)continue; // The old upright capsule does not match the fallen corpse.
                    return Vector3.Distance(hitEnemy.transform.position+Vector3.up*1.3f,to)<.3f;
                }
                return false;
            }
            return true;
        }
        private void CoverShore()
        {
            if(Time.time<nextShot) return;
            nextShot=Time.time+2.5f;
            var actor=shots%2==0?hungActor:commanderActor;
            var pose=shots%2==0?hungPose:commanderPose;
            var origin=actor.GetBoneTransform(HumanBodyBones.Chest).position;
            // Port-side firing sector excludes the passenger row and the bow/stern.
            var target=mission.Enemies.Where(e=>e.Alive && e.transform.position.x<boat.position.x-3
                && Mathf.Abs(e.transform.position.z-actor.transform.position.z)<8
                && Vector3.Distance(e.transform.position,origin)<22).OrderBy(e=>Vector3.Distance(e.transform.position,origin)).FirstOrDefault();
            if(target==null) { pose.Play(seatedReady); return; }
            var end=target.transform.position+Vector3.up;
            if(Physics.Linecast(origin,end,out var hit,mission.ObstructionMask,QueryTriggerInteraction.Ignore)
                && hit.collider.GetComponentInParent<Map01EnemyController>()!=target) return;
            shots++;
            if(shots%8==0) { pose.Play(seatedReload); nextShot+=seatedReload.length; return; }
            pose.Play(seatedFire,true);
            RetreatTrace(origin,end);
            target.GetComponent<Health>().TakeDamage(8,end,actor.gameObject);
        }

        private void EndRadio()
        {
            speech?.Stop();
            namPose?.Dispose(); namPose=null;
            SetPlayerDrivers(true);
            cameraRig.ClearCinematicView(); mission.Cinematic=false;
            mission.ModernCombat?.RestoreAfterCinematic();
            CurrentPhase=Phase.Extraction; subtitle=null;
            ForestMenu.SuppressKeysAfterCutscene();
            Cursor.lockState=CursorLockMode.Locked; Cursor.visible=false;
            mission.Say("Reach the northern jetty. Board the waiting boat.",8);
        }
        private void BeginBoarding()
        {
            mission.CloseGameplayPanel(); mission.Cinematic=true; quest.BeginBoarding();
            if(mission.ModernHealth!=null)mission.ModernHealth.CinematicInvulnerable=true;
            skipHeld=0;skipReleased=false;
            SetPlayerDrivers(false);
            var controller=mission.player.GetComponent<CharacterController>();
            if(controller!=null) controller.enabled=false;
            initialPlayerPosition=mission.player.position; initialPlayerRotation=mission.player.rotation;
            urgentRun=gameplayRifleRun??namActor.runtimeAnimatorController.animationClips.FirstOrDefault(c=>c.name=="Nam_Rifle_Run");
            if(urgentRun==null)Debug.LogError("Assign the gameplay Nam_Rifle_Run clip to extraction; a generic run is not suitable.");
            urgentJump=Resources.Load<AnimationClip>("Cutscenes/Nam_Urgent_Board_Jump");
            var walk=urgentRun??board;
            namPose=new PosePlayer(namActor);
            namPose.Play(walk);
            hungPose.Play(lowerWeapon); commanderPose.Play(lowerWeapon);
            speech=DialogueVoice.For(gameObject);speech.Play("m1_board_invite");
            subtitle=DialogueVoice.Caption("m1_board_invite");
            PrepareShoreActors();
            ChangePhase(Phase.Approach);
        }
        private void ChangePhase(Phase phase) { CurrentPhase=phase; clock=0; }
        private void SetPlayerDrivers(bool enabled)
        {
            if(mission.ModernPlayer!=null) mission.ModernPlayer.enabled=enabled;
            if(mission.ModernCombat!=null) mission.ModernCombat.enabled=enabled;
        }
        private void StartDeparture()
        {
            Seat(mission.player,0);
            mission.hung.localRotation=Quaternion.identity;
            foreach(var weapon in namActor.GetComponentsInChildren<Weapon>(true)) weapon.gameObject.SetActive(weapon.IsGun);
            if(!seatedRifles.Any(p=>p.actor==namActor)) EnsureRifle(namActor);
            namPose.Play(seatedReady); hungPose.Play(rowLoop); commanderPose.Play(seatedReady);
            var hg=hungActor.GetComponent<Map01Rifle>();if(hg!=null)hg.weapon.gameObject.SetActive(false);
            oarHeld=true;SpawnPursuitIfNeeded();
            speech=DialogueVoice.For(gameObject);speech.Play("m1_depart_order");
            subtitle=DialogueVoice.Caption("m1_depart_order");
            ChangePhase(Phase.Departing); nextShot=Time.unscaledTime+.8f; wake.Play();
        }
        private void ApplyDeparture(float progress)
        {
            float rhythm=(progress+.12f*(1-Mathf.Cos(progress*Mathf.PI*15))/(Mathf.PI*15))/(1+.24f/(Mathf.PI*15));
            float eased=rhythm*rhythm*(3-2*rhythm);
            var position=RiverPoint(departurePath,eased);
            var direction=progress<.998f ? RiverPoint(departurePath,Mathf.Min(1,eased+.002f))-position
                : position-RiverPoint(departurePath,Mathf.Max(0,eased-.002f));
            boat.position=position+Vector3.up*(Mathf.Sin(progress*38)*.025f);
            if(direction.sqrMagnitude>.0001f) boat.rotation=Quaternion.LookRotation(direction.normalized)*Quaternion.Euler(0,0,Mathf.Sin(progress*29)*1.1f);
            var focus=boat.position+Vector3.up*.8f;
            View(Vector3.Lerp(boatOrigin+new Vector3(5.4f,3.1f,-3.5f),boatOrigin+new Vector3(8,5,-7),Mathf.SmoothStep(0,1,progress)),focus,Mathf.Lerp(48,58,progress));
        }
        private void FinishDeparture()
        {
            speech?.Stop();
            ApplyDeparture(1); wake.Stop(); motor.Stop();
            namPose.Play(travel);hungPose.Play(rowStop);commanderPose.Play(travel);
            namPose.Settle();hungPose.Settle(true);commanderPose.Settle();
            clock=18;LateUpdate(); // Freeze the same final grip for playback and skip.
            foreach(var enemy in pursuit)if(enemy!=null)Destroy(enemy);pursuit.Clear();
            foreach(var enemy in shoreEnemies)if(enemy!=null)enemy.enabled=true;shoreEnemies.Clear();
            if(mission.ModernHealth!=null)mission.ModernHealth.CinematicInvulnerable=false;
            CurrentPhase=Phase.Complete; subtitle=null; mission.Cinematic=false;
            quest.CompleteExtraction();
            var save=mission.GetComponent<Map01SaveSystem>();
            if(!save.AutoSaveOnExit(out var error)) { Debug.LogError(error); mission.Say(error,12); }
            ForestMenu.SuppressKeysAfterCutscene(); Cursor.lockState=CursorLockMode.None; Cursor.visible=true;
        }
        public void Skip()
        {
            if(CurrentPhase==Phase.Radio) { EndRadio(); return; }
            if(CurrentPhase<Phase.Approach || CurrentPhase>Phase.Departing) return;
            // Same final transforms, quest commit and save as natural playback. No scene load.
            if(CurrentPhase!=Phase.Departing) StartDeparture();
            FinishDeparture();
        }
        private void BoardingView() {
            var wide=new Vector3(5.4f,3.1f,83.5f);var close=new Vector3(3.5f,2.1f,84.2f);
            float detail=CurrentPhase==Phase.Boarding?Mathf.SmoothStep(0,1,clock/2):CurrentPhase==Phase.Seating?1-Mathf.SmoothStep(0,1,clock/SeatingSeconds):0;
            View(Vector3.Lerp(wide,close,detail),Vector3.Lerp(new Vector3(.7f,.7f,87),new Vector3(.25f,.85f,87),detail),Mathf.Lerp(48,44,detail));
        }
        private void View(Vector3 position,Vector3 focus,float fov) => cameraRig.SetCinematicView(position,Quaternion.LookRotation(focus-position),fov,1);
        public static Vector3 Along(Vector3[] anchors,float progress)
        {
            float p=Mathf.Clamp01(progress)*(anchors.Length-1);
            int i=Mathf.Min((int)p,anchors.Length-2);
            return Vector3.Lerp(anchors[i],anchors[i+1],p-i);
        }
        public static Vector3 RiverPoint(Vector3[] points,float progress)
        {
            float p=Mathf.Clamp01(progress)*(points.Length-1);int i=Mathf.Min((int)p,points.Length-2);float t=p-i;
            var a=points[Mathf.Max(0,i-1)];var b=points[i];var c=points[i+1];var d=points[Mathf.Min(points.Length-1,i+2)];
            return .5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t);
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
        private void OnDestroy() { foreach(var enemy in shoreEnemies)if(enemy!=null)enemy.enabled=true; if(wakeMaterial!=null)Destroy(wakeMaterial);if(wakeTexture!=null)Destroy(wakeTexture); namPose?.Dispose(); hungPose?.Dispose(); commanderPose?.Dispose(); if(mission!=null && mission.ModernHealth!=null)mission.ModernHealth.CinematicInvulnerable=false; }

        public sealed class PosePlayer : IDisposable
        {
            private PlayableGraph graph;
            private AnimationPlayableOutput output;
            private AnimationClipPlayable playable;
            private AnimationClipPlayable previous;
            private AnimationMixerPlayable mixer;
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
                graph.Evaluate(dt);
            }
            public void Rate(float rate) {if(playable.IsValid())playable.SetSpeed(rate);}
            public double Time => playable.IsValid()?playable.GetTime():0;
            public void Sample() { if(graph.IsValid()) graph.Evaluate(0); }
            public void Settle(bool end=false) {
                blend=1;mixer.SetInputWeight(0,0);mixer.SetInputWeight(1,1);
                if(playable.IsValid()) {playable.SetTime(end?selected.length:0);playable.SetSpeed(0);}
                graph.Evaluate(0);
            }
            public void Dispose() {
                if(graph.IsValid()) graph.Destroy();
                if(actor!=null) { actor.runtimeAnimatorController=controller;actor.applyRootMotion=rootMotion; actor.cullingMode=culling; }
            }
        }
    }
}
