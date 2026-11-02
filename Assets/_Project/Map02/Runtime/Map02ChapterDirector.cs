using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Player;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace ShadowVale.Map01
{
    /// <summary>Playable chapter, separate from Map 1 and the retired evacuation prototype.</summary>
    public sealed partial class Map02ChapterDirector : MonoBehaviour,IBinoculars
    {
        public event Action<string,float> SceneSampled;
        public event Action<string> TaskCompleted;
        public Map02ChapterActor Actor(string role)=>roles.TryGetValue(role,out var actor)?actor:null;
        public Map02ChapterState State {get;private set;}=new Map02ChapterState();
        public Map02Mission Mission {get;private set;}
        public bool Busy=>EvacuationCaptureActive||EvacuationModal||NightPatrolFailureActive||sceneTask!=null||doingWork||DayActivityActive||UndergroundStore?.IsClimbing==true||stepsCut;   // the dock-steps walk is a cutscene too
        public bool ConversationActive=>sceneTask!=null;
        public bool Binoculars=>binocular;
        bool binocularHeld;
        public void HoldBinoculars(bool held){binocularHeld=held;SetBinoculars(held&&!Mission.InputBlocked);}
        void SetBinoculars(bool held){if(ScoutEscortActive||held&&player?.IsProne==true&&EvacuationChoicesActive&&State.night2EvacuationChoice==1)held=false;if(binocular==held)return;binocular=held;rig?.SetBinoculars(held,held?2:1);}
        public bool LocksInput=>Busy||DayReturningInput||State.finished||Transitioning;
        public bool MovementLocked=>DuckStartled;   // Night 3 no longer holds Nam in place: the cover fire (N3_08) only slows him (CoverPace)
        /// <summary>Covering Hùng (N3_08): Nam can shift along the bank, slowly, never sprint.</summary>
        public bool CoverFireActive=>State.activeTask=="N3_08"&&!State.Done("N3_08");
        public const float CoverPace=.45f;
        public bool MaySave=>!Busy&&State.patrolFocusSeconds<=0;
        public string SaveReason=>"Hãy hoàn tất thao tác hoặc hội thoại trước khi lưu.";
        public IReadOnlyDictionary<string,Transform> Sites=>sites;
        public Map02ChapterTask CurrentScene=>sceneTask;
        public List<Map02ChapterEnemy> Enemies {get;}=new List<Map02ChapterEnemy>();
        public Map02ChapterLayout Layout {get;private set;}
        float holdClock,detectionClock;Vector3 workOrigin;AudioSource signalAudio;
        Map01CinematicPose namPose;AnimationClip namIdle;readonly List<GameObject> hiddenWeapons=new List<GameObject>();
        float arrivalBriefingFloor;
        Material originalSky,chapterSky;UnityEngine.Rendering.AmbientMode originalAmbientMode;Color originalSunColor;
        readonly Dictionary<string,Transform> sites=new Dictionary<string,Transform>();
        readonly Dictionary<string,Map02ObjectiveObject> markers=new Dictionary<string,Map02ObjectiveObject>();
        readonly Dictionary<string,Map02ChapterActor> roles=new Dictionary<string,Map02ChapterActor>();
        Map02ChapterTask sceneTask,workTask;
        Map02DialogueCatalog.Entry[] lines;
        int scenePurpose;int lineIndex;float lineClock,sceneClock,skipHeld,workClock,signal=.15f,signalHold,scannedAt=-99,patrolLoadGrace,observed,spawnRetry;
        int phaseApplied=-1;
        readonly Dictionary<string,Vector3> initialRolePositions=new Dictionary<string,Vector3>();
        bool doingWork,heldReleased,binocular,protectedBefore,ready,handsBusy;
        Vector3 lockedPosition;Quaternion lockedRotation;WeaponKind equippedBefore;
        Map02ChapterActor escort,secondaryEscort;float phaseFade;
        public bool Transitioning=>phaseFade>0;
        bool placedActors;int placedPhase=-1;
        DialogueVoice voice;
        ThirdPersonCamera rig;
        Health health;PlayerCombat combat;PlayerController player;
        Vector3 cameraBefore;Quaternion cameraRotation;float cameraFov;
        readonly List<Light> lamps=new List<Light>();Light sweepLight;
        Light sun;Color ambientBefore;float sunIntensity;Quaternion sunRotation;bool fogBefore;Color fogColorBefore;float fogDensityBefore;
        GameObject root,tank;ParticleSystem tankSmoke;
        public bool IsAvailable(Map02ObjectiveObject target)=>target!=null&&!string.IsNullOrEmpty(target.chapterId)&&State.Available(Map02ChapterDefinition.Load().Find(target.chapterId));
        public void Initialize(Map02Mission mission,Map02QuestLayout layout)
        {
            Layout=Resources.Load<Map02ChapterLayout>("Story/Map02ChapterLayout");
            Mission=mission;player=mission.Player.GetComponent<PlayerController>();combat=mission.Player.GetComponent<PlayerCombat>();health=mission.Player.GetComponent<Health>();
            combat.EquipAllowed=kind=>!ScoutEscortActive||kind==WeaponKind.Pistol;
            rig=FindObjectsByType<ThirdPersonCamera>(FindObjectsSortMode.None).First(c=>c.GetComponent<Camera>()?.enabled==true);
            voice=DialogueVoice.For(gameObject);voice.CanSpeak=()=>!mission.Paused&&!ForestMenu.Visible;
            root=new GameObject("Map 2 chapter markers — replace art under Actor slots");root.transform.SetParent(transform);
            var legacy=FindObjectsByType<Map02ObjectiveObject>(FindObjectsSortMode.None).Where(t=>string.IsNullOrEmpty(t.chapterId)).ToArray();
            Vector3 Legacy(Map02Work w,Vector3 fallback)=>legacy.FirstOrDefault(o=>o.work==w)?.standPoint?.position??fallback;
            Vector3 command=Legacy(Map02Work.Contact,player.transform.position+Vector3.forward*8),dock=layout.southDock.position;
            Site("command",command);Site("gate",Legacy(Map02Work.Contact,command)+Vector3.forward*9);
            Site("family",Legacy(Map02Work.MedicineDelivery,command));Site("store",Legacy(Map02Work.CrateDelivery,command));
            Site("arrival",Legacy(Map02Work.CratePickup,player.transform.position));Site("dock",dock);
            Site("southStore",layout.southAssembly.position);Site("ditch",dock+Vector3.left*5);Site("radio",command+Vector3.right*5);
            Site("dayOneFamily",new Vector3(-50.7f,1.03f,-35.7f));Site("nightOneRest",sites["dayOneFamily"].position+Vector3.right*3);
            Site("rest",command+Vector3.back*4);Site("bamboo",command+Vector3.right*14);
            Site("scout",Legacy(Map02Work.ScoutAid,command));Site("bridge",sites["scout"].position+Vector3.forward*12);
            Site("nightPatrol",sites["bridge"].position+new Vector3(1,0,-1).normalized*35);
            InitializeNightBankRoute();
            player.MovementConstraint=ConstrainNightMovement;
            Site("child",Legacy(Map02Work.RescueNeighbours,command));
            Site("lane1",command+Vector3.forward*14);Site("lane2",command+Vector3.forward*5);Site("lane3",command+Vector3.back*5);
            Site("cover",sites["lane3"].position+Vector3.left*5);Site("fallen",dock+Vector3.forward*8);
            for(int i=0;i<3;i++)Site("family"+i,layout.homePositions[Mathf.Min(i,layout.homePositions.Length-1)]);
            RegisterNightThreeSites();
            foreach(var rice in FindObjectsByType<VillageRiceInstances>(FindObjectsSortMode.None))rice.ClearAccess(sites["scout"].position,sites["bridge"].position,3.2f,.85f);
            foreach(var old in legacy)
            {
                old.enabled=false;
                if(old.work!=Map02Work.ScoutAid)continue;
                // Retire the prototype cube once the actual injured scout owns this interaction.
                foreach(var renderer in old.GetComponentsInChildren<Renderer>(true))renderer.enabled=false;
                foreach(var collider in old.GetComponentsInChildren<Collider>(true))collider.enabled=false;
            }

            foreach(var t in Map02ChapterDefinition.Load().tasks)
            {
                var go=new GameObject(t.id+" "+t.title);go.transform.SetParent(root.transform);go.transform.position=DayTaskPoint(t);
                var marker=go.AddComponent<Map02ObjectiveObject>();marker.saveId="m2v3_"+t.id;marker.chapterId=t.id;marker.prompt=t.title;marker.radius=3;marker.standPoint=go.transform;markers.Add(t.id,marker);
            }
            markers["N3_CRAWL"].transform.position=HiddenFromTank(sites["cover"].position);   // he answers Hùng from behind a wall, not in the lane
            var arrival=FindFirstObjectByType<Map02Arrival>();
            foreach(var role in new[]{"Anh Thành","Anh Lực","Hùng","Chị Lành","Ông Sáu","Bé Tí","Chú Bảy","Tâm",Guard,SeatedGuard})
            {
                var actor=new GameObject("Actor slot — "+role);actor.transform.SetParent(root.transform);var slot=actor.AddComponent<Map02ChapterActor>();slot.role=role;roles.Add(role,slot);var art=Layout?.actors?.FirstOrDefault(a=>a.role==role)?.prefab;
                if(art==null&&(role==Guard||role==SeatedGuard))art=GuardArt;
                if(art!=null&&!(arrival?.Passengers?.Length>=3&&(role=="Hùng"||role=="Anh Thành")))slot.Bind(Instantiate(art).transform);
            }
            TintGuards();
            if(arrival?.Passengers?.Length>=3)
            {
                if(roles["Hùng"].visual==null)roles["Hùng"].Bind(arrival.Passengers[2]);
                if(roles["Anh Thành"].visual==null)
                {
                    roles["Anh Thành"].Bind(arrival.Passengers[1]);
                    // The boat commander's root is not at his soles: the arrival lifts it until his soles touch the bank.
                    // Put the slot (the floor his day locomotion grounds to) on the bank itself, and leave the model
                    // where the arrival stood it, so he neither floats once he moves nor sinks while the bank scene holds him.
                    if(arrival.CurrentPhase==Map02Arrival.Phase.Gameplay)
                    {
                        var thanh=roles["Anh Thành"];var stood=thanh.visual.position;thanh.StopAt(arrival.SpawnPoints[1]);thanh.visual.position=stood;
                    }
                }
                else
                {
                    // The boat uses Map 1's commander; the village uses Thành's named source model.
                    roles["Anh Thành"].StopAt(arrival.Passengers[1].position);
                    arrival.Passengers[1].gameObject.SetActive(false);
                }
            }
            if(roles["Hùng"].visual==null)
            {
                // Direct scene Play has no carried Map 1 party. Use the same authored Hùng visual.
                var hung=Instantiate(Resources.Load<Map01HungVisual>("Characters/HungVisual"));hung.enabled=false;
                var dimensions=Map01ActorAssets.Load();float scale=dimensions.namHeight/dimensions.hungHeight;
                hung.actor.transform.localScale=Vector3.one*scale;
                hung.actor.transform.localPosition=Vector3.up*(-dimensions.hungSole*scale);
                hung.actor.SetBool("Captive",false);hung.actor.SetBool("Wounded",true);hung.actor.SetBool("Dead",false);
                hung.actor.Play(Map01HungVisual.WoundedState,0,0);
                roles["Hùng"].Bind(hung.transform);roles["Hùng"].StopAt(sites["arrival"].position);
            }
            if(arrival?.Passengers==null){roles["Hùng"].StopAt(sites["arrival"].position+Vector3.right*1.4f);roles["Anh Thành"].StopAt(sites["arrival"].position+Vector3.left*1.4f);}
            foreach(var role in roles)initialRolePositions[role.Key]=role.Value.transform.position;
            State.storedRounds=0; // No ammo is invented on chapter entry.
            originalSky=RenderSettings.skybox;if(originalSky!=null){chapterSky=new Material(originalSky);RenderSettings.skybox=chapterSky;} originalAmbientMode=RenderSettings.ambientMode;
            ambientBefore=RenderSettings.ambientLight;fogBefore=RenderSettings.fog;fogColorBefore=RenderSettings.fogColor;fogDensityBefore=RenderSettings.fogDensity;
            sun=RenderSettings.sun!=null&&RenderSettings.sun.isActiveAndEnabled?RenderSettings.sun:FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l=>l.type==LightType.Directional).OrderByDescending(l=>l.intensity).FirstOrDefault();
            if(sun!=null){sunIntensity=sun.intensity;sunRotation=sun.transform.rotation;originalSunColor=sun.color;}
            CaptureNaturalLighting();
            foreach(var place in new[]{"command","family","gate","radio","dock","rest","bamboo","store"}){var lamp=new GameObject("Oil lamp light — "+place);lamp.transform.SetParent(root.transform);lamp.transform.position=sites[place].position+Vector3.up*2.6f;var light=lamp.AddComponent<Light>();light.type=LightType.Point;light.color=new Color(1,.65f,.26f);light.range=5;light.intensity=0;light.shadows=LightShadows.None;lamps.Add(light);}
            var search=new GameObject("Enemy searchlight");search.transform.SetParent(root.transform);sweepLight=search.AddComponent<Light>();sweepLight.type=LightType.Spot;sweepLight.range=55;sweepLight.spotAngle=24;sweepLight.intensity=3;sweepLight.color=new Color(.65f,.8f,1);sweepLight.enabled=false;
            combat.Attacked+=OnPlayerShot;
            EnsureDayState();LoadDayPerformances();BuildDayProps();BuildDayTwoRoute();BuildNightTwoRoute();BuildEvacuationChoices();ready=true;ApplyState();SyncDayProps();StartCoroutine(PreloadDayOneVoices());StartCoroutine(LoadChapterSoundscape());
        }
        void Site(string id,Vector3 position)
        {
            var authored=Layout?.sites?.FirstOrDefault(site=>site.id==id);if(authored!=null)position=authored.position;
            var go=new GameObject("Site_"+id);go.transform.SetParent(root.transform);
            if(NavMesh.SamplePosition(position,out var h,12,NavMesh.AllAreas))position=h.position;
            go.transform.position=position;sites.Add(id,go.transform);
        }
        int Inventory(string id)=>Mission.Inventory.TryGetValue(id,out int n)?n:0;
        public bool Interact(Map02ObjectiveObject marker)
        {
            if(!ready||Busy||Mission.InputBlocked||!IsAvailable(marker)||!marker.WithinReach(Mission.Player))return false;
            var t=Map02ChapterDefinition.Load().Find(marker.chapterId);
            if((!string.IsNullOrEmpty(State.carry)||escort!=null)&&State.activeTask!=t.id){Mission.Say("Hoàn tất việc đang mang hoặc dìu người trước.");return false;}
            if(State.activeTask!=t.id){State.activeTask=t.id;State.workCount=State.Count(t.id);State.taskProgress=0;}
            if((!string.IsNullOrEmpty(State.carry)||escort!=null)&&State.activeTask!=t.id)return false;
            workTask=t;State.trackedTask=t.id;
            if(RouteDayInteraction(t))return true;
            if(t.kind=="patrolReport"||t.kind=="inspectDoor"||t.kind=="inspectRoute")return InteractNightRoute(t);
            if(t.id=="N2_05"&&InteractNightRoute(t))return true;
            if(t.kind=="carry")
            {
                if(string.IsNullOrEmpty(State.carry)){State.carry=t.id;combat.Equip(WeaponKind.Unarmed);Mission.Say("Thùng đã được nhấc. Mang đến điểm đặt được đánh dấu.");}
                else {State.carry=null;State.workCount=State.Increment(t.id);Mission.Say("Đã đặt thùng "+State.workCount+"/"+t.count);if(State.workCount>=t.count){StoreRounds(t);Finish(t);} }
                UpdateMarkers();return true;
            }
            if(t.kind=="families") {if(!State.introduced.Contains(t.id)){BeginScene(t);scenePurpose=2;}else CompleteFamily(t);return true;}
            if(t.kind=="wire")
            {
                if(State.TakeWire()){UpdateMarkers();Mission.StoreCheckpoint();Mission.Say("Đã lấy cuộn dây và ăng-ten. Mang về trạm, nhấn E giao cho Hùng.");return true;}
                if(State.DeliverWire()){CommitFinishedTask(t);return true;}
                return false;
            }
            if(t.kind=="medicine"){if(!State.Done("D1_02"))return false;BeginScene(t);return true;}
            if(t.kind=="gear")
            {
                if(State.gearStored){Mission.Inventory[Mission.RifleAmmo]=Inventory(Mission.RifleAmmo)+State.storedRounds;State.storedRounds=0;State.gearStored=false;}
                if(t.id=="N3_01"&&Mission.IssueSpareRifle())Mission.Say("Nhận thêm một khẩu súng dự phòng trong túi. Súng đang cầm hỏng thì tự đổi sang khẩu này.");
                combat.Equip(WeaponKind.Rifle);Finish(t);return true;
            }
            if(t.kind=="escortHung"||t.kind=="escortScout"||t.kind=="patrolHung")
            {
                if(t.id=="D2_04"){StartScoutEscort();Mission.Say("Dìu Tâm về chốt · 1,8 m/s · chỉ dùng K54. Địch sẽ xuất hiện lần lượt từ hai bên đường ruộng.");UpdateMarkers();Mission.StoreCheckpoint();return true;}
                escort=roles[t.kind=="escortScout"?"Tâm":"Hùng"];escort.Follow(Mission.Player,t.id=="D1_02"?1.25f:1.35f,false);Mission.Say(escort.role=="Hùng"?"Đi bộ và giữ Hùng trong phạm vi 4 m. Đến điểm đánh dấu để hoàn tất.":"Tâm sẽ đi theo. Đưa cả hai tới điểm đánh dấu để hoàn tất.");UpdateMarkers();return true;
            }
            if(t.kind=="rescueFamily"||t.kind=="rescueChild")
            {
                if(!State.introduced.Contains(t.id)){BeginScene(t,0,2);scenePurpose=3;}else StartRescueEscort(t);return true;
            }
            if(t.kind=="defend"){StartDefend(t);return true;}
            if(t.kind=="coverFire"){StartCover(t);return true;}
            if(t.kind=="reach"||t.kind=="binocular"||t.kind=="crawl")return true;
            if(t.kind=="repair"||t.kind=="antenna"||t.kind=="clear"||t.kind=="demolition")
            {
                if(CarryCrate(t))return true;
                OpenWork(t);return true;
            }
            BeginScene(t);return true;
        }
        void StoreRounds(Map02ChapterTask t)
        {
            if(t.id!="D1_04")return;
            // Store the carried campaign ammunition, never refill a magic stockpile.
            State.storedRounds+=(Layout!=null?Layout.supplyCrateRounds:30)*3;State.gearStored=true;
        }
        void CompleteFamily(Map02ChapterTask task){State.workCount=State.Increment(task.id);if(State.workCount>=task.count)Finish(task);else {Mission.Say("Đã phân công "+State.workCount+"/3 nhà.");UpdateMarkers();Mission.StoreCheckpoint();}}
        void OpenWork(Map02ChapterTask task)
        {
            if(!string.IsNullOrEmpty(task.scene)&&!State.introduced.Contains(task.id)){
                BeginScene(task,0,task.kind=="antenna"?1:task.kind=="clear"?3:0);scenePurpose=1;
            }else StartWork(task);
        }
        void StartWork(Map02ChapterTask task)
        {
            Cursor.visible=false;Cursor.lockState=CursorLockMode.Locked;doingWork=true;workTask=task;workClock=holdClock=0;heldReleased=false;signalHold=0;workOrigin=Mission.Player.position;
            if(task.kind=="antenna"){signalAudio=gameObject.AddComponent<AudioSource>();signalAudio.clip=Resources.Load<AudioClip>("OperationsAudio/radio_off");signalAudio.loop=true;signalAudio.volume=.2f;signalAudio.Play();}
            equippedBefore=combat.EquippedKind;combat.Equip(WeaponKind.Unarmed);player.RestoreMotion(false);
            if(task.kind=="antenna")StartAntennaTraversal();
            if(IsPatch(task))BeginPatch();
        }
        void Update()
        {
            bool hush=!ready||Mission.Paused||ForestMenu.Visible||Mission.World.story.failed;
            PauseMealCrickets(hush);PauseSupperSound(hush);TickAmbience(Mathf.Min(Time.deltaTime,.1f),hush);
            if(ready)SyncNightAudioPause();
            TickChapterSoundscape();
            if(ready)SetBinoculars((binocularHeld||Keyboard.current?.fKey.isPressed==true)&&!Mission.InputBlocked&&!ForestMenu.Visible);
            if(nightWarningAudio!=null){if(Mission.Paused||ForestMenu.Visible)nightWarningAudio.Pause();else nightWarningAudio.UnPause();}
            if(!ready||Mission.Paused||ForestMenu.Visible)return;
            float dt=Mathf.Min(Time.deltaTime,.1f);
            if(EvacuationCaptureActive){TickEvacuationCapture(dt);return;}
            if(NightPatrolFailureActive){TickNightPatrolFailure(dt);return;}
            if(Mission.World.story.failed)return;
            if(EvacuationModal){if(Mission.Panel==0)TickEvacuationModal(dt);return;}
            TickNightStoreArrival();
            if(State.phase==3&&NightPatrolStopped&&!State.Done("N2_05"))TickNightRadio(dt,180,false,100);
            TickEffects();
            TickScoutEscort();
            if(UndergroundStore?.IsClimbing==true)return;
            if(phaseFade>0){phaseFade=Mathf.Max(0,phaseFade-dt);TickLight(dt);return;}
            if(State.finished)return;
            TickTank(dt);TickBoats(dt);TickDressing(dt);TickStepsRehearsal(dt);TickAllies();TickWreck(dt);TickBleeding(dt);
            bool leading=LeadingWounded; // Day 3 / Night 3 escorts and Tâm's keep Nam armed and at pace.
            bool carrying=!string.IsNullOrEmpty(State.carry)||leading||DayActivityActive;if(carrying!=handsBusy||carrying&&!combat.HandsOccupied){handsBusy=carrying;combat.SetHandsOccupied(carrying);}   // a bag refresh (each save) frees the hands
            player.SurfaceSpeedMultiplier=leading?EscortPace:!string.IsNullOrEmpty(State.carry)?CarryPace:CoverFireActive?CoverPace:State.wounded?WoundedPace:1;
            player.SprintAllowed=()=>!leading&&!CoverFireActive&&(CarrySprintForTesting||string.IsNullOrEmpty(State.carry))&&Mission.CharacterCanSprint;
            if(sceneTask!=null){TickLight(dt);TickScene(dt);return;}
            if(TickDay(dt))return;
            TickBark(dt);
            if(doingWork){TickWork(dt);return;}
            if(Mission.Panel!=0)return;
            TickLight(dt);TickCrawlCall(dt);TickAmmo();TickNightTwoRoute(dt);
            RefreshDayTwoProps();
            if(State.phase==5&&State.Done("N3_00")&&!State.Done("N3_04"))EnsureGroup("initial_attack",4);
            if(State.wounded&&!Mission.Cinematic)rig.AddRecoil(Mathf.Sin(Time.time*2)*dt*.08f,Mathf.Sin(Time.time*3)*dt*.06f);
            foreach(var t in State.AvailableTasks.ToArray())
            {
                var mark=markers[t.id];bool near=Vector3.Distance(Mission.Player.position,mark.transform.position)<mark.radius;
                if(t.kind=="reach"&&near){Finish(t);break;}
                if(t.id=="N3_07"&&Vector3.Distance(Mission.Player.position,sites["ditch"].position)<6){BeginScene(t);break;} // The last boat is pinned the moment Nam reaches the bank.
                if(t.id=="D2_02")EnsureGroup("scout_patrol",3,false);
                if((t.kind=="escortHung"||t.kind=="escortScout"||t.kind=="patrolHung"||t.kind.StartsWith("rescue"))&&escort!=null&&workTask==t)
                {
                    if(t.id=="D2_04"&&!ScoutAmbushCleared)continue;
                    var dest=Destination(t);if(Vector3.Distance(Mission.Player.position,dest)<3&&Vector3.Distance(escort.transform.position,dest)<4&&(secondaryEscort==null||Vector3.Distance(secondaryEscort.transform.position,dest)<5))
                    {if(t.id=="D2_04")EndScoutEscort();bool rescue=t.kind.StartsWith("rescue");escort.StopAt(rescue?SafeSpot(escort.role):dest+(t.id=="D1_02"?Vector3.forward*.6f:Vector3.right*1.4f));escort=null;secondaryEscort?.StopAt(rescue?SafeSpot(secondaryEscort.role):dest+Vector3.left*1.4f);secondaryEscort=null;if(t.kind=="rescueFamily")Finish(t);else if(t.kind=="rescueChild")BeginScene(t,2);else if(t.id=="N1_02"||t.id=="N1_03")StartPost(t);else BeginScene(t);break;}
                }
                if(t.kind=="binocular")
                {
                    if(TryReachNightObservationCheckpoint(Mission.Player.position))Mission.StoreCheckpoint();
                    if(TickNightObservation(dt,t.seconds)){Finish(t);break;}
                }
                if(t.kind=="crawl")
                {
                    // Wounded, he keeps low (the crouch, held for him) and makes his own way to anh Lực's cover.
                    if(!player.IsSneaking)player.RestoreMotion(true);
                    if(player.IsMoving)State.taskProgress+=dt;
                    if(Vector3.Distance(Mission.Player.position,mark.transform.position)<1.5f){health.CinematicInvulnerable=false;BeginScene(t);break;}
                }
                if(t.kind=="defend")
                {
                    // Holding starts on reaching the lane, never by pressing E.
                    if(State.activeTask!=t.id&&string.IsNullOrEmpty(State.carry)&&escort==null&&InLane(t))StartDefend(t);
                    if(State.activeTask==t.id&&TickLane(t,dt))break;
                }
                if(t.kind=="coverFire"&&State.activeTask==t.id&&TickCover(t,dt))break;
            }
        }
        Vector3 SupperYard=>sites["family"].position+Vector3.left*2;
        /// <summary>Where each of Lành's family waits at the south shelter once brought there: on the far side from the bank
        /// hole, which shares the shelter's spot and where Hùng keeps watch. Bé Tí stays just behind her mother.</summary>
        Vector3 SafeSpot(string role){var lanh=sites["southStore"].position+Vector3.left*1.4f;return role=="Chị Lành"?lanh:role=="Bé Tí"?BesideMother(lanh):lanh+Vector3.left*1.2f+Vector3.forward*1f;}
        static Vector3 BesideMother(Vector3 mother)=>mother+Vector3.back*1f+Vector3.left*.5f;   // behind her, clear of whoever stands at her right
        Vector3 Destination(Map02ChapterTask t)=>t.kind.StartsWith("rescue")?sites["southStore"].position:DayTaskPoint(t);
        public void UpdateMarkers()
        {
            RefreshDayTwoProps();
            foreach(var t in Map02ChapterDefinition.Load().tasks)
            {
                var mark=markers[t.id];Vector3 pos=DayTaskPoint(t);
                if(t.kind=="antenna"&&antennaBase!=null)pos=antennaBase.position;
                if(t.kind=="wire"&&State.wireStage==1)pos=sites["radio"].position;
                if(t.kind=="inspectDoor"){pos=NightDoorStand;mark.radius=1.8f;}
                if(t.kind=="inspectRoute"){pos=NightRouteMarker;mark.radius=NightTwoRouteProgress==3?1.8f:2.4f;}
                if(t.kind=="carry"&&State.activeTask==t.id&&string.IsNullOrEmpty(State.carry))pos=sites[t.id=="D1_04"?"arrival":"store"].position;
                if(t.kind=="carry"&&State.activeTask!=t.id)pos=sites[t.id=="D1_04"?"arrival":"store"].position;
                if(t.id=="D1_04"&&State.Count(t.id)>=3)pos=sites["store"].position;
                if((t.id=="N1_02"||t.id=="N1_03")&&(Day.postsReached&(t.id=="N1_02"?1:2))!=0)pos=PostKeeper(t.id).transform.position;
                if(t.kind=="families")pos=sites["family"+Mathf.Clamp(State.Count(t.id),0,2)].position;
                if(t.id=="N2_01"||t.id=="N2_02")pos=NightObservationSpawn;
                if(t.kind=="inspectDoor")pos=UndergroundStore?.Inside==true?UndergroundStore.InspectionPoint:NightDoorStand;
                if(t.kind=="inspectRoute")pos=UndergroundStore?.Inside==true?UndergroundStore.BottomStand:NightRouteMarker;
                if(t.kind=="demolition")pos=CrateMarker(t);
                if(t.kind=="escortHung"||t.kind=="patrolHung"||t.kind=="escortScout")pos=escort!=null&&workTask==t?Destination(t):roles[t.kind=="escortScout"?"Tâm":"Hùng"].transform.position;
                if(t.kind.StartsWith("rescue")&&escort!=null&&workTask==t)pos=Destination(t);
                if((t.id=="N1_02"||t.id=="N1_03")&&(Day.postsReached&(t.id=="N1_02"?1:2))!=0)pos=PostKeeper(t.id).transform.position;
                // Dialogue pins follow the person, not a nearby empty site marker.
                if(t.id=="D2_03")pos=roles["Tâm"].transform.position;
                if(t.id=="D1_01")pos=roles["Anh Lực"].transform.position;
                if(t.id=="D1_06")pos=roles["Anh Thành"].transform.position;
                if(t.id=="D1_03")pos=roles["Chị Lành"].transform.position;
                mark.transform.position=pos;mark.prompt=Prompt(t);
            }
        }
        void TickWork(float dt)
        {
            workClock+=dt;var kb=Keyboard.current;
            if(workTask.kind=="antenna"){TickAntennaTraversal(dt,kb);return;}
            if(kb?.escapeKey.wasPressedThisFrame==true){EndWork(false);return;}
            if(Patching){TickPatch(dt);return;}
            if(kb==null)return;
            if(!kb.spaceKey.isPressed)heldReleased=true;
            if(kb.spaceKey.wasPressedThisFrame&&heldReleased)
            {
                heldReleased=false;
                if(workTask.kind=="repair")
                {
                    float needle=Mathf.PingPong(workClock*.65f,1);if(Mathf.Abs(needle-.5f)>.18f){Mission.Say("Chưa trúng nhịp. Thử lại, không mất vật tư.");return;}
                    State.workCount=State.Increment(workTask.id);if(State.workCount>=workTask.count)EndWork(true);
                }
            }
            if(workTask.kind!="repair"){holdClock=kb.spaceKey.isPressed?holdClock+dt:0;if(holdClock>=workTask.seconds)EndWork(true);}
        }
        /// <summary>Night 3, the village under attack: people run (m/s); Ông Sáu at a laboured jog.</summary>
        const float FleeRun=3.4f,ElderFlee=2.4f,NightRun=3.2f;
        /// <summary>Walking pace with a load in both hands.</summary>
        public const float CarryPace=.85f;
        /// <summary>TEMPORARY (25/10/2026): sprinting with a load, so the chapter can be speed-run while it is tuned. Set false to restore the walk.</summary>
        public const bool CarrySprintForTesting=true;
        /// <summary>Walking pace while leading Hùng before Day 3 (no sprinting); see LeadingWounded.</summary>
        public const float EscortPace=.55f;
        /// <summary>After a pose on Nam is released (its animator rebinds): the equipped weapon's stance, hands kept busy if they are.</summary>
        void RestoreNamAnimator(){if(combat==null)return;combat.RestoreAfterCinematic();if(handsBusy)combat.SetHandsOccupied(true);}
        void EndWork(bool success)
        {
            var task=workTask;if(IsPatch(task))EndPatch();if(signalAudio!=null){signalAudio.Stop();Destroy(signalAudio);signalAudio=null;}doingWork=false;if(task.kind=="antenna")ReleaseAntennaTraversal();combat.Equip(equippedBefore);combat.RestoreAfterCinematic();ForestMenu.SuppressKeysAfterCutscene();
            if(success){if(task.kind=="antenna")BeginScene(task,1);else if(task.kind=="clear")BeginScene(task,3);else Finish(task);}
        }
        public void DrawOverlay()
        {
            if(EvacuationCaptureActive){DrawEvacuationCapture();return;}
            if(NightPatrolFailureActive){DrawNightPatrolFailure();return;}
            DrawDayOverlay();
            UndergroundStore?.DrawGuide();
            var label=new GUIStyle(GUI.skin.label){fontSize=24,wordWrap=true};var title=new GUIStyle(label){fontSize=28,fontStyle=FontStyle.Bold};
            if(EvacuationModal){DrawEvacuationChoices(label,title);return;}
            if(!NightSuspicionSpeaking)DrawLiveNightRadio(label,title);
            DrawEffects();DrawBark(label);DrawAmmoMarks(label);DrawCover(label);
            DrawNightPatrolSuspicion();
            DrawNightPatrolSuspicionDialogue();
            if(sceneTask!=null&&spoken&&!HoldMealVoice&&!NightSilentOutro&&sceneTask.id!="N1_04")
            {
                GUI.Box(new Rect(150,700,1300,175),GUIContent.none);var line=lines[lineIndex];
                GUI.Label(new Rect(180,715,1240,38),line.speaker+(line.radio?" (bộ đàm)":""),title);GUI.Label(new Rect(180,755,1240,85),line.text,label);
                GUI.Label(new Rect(180,844,1240,32),$"{lineIndex+1}/{lines.Length} · E tiếp tục · Giữ Esc 1 giây bỏ cảnh",label);
            }
            if(Patching)DrawPatch(label,title);
            else if(doingWork&&workTask?.kind=="antenna")DrawAntennaWork(label,title);
            else if(doingWork)
            {
                GUI.Box(new Rect(350,270,900,350),GUIContent.none);GUI.Label(new Rect(385,300,830,80),workTask.title,title);
                GUI.Label(new Rect(385,390,830,100),workTask.kind=="antenna"?"A / D chỉnh hướng · giữ tín hiệu trong vùng ổn định 2 giây":workTask.kind=="repair"?$"Nhấn Space đúng nhịp · {State.workCount}/{workTask.count} · Sai được thử lại":"Giữ Space để thao tác · Esc hủy",label);
                if(workTask.kind=="repair"||workTask.kind=="antenna")
                {
                    float value=workTask.kind=="antenna"?signal:Mathf.PingPong(workClock*.65f,1);
                    GUI.Box(new Rect(390,510,810,34),"");var tint=GUI.color;GUI.color=Color.green;GUI.DrawTexture(new Rect(390+810*(workTask.kind=="antenna"?.665f:.32f),510,810*(workTask.kind=="antenna"?.11f:.36f),34),Texture2D.whiteTexture);GUI.color=Color.white;GUI.DrawTexture(new Rect(390+810*value,503,6,48),Texture2D.whiteTexture);GUI.color=tint;
                    if(workTask.kind=="antenna")GUI.Label(new Rect(390,558,810,40),$"Tín hiệu ổn định: {signalHold:0.0} / 2 giây",label);
                }
                else
                {
                    float progress=Mathf.Clamp01(holdClock/Mathf.Max(.1f,workTask.seconds));GUI.Box(new Rect(390,510,810,34),"");var tint=GUI.color;GUI.color=new Color(.7f,.8f,.4f);GUI.DrawTexture(new Rect(390,510,810*progress,34),Texture2D.whiteTexture);GUI.color=tint;GUI.Label(new Rect(390,558,810,40),$"Đã thao tác {progress*100:0}% · nhả Space sẽ dừng thao tác",label);
                }
            }
            if(Busy){GUI.Box(new Rect(430,32,1050,42),GUIContent.none);GUI.Label(new Rect(443,36,1030,35),Map02ChapterState.PhaseName(State.phase),label);}
            if(State.phase==3&&!State.Done("N2_01")&&binocular)
            {
                DrawNightObservationGuide(label);
            }
            if(State.finished){GUI.Box(new Rect(430,280,740,220),"MAP 2 HOÀN THÀNH");GUI.Label(new Rect(465,340,670,120),State.Objective,label);}
            float black=Mathf.Max(phaseFade>0?Mathf.Clamp01(phaseFade/1.2f):0,Mathf.Max(MealFade,ShiftFade));
            if(black>0){var tint=GUI.color;GUI.color=new Color(0,0,0,black);GUI.DrawTexture(new Rect(0,0,1600,900),Texture2D.whiteTexture);GUI.color=tint;}
        }
        void OnDestroy()
        {
            ReleaseEvacuationLightPresentation();
            ReleaseNightPatrolPresentation();ReleaseNightRiverCrossing();Cancel();ResetEffects();RestoreNaturalLighting();ReleaseDayArt();ReleaseDayTwoProps();if(combat!=null){combat.EquipAllowed=null;combat.Attacked-=OnPlayerShot;combat.ReloadDurationMultiplier=1;combat.SetHandsOccupied(false);}if(player!=null){player.SurfaceSpeedMultiplier=1;player.SprintAllowed=null;player.MovementConstraint=null;}
            RenderSettings.skybox=originalSky;RenderSettings.ambientMode=originalAmbientMode;if(chapterSky!=null)Destroy(chapterSky);RenderSettings.ambientLight=ambientBefore;RenderSettings.fog=fogBefore;RenderSettings.fogColor=fogColorBefore;RenderSettings.fogDensity=fogDensityBefore;
            if(sun!=null){sun.intensity=sunIntensity;sun.transform.rotation=sunRotation;sun.color=originalSunColor;}
        }
    }
}
