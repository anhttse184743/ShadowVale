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
        private int proceduralFrame=-1;
        private readonly float[] aimUntil=new float[2];
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
        private int shots;
        private readonly List<(Animator actor, Weapon rifle)> seatedRifles = new();

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
                mission.CloseGameplayPanel(); mission.Cinematic = true;
                SetPlayerDrivers(false);
                namPose = new PosePlayer(namActor); namPose.Play(radio);
                subtitle = "Commander (radio): Nam, move to the northern jetty. Hung and I are aboard. We will cover you.";
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
            if (mission == null || CurrentPhase == Phase.Dormant || CurrentPhase == Phase.Complete) return;
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
                    subtitle = clock<4.5f ? "Commander (radio): Nam, move to the northern jetty. Hung and I are aboard. We will cover you."
                        : "Nam: Understood. Moving to the boat.";
                    var focus = mission.player.position+Vector3.up*1.35f;
                    View(focus-mission.player.forward*2.2f+mission.player.right*1.5f,focus,49);
                    if(clock>=Mathf.Max(7,radio.length)) EndRadio();
                    break;
                case Phase.Approach:
                    float p=Mathf.Clamp01(clock/1.2f);
                    mission.player.SetPositionAndRotation(Vector3.Lerp(initialPlayerPosition,boardingAnchors[0],p),
                        Quaternion.Slerp(initialPlayerRotation,Quaternion.Euler(0,90,0),p));
                    BoardingView();
                    if(p>=1) { ChangePhase(Phase.Boarding); namPose.Play(board); }
                    break;
                case Phase.Boarding:
                    mission.player.position=Along(boardingAnchors,clock/board.length);
                    BoardingView();
                    if(clock>=board.length) { ChangePhase(Phase.Seating); namPose.Play(sit); }
                    break;
                case Phase.Seating:
                    mission.player.rotation=Quaternion.Slerp(Quaternion.Euler(0,90,0),Quaternion.Euler(0,270,0),Mathf.SmoothStep(0,1,clock/sit.length));
                    BoardingView();
                    if(clock>=sit.length) StartDeparture();
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
            if(proceduralFrame==Time.frameCount)return;
            proceduralFrame=Time.frameCount;
            if(CurrentPhase==Phase.Departing) for(int index=0;index<2;index++) {
                if(Time.unscaledTime>aimUntil[index])continue;
                var actor=index==0?namActor:commanderActor;
                var chest=actor.GetBoneTransform(HumanBodyBones.Chest);
                var direction=lastTargets[index]+Vector3.up-chest.position;
                float yaw=Mathf.Clamp(Vector3.SignedAngle(actor.transform.forward,Vector3.ProjectOnPlane(direction,Vector3.up),Vector3.up),-55,55);
                chest.rotation=Quaternion.AngleAxis(yaw,Vector3.up)*chest.rotation;
            }
            foreach(var pursuer in pursuit) {
                if(pursuer==null)continue;
                var agent=pursuer.GetComponent<NavMeshAgent>();var actor=pursuer.GetComponentInChildren<Animator>();
                actor.SetFloat(PlayerCombat.AnimatorParams.Speed,agent.velocity.magnitude);
                if(agent.velocity.sqrMagnitude<.01f)pursuer.transform.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(boat.position-pursuer.transform.position,Vector3.up));
                if(Time.unscaledTime>=nextEnemyShot) {
                    nextEnemyShot=Time.unscaledTime+1.4f;
                    actor.SetTrigger(PlayerCombat.AnimatorParams.Attack);
                    var rifle=actor.GetComponent<Map01Rifle>();
                    if(rifle!=null)mission.Trace(rifle.weapon.Muzzle.position,boat.position+boat.right*1.2f,new Color(1,.7f,.3f));
                }
            }
            if(oar==null || !oarHeld) return;
            // Correct Humanoid arm-length retargeting at the prop contacts; keep the authored torso/legs.
            float phase=CurrentPhase==Phase.Departing?Mathf.Min(clock,16)*Mathf.PI*2/2.4f:0;
            float reach=.24f*Mathf.Cos(phase);
            // Catch -> submerged pull -> lift -> recovery. The inboard hand is the top grip.
            float lift=Mathf.Max(0,-Mathf.Sin(phase))*.20f;
            if(CurrentPhase==Phase.Departing && clock>16)lift+=Mathf.SmoothStep(0,.24f,(clock-16)/2);
            float contact=CurrentPhase==Phase.Boarding?Mathf.SmoothStep(0,1,(clock-2.6f)/.9f):1;
            var rowingChest=hungActor.GetBoneTransform(HumanBodyBones.Chest);
            rowingChest.rotation=Quaternion.AngleAxis((12+5*Mathf.Cos(phase))*contact,boat.up)*rowingChest.rotation;
            PaddleArm(HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftHand,
                boat.TransformPoint(new Vector3(.08f,1.04f+lift,-1.6f+reach)),contact,-1);
            PaddleArm(HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand,
                boat.TransformPoint(new Vector3(.48f,.72f+lift,-1.55f+reach)),contact,1);
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
                Vector3 shaft=boat.TransformDirection(new Vector3(.40f,-.32f,.05f)).normalized;
                Vector3 gripFacing=Vector3.ProjectOnPlane(forearm,shaft).normalized;
                if(gripFacing.sqrMagnitude>.1f) hand.rotation=Quaternion.FromToRotation(fingers,gripFacing)*hand.rotation;
            }
        }
        private static Vector3 PaddleGrip(Transform hand) => hand.childCount>0
            ? Vector3.Lerp(hand.position,hand.GetChild(0).position,.45f) : hand.position;
        private void UpdateRowing()
        {
            if(CurrentPhase==Phase.Approach) hungPose.Play(rifleStow);
            if(CurrentPhase==Phase.Boarding) {
                if(clock>1.7f) {
                    var gun=hungActor.GetComponent<Map01Rifle>();if(gun!=null)gun.weapon.gameObject.SetActive(false);
                    oarHeld=clock>2.6f;hungPose.Play(oarPickup);
                    mission.hung.localRotation=Quaternion.Slerp(Quaternion.Euler(0,270,0),Quaternion.identity,Mathf.SmoothStep(0,1,(clock-1.7f)/2));
                }
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
            if(mission.Enemies.Any(e=>e.Alive && Vector3.Distance(e.transform.position,boat.position)<35)) return;
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
                if(agent.isOnNavMesh) {agent.isStopped=false;agent.SetDestination(chosen+Vector3.forward*4);}
                pursuit.Add(clone);
            }
        }
        private void CoverDeparture()
        {
            if(Time.unscaledTime<nextShot)return;
            nextShot=Time.unscaledTime+.28f;int index=shots++%2;
            var actor=index==0?namActor:commanderActor;var pose=index==0?namPose:commanderPose;
            var candidates=mission.Enemies.Where(e=>e.Alive).Select(e=>e.transform)
                .Concat(pursuit.Where(p=>p!=null).Select(p=>p.transform));
            var gun=actor.GetComponent<Map01Rifle>();if(gun==null)return;
            var origin=gun.weapon.Muzzle.position;
            var target=candidates.Where(t=>Vector3.Distance(t.position,boat.position)<38)
                .OrderBy(t=>Vector3.Distance(t.position,origin)+(Vector3.Distance(t.position,lastTargets[1-index])<1?12:0))
                .FirstOrDefault(t=>SafeShot(actor,origin,t.position+Vector3.up));
            if(target==null) {pose.Play(seatedReady);return;}
            var end=target.position+Vector3.up;lastTargets[index]=target.position;aimUntil[index]=Time.unscaledTime+1;
            if(shots%20==0) {pose.Play(seatedReload);nextShot+=2.6f;return;}
            pose.Play(seatedFire,true);
            mission.Trace(origin,end,new Color(1,.8f,.35f));
            if(rifleAudio!=null)motor.PlayOneShot(rifleAudio,.4f);
            var flash=new GameObject("Cinematic muzzle flash");flash.transform.position=origin;
            var light=flash.AddComponent<Light>();light.color=new Color(1,.7f,.25f);light.range=2;light.intensity=2;Destroy(flash,.045f);
            if(shots%6==0)nextShot+=.65f;
        }
        private bool SafeShot(Animator actor,Vector3 from,Vector3 to)
        {
            var local=boat.InverseTransformPoint(to);
            if(local.x>-.9f)return false; // Shore-side sectors only, never down the passenger row.
            foreach(var other in new[]{namActor,hungActor,commanderActor}) {
                if(other==actor)continue;
                var p=other.GetBoneTransform(HumanBodyBones.Chest).position;
                var d=to-from;float t=Mathf.Clamp01(Vector3.Dot(p-from,d)/d.sqrMagnitude);
                if(Vector3.Distance(p,from+d*t)<.5f)return false;
            }
            if(Physics.Linecast(from,to,out var hit,mission.ObstructionMask,QueryTriggerInteraction.Ignore)
                && hit.collider.GetComponentInParent<Map01EnemyController>()==null)return false;
            return Vector3.Angle(actor.transform.forward,to-from)<75;
        }
        private void CoverShore()
        {
            if(Time.time<nextShot) return;
            nextShot=Time.time+.9f;
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
            mission.Trace(origin,end,new Color(1,.8f,.4f));
            target.GetComponent<Health>().TakeDamage(8,end,actor.gameObject);
        }

        private void EndRadio()
        {
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
            var walk=namActor.runtimeAnimatorController.animationClips.FirstOrDefault(c=>c.name.IndexOf("Walk",StringComparison.OrdinalIgnoreCase)>=0);
            namPose=new PosePlayer(namActor);
            namPose.Play(walk);
            hungPose.Play(rifleStow); commanderPose.Play(lowerWeapon);
            subtitle="Hung: The route is clear. Come aboard, Nam.";
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
            subtitle="Commander: Everyone aboard. Move out.";
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
            ApplyDeparture(1); wake.Stop(); motor.Stop();
            namPose.Play(travel);hungPose.Play(rowStop);commanderPose.Play(travel);
            namPose.Settle();hungPose.Settle(true);commanderPose.Settle();
            clock=18;proceduralFrame=-1;LateUpdate(); // Freeze the same final grip for playback and skip.
            foreach(var enemy in pursuit)if(enemy!=null)Destroy(enemy);pursuit.Clear();
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
            float detail=CurrentPhase==Phase.Boarding?Mathf.SmoothStep(0,1,clock/2):CurrentPhase==Phase.Seating?1-Mathf.SmoothStep(0,1,clock/sit.length):0;
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
        private void OnDestroy() { if(wakeMaterial!=null)Destroy(wakeMaterial);if(wakeTexture!=null)Destroy(wakeTexture); namPose?.Dispose(); hungPose?.Dispose(); commanderPose?.Dispose(); if(mission!=null && mission.ModernHealth!=null)mission.ModernHealth.CinematicInvulnerable=false; }

        private sealed class PosePlayer : IDisposable
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
                if(!selected.isLooping && playable.GetTime()+dt>=selected.length) { playable.SetTime(selected.length); playable.SetSpeed(0); }
                graph.Evaluate(dt);
            }
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
