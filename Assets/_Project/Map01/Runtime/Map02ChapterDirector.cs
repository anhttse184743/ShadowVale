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
        public bool Busy=>sceneTask!=null||doingWork||DayActivityActive;
        public bool ConversationActive=>sceneTask!=null;
        public bool Binoculars=>binocular;
        bool binocularHeld;
        public void HoldBinoculars(bool held){binocularHeld=held;SetBinoculars(held&&!Mission.InputBlocked);}
        void SetBinoculars(bool held){if(ScoutEscortActive)held=false;if(binocular==held)return;binocular=held;rig?.SetBinoculars(held,held?2:1);}
        public bool LocksInput=>Busy||DayReturningInput||State.finished||Transitioning;
        public bool MovementLocked=>State.activeTask=="N3_08"&&!State.Done("N3_08")||State.Done("N3_WOUND")&&!State.Done("N3_CRAWL");
        public bool MaySave=>!Busy;
        public string SaveReason=>"Hãy hoàn tất thao tác hoặc hội thoại trước khi lưu.";
        public IReadOnlyDictionary<string,Transform> Sites=>sites;
        public Map02ChapterTask CurrentScene=>sceneTask;
        public List<Map02ChapterEnemy> Enemies {get;}=new List<Map02ChapterEnemy>();
        public Map02ChapterLayout Layout {get;private set;}
        float holdClock,detectionClock;Vector3 workOrigin;AudioSource signalAudio;
        Map01CinematicPose namPose;AnimationClip namIdle;readonly List<GameObject> hiddenWeapons=new List<GameObject>();
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
            EnsureDayState();LoadDayPerformances();BuildDayProps();ready=true;ApplyState();SyncDayProps();StartCoroutine(PreloadDayOneVoices());StartCoroutine(LoadChapterSoundscape());
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
            if(t.kind=="carry")
            {
                if(string.IsNullOrEmpty(State.carry)){State.carry=t.id;combat.Equip(WeaponKind.Unarmed);Mission.Say("Thùng đã được nhấc. Mang đến điểm đặt được đánh dấu.");}
                else {State.carry=null;State.workCount=State.Increment(t.id);Mission.Say("Đã đặt thùng "+State.workCount+"/"+t.count);if(State.workCount>=t.count){StoreRounds(t);Finish(t);} }
                UpdateMarkers();return true;
            }
            if(t.kind=="families") {if(!State.introduced.Contains(t.id)){BeginScene(t);scenePurpose=2;}else CompleteFamily(t);return true;}
            if(t.kind=="wire")
            {
                if(State.TakeWire()){UpdateMarkers();Mission.StoreCheckpoint();Mission.Say("Đã lấy cuộn dây. Mang về trạm, nhấn E giao cho Hùng.");return true;}
                if(State.DeliverWire()){CommitFinishedTask(t);return true;}
                return false;
            }
            if(t.kind=="medicine"){if(!State.Done("D1_02"))return false;BeginScene(t);return true;}
            if(t.kind=="gear")
            {
                if(State.gearStored){Mission.Inventory[Mission.RifleAmmo]=Inventory(Mission.RifleAmmo)+State.storedRounds;State.storedRounds=0;State.gearStored=false;}
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
            if(IsPatch(task))BeginPatch();
        }
        void Update()
        {
            PauseMealCrickets(!ready||Mission.Paused||ForestMenu.Visible||Mission.World.story.failed);
            TickChapterSoundscape();

            if(ready)SetBinoculars((binocularHeld||Keyboard.current?.fKey.isPressed==true)&&!Mission.InputBlocked&&!ForestMenu.Visible);
            if(!ready||Mission.Paused||ForestMenu.Visible||Mission.World.story.failed)return;
            float dt=Mathf.Min(Time.deltaTime,.1f);
            TickEffects();
            TickScoutEscort();
            if(phaseFade>0){phaseFade=Mathf.Max(0,phaseFade-dt);TickLight(dt);return;}
            if(State.finished)return;
            TickTank(dt);TickBoats(dt);TickDressing(dt);
            bool leading=LeadingWounded; // Day 3 / Night 3 escorts and Tâm's keep Nam armed and at pace.
            bool carrying=!string.IsNullOrEmpty(State.carry)||leading||DayActivityActive;if(carrying!=handsBusy||carrying&&!combat.HandsOccupied){handsBusy=carrying;combat.SetHandsOccupied(carrying);}   // a bag refresh (each save) frees the hands
            player.SurfaceSpeedMultiplier=leading?EscortPace:!string.IsNullOrEmpty(State.carry)?CarryPace:State.wounded?.7f:1;
            player.SprintAllowed=()=>!leading&&string.IsNullOrEmpty(State.carry)&&Mission.CharacterCanSprint;
            if(sceneTask!=null){TickLight(dt);TickScene(dt);return;}
            if(TickDay(dt))return;
            TickBark(dt);
            if(doingWork){TickWork(dt);return;}
            if(Mission.Panel!=0)return;
            TickLight(dt);TickPatrol(dt);TickCrawlCall(dt);TickAmmo();
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
                    {if(t.id=="D2_04")EndScoutEscort();escort.StopAt(dest+(t.id=="D1_02"?Vector3.forward*.6f:Vector3.right*1.4f));escort=null;secondaryEscort?.StopAt(dest+Vector3.left*1.4f);secondaryEscort=null;if(t.kind=="rescueFamily")Finish(t);else if(t.kind=="rescueChild")BeginScene(t,2);else if(t.id=="N1_02"||t.id=="N1_03")StartPost(t);else BeginScene(t);break;}
                }
                if(t.kind=="binocular")
                {
                    var cam=rig.GetComponent<Camera>();var delta=sites["scout"].position+Vector3.up-cam.transform.position;
                    bool view=binocular&&!player.IsMoving&&Vector3.Distance(Mission.Player.position,sites["scout"].position)>8&&Vector3.Angle(cam.transform.forward,delta)<Mathf.Min(32,cam.fieldOfView*.5f+8)&&ObservationVisible(cam,sites["scout"].position+Vector3.up);
                    observed=view?observed+dt:0;if(observed>=t.seconds){Finish(t);break;}
                }
                if(t.kind=="crawl"&&Keyboard.current?.cKey.isPressed==true){var path=new NavMeshPath();if(NavMesh.CalculatePath(Mission.Player.position,mark.transform.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete){var next=path.corners.FirstOrDefault(p=>Vector3.Distance(p,Mission.Player.position)>.15f);if(Vector3.Distance(Mission.Player.position,mark.transform.position)>1.2f){var delta=Vector3.ProjectOnPlane(next-Mission.Player.position,Vector3.up).normalized*.8f*dt;Mission.Player.GetComponent<CharacterController>().Move(delta);State.taskProgress+=dt;}}if(Vector3.Distance(Mission.Player.position,mark.transform.position)<1.5f){health.CinematicInvulnerable=false;BeginScene(t);break;}}
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
        Vector3 Destination(Map02ChapterTask t)=>t.kind.StartsWith("rescue")?sites["southStore"].position:DayTaskPoint(t);
        public void UpdateMarkers()
        {
            foreach(var t in Map02ChapterDefinition.Load().tasks)
            {
                var mark=markers[t.id];Vector3 pos=DayTaskPoint(t);
                if(t.kind=="wire"&&State.wireStage==1)pos=sites["radio"].position;
                if(t.kind=="carry"&&State.activeTask==t.id&&string.IsNullOrEmpty(State.carry))pos=sites[t.id=="D1_04"?"arrival":"store"].position;
                if(t.kind=="carry"&&State.activeTask!=t.id)pos=sites[t.id=="D1_04"?"arrival":"store"].position;
                if(t.id=="D1_04"&&State.Count(t.id)>=3)pos=sites["store"].position;
                if((t.id=="N1_02"||t.id=="N1_03")&&(Day.postsReached&(t.id=="N1_02"?1:2))!=0)pos=PostKeeper(t.id).transform.position;
                if(t.kind=="families")pos=sites["family"+Mathf.Clamp(State.Count(t.id),0,2)].position;
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
            if(kb?.escapeKey.wasPressedThisFrame==true){EndWork(false);return;}
            if(Patching){TickPatch(dt);return;}
            if(kb==null)return;
            if(!kb.spaceKey.isPressed)heldReleased=true;
            if(workTask.kind=="antenna")
            {
                signal=Mathf.Clamp01(signal+(kb.dKey.isPressed?dt*.22f:0)-(kb.aKey.isPressed?dt*.22f:0));
                if(signalAudio!=null)signalAudio.volume=Mathf.Abs(signal-.72f)*.35f;
                signalHold=Mathf.Abs(signal-.72f)<.055f?signalHold+dt:0;
                if(signalHold>=2)EndWork(true);return;
            }
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
        /// <summary>Walking pace with a load in both hands (no sprinting).</summary>
        public const float CarryPace=.85f;
        /// <summary>Walking pace while leading Hùng before Day 3 (no sprinting); see LeadingWounded.</summary>
        public const float EscortPace=.55f;
        /// <summary>After a pose on Nam is released (its animator rebinds): the equipped weapon's stance, hands kept busy if they are.</summary>
        void RestoreNamAnimator(){if(combat==null)return;combat.RestoreAfterCinematic();if(handsBusy)combat.SetHandsOccupied(true);}
        void EndWork(bool success)
        {
            var task=workTask;if(IsPatch(task))EndPatch();if(signalAudio!=null){signalAudio.Stop();Destroy(signalAudio);signalAudio=null;}doingWork=false;combat.Equip(equippedBefore);combat.RestoreAfterCinematic();ForestMenu.SuppressKeysAfterCutscene();
            if(success){if(task.kind=="antenna")BeginScene(task,1);else if(task.kind=="clear")BeginScene(task,3);else Finish(task);}
        }
        void BeginScene(Map02ChapterTask task,int from=0,int take=0)
        {
            if(!State.Available(task))return;
            lines=Map02ChapterDefinition.Load().Lines(task.scene??"").Skip(from).Take(take>0?take:int.MaxValue).ToArray();scenePurpose=0;lines=DayLines(task,lines);
            if(lines.Length==0){Finish(task);return;}
            DropBarks();if(task.id=="N3_07")SpawnEscort(); // They come up behind the tank.
            ReleaseDayFreePose();sceneTask=task;PrepareSceneMotion(task);PrepareDayScene(task);lineIndex=0;lineClock=sceneClock=skipHeld=0;lockedPosition=Mission.Player.position;lockedRotation=Mission.Player.rotation;if(State.phase>=2)StageConversation(task);
            player.RestoreMotion(false);DialogueVoice.StopAll();
            var actor=Mission.Player.GetComponentInChildren<Animator>();namIdle=actor.runtimeAnimatorController.animationClips.FirstOrDefault(c=>c.name=="Idle_Tuned");namPose=new Map01CinematicPose(actor,null,false);
            foreach(var weapon in Mission.Player.GetComponentsInChildren<Weapon>(true))if(weapon.gameObject.activeSelf){hiddenWeapons.Add(weapon.gameObject);weapon.gameObject.SetActive(false);}
            var cam=rig.GetComponent<Camera>();cameraBefore=cam.transform.position;cameraRotation=cam.transform.rotation;cameraFov=cam.fieldOfView;
            protectedBefore=health.CinematicInvulnerable;health.CinematicInvulnerable=true;
            PlanEntrances(task);StartLine();
        }
        void Speak(){voice.Stop();lineClock=0;if(HoldMealVoice)return;if(Map02DialogueCatalog.HasApprovedRecording(lines[lineIndex]))voice.Play(lines[lineIndex].RecordingId);}
        void TickScene(float dt)
        {
            sceneClock+=dt;lineClock+=dt;TickMealVoice();TickMealCrickets();
            if(sceneTask.id=="N3_09"&&tank!=null)SampleSacrifice();
            SceneSampled?.Invoke(sceneTask.id,sceneClock);
            if(sceneTask.id=="N3_WOUND"&&sceneClock>2)health.CinematicInvulnerable=true;var kb=Keyboard.current;
            skipHeld=kb?.escapeKey.isPressed==true?skipHeld+Time.unscaledDeltaTime:0;
            if(sceneTask.id=="N1_01"&&lineIndex==lines.Length-1&&kb?.eKey.wasPressedThisFrame==true&&lineClock>.6f){EndScene();return;}
            if(skipHeld>=1){EndScene();return;}
            if(!spoken){TickGate();if(!spoken)return;}
            float duration=DayLineDuration(LineSeconds(lines[lineIndex]))+PauseAfter(lineIndex);
            bool next=(lineClock>=duration&&!voice.HasSpeech)||(kb?.eKey.wasPressedThisFrame==true&&lineClock>.6f);
            if(HoldMealVoice)next=false;
            if(next)
            {
                if(lineIndex+1<lines.Length){lineIndex++;StartLine();}
                else if((sceneTask.kind!="cinematic"&&sceneTask.id!="N1_04"&&sceneTask.id!="D1_02")||sceneClock>=SceneDuration)EndScene();
            }
        }
        void LateUpdate()
        {
            DayLateUpdate();UpdateWorkWeapon();if(!ready)return;PanToHung();HoldCargo();if(sceneTask==null){UpdateHungRadio();return;}
            HoldConversation();MoveDaySceneRoots();Mission.Player.SetPositionAndRotation(lockedPosition,lockedRotation);var overrideClip=NamPoseClip();namPose?.Sample(overrideClip!=null?overrideClip:namIdle,sceneClock,overrideClip==null);
            SampleDayScene();UpdateHungRadio();
            string speaker=lines[lineIndex].speaker;Vector3 subject=Mission.Player.position;
            if(!lines[lineIndex].radio&&speaker!="Nam"&&roles.TryGetValue(speaker,out var actor))subject=actor.transform.position;
            var focus=Vector3.Lerp(subject,Mission.Player.position,.3f)+Vector3.up*1.25f;
            if(Vector3.Distance(subject,Mission.Player.position)>10)focus=Mission.Player.position+Vector3.up*1.25f;
            var eye=focus-Mission.Player.forward*3.4f+Mission.Player.right*2.5f+Vector3.up*1.4f;
            if(FinaleCamera(out var staged,out var stagedFocus)){eye=staged;focus=stagedFocus;}
            if(sceneTask.id=="DAWN"){focus=Vector3.Lerp(Mission.Player.position,memorialArt!=null?memorialArt.transform.position:sites["ditch"].position,.5f)+Vector3.up*.8f;eye=focus-Mission.Player.forward*4+Mission.Player.right*3+Vector3.up*1.5f;}
            // Medium/wide framing; obstruction tested below.
            var ray=eye-focus;foreach(var hit in Physics.SphereCastAll(focus,.12f,ray.normalized,ray.magnitude,~0,QueryTriggerInteraction.Ignore))if(hit.collider.GetComponentInParent<Health>()==null&&hit.distance>.65f)eye=focus+ray.normalized*Mathf.Min(ray.magnitude,Mathf.Max(.7f,hit.distance-.12f));
            if(sceneTask.id=="D1_02"){focus=roles["Hùng"].transform.position+Vector3.up*.85f;eye=focus+new Vector3(-3f,.35f,2f);}
            float mealFov=45;if(sceneTask.id=="N1_01")MealShot(out eye,out focus,out mealFov);
            float blend=sceneTask.id=="N1_01"?1:Mathf.SmoothStep(0,1,sceneClock/.8f);   // a cut into the house, not a glide through its wall
            rig.SetCinematicView(Vector3.Lerp(cameraBefore,eye,blend),Quaternion.Slerp(cameraRotation,Quaternion.LookRotation(focus-eye),blend),Mathf.Lerp(cameraFov,sceneTask.id=="N1_01"?mealFov:sceneTask.id=="D1_02"?45:58,blend),1);
        }
        void EndScene()
        {
            foreach(var e in entrances)if(Vector3.Distance(e.actor.transform.position,e.to)>=1.5f)e.actor.StopAt(e.to); // Skipped walks end where the scene meant them to.
            ClearEntrances();
            ReleaseConversation();ReleaseDayScene();ReleaseSceneAction();var task=sceneTask;if(task.id=="N3_09")roles["Hùng"].StopAt(RetreatPoint);int purpose=scenePurpose;scenePurpose=0;namPose?.Dispose();namPose=null;foreach(var w in hiddenWeapons)if(w!=null)w.SetActive(true);hiddenWeapons.Clear();voice.Stop();sceneTask=null;lines=null;rig.ClearCinematicView();health.CinematicInvulnerable=protectedBefore;
            ForestMenu.SuppressKeysAfterCutscene();Mission.RequireInteractionRelease();combat.RestoreAfterCinematic();
            dayDockEnding=false;
            if(purpose==5){Day.arrivalSeen=true;dayArrivalStarting=false;Mission.StoreCheckpoint();return;}
            if(purpose!=0){if(!State.introduced.Contains(task.id))State.introduced.Add(task.id);if(purpose==4)OpenDay(DayActivity.Dock,task);else if(purpose==1)StartWork(task);else if(purpose==2)CompleteFamily(task);else StartRescueEscort(task);return;}
            Finish(task);
        }
        void Finish(Map02ChapterTask task)
        {
            if(task.id=="D1_03")Day.GiveMedicine(0);if(task.id=="N1_03")Day.GiveMedicine(1);
            if(!State.Complete(task.id))return;
            CommitFinishedTask(task);
        }
        void CommitFinishedTask(Map02ChapterTask task)
        {
            TaskCompleted?.Invoke(task.id);
            if(task.id=="D2_03")QueueScoutWarning();
            if(task.id=="N1_03")roles["Hùng"].GoTo(sites["nightOneRest"].position,1.25f);
            if(task.id=="D1_01")roles["Anh Thành"].GoTo(sites["command"].position,1.6f);
            if(task.id=="D2_04")roles["Tâm"].StopAt(sites["command"].position+Vector3.right*1.5f);
            if(task.id=="N3_00")roles["Hùng"].GoTo(sites["ditch"].position,1.8f);
            StageNightThree(task);
            ApplyState();Mission.RefreshChapter();
            if(task.id=="N2_02")Mission.EnsurePatrolCheckpoint();
            Mission.StoreCheckpoint();
            var def=Map02ChapterDefinition.Load();
            if(task.id=="N3_09")BeginScene(def.Find("DAWN"));
            if(task.id=="N3_05C")BeginScene(def.Find("N3_WOUND")); // These happen to Nam; he never walks to a pin to trigger them.
            if(task.id=="N3_08")BeginScene(def.Find("N3_09"));
            if(task.id=="N3_04"){Bark("m2v3_wound_01");Bark("m2v3_wound_02");}
            if(task.id=="N3_WOUND")crawlCall=4; // The scene just ended on his first call.
            if(State.phase==4&&State.Available(def.Find("D3_05"))&&task.id!="D3_05")roles["Hùng"].GoTo(HungAtSupper,1.35f); // Home for supper.
            if(State.finished){Mission.World.story.stage=Map02Stage.Complete;Mission.StoreCheckpoint();}
            if(State.finished)Mission.Say("MAP 2 HOÀN THÀNH · Ta giữ được làng. Hùng đã hy sinh cứu chuyến thuyền cuối.");
        }
        public void Restore(Map02ChapterState state)
        {
            Cancel();State=state??new Map02ChapterState();State.Validate();EnsureDayState();workTask=Map02ChapterDefinition.Load().Find(State.activeTask);
            Mission.World.story.stage=State.finished?Map02Stage.Complete:Map02Stage.Contact;
            foreach(var e in Enemies)if(e!=null)Destroy(e.gameObject);Enemies.Clear();
            foreach(var saved in State.enemies)SpawnEnemy(saved.id,saved.group,saved.position,saved.health,saved.group!="scout_patrol",saved.looted);
            ammoHinted=false;
            // Saves from before evacuation-driven lanes stored seconds held.
            var held=Map02ChapterDefinition.Load().Find(State.activeTask);if(held!=null&&held.kind=="defend"&&State.taskProgress>1)State.taskProgress=Mathf.Clamp01(State.taskProgress/Mathf.Max(1,held.seconds));
            if(State.completed.Count==0)foreach(var pair in initialRolePositions)roles[pair.Key].StopAt(pair.Value);
            placedActors=false;phaseApplied=-1;ApplyState();
            foreach(var saved in State.actors)if(roles.TryGetValue(saved.role,out var actor))actor.StopAt(saved.position);
            PlaceChildWithMother();
            if(State.phase==0&&State.Done("D1_01"))roles["Anh Thành"].GoTo(sites["command"].position,1.6f);
            if(State.phase==4&&State.Done("D3_05"))roles["Anh Thành"].StopAt(SupperYard);
            if(State.phase>=5&&State.Done("N3_02"))foreach(var name in new[]{"Ông Sáu","Chị Lành"})if(Vector3.Distance(roles[name].transform.position,sites["southStore"].position)>8)roles[name].StopAt(sites["southStore"].position+(name=="Ông Sáu"?Vector3.right:Vector3.left)*1.4f);
            if(State.phase>=5&&State.Done("N3_03")&&Vector3.Distance(roles["Bé Tí"].transform.position,sites["southStore"].position)>8)roles["Bé Tí"].StopAt(sites["southStore"].position+Vector3.forward*1.4f);
            hungCanal=null;
            if(State.activeTask=="N3_08"&&State.taskProgress>1){State.coverClock=State.taskProgress;State.taskProgress=Mathf.Clamp01(State.coverClock/CoverSlowest);} // Saves that counted seconds.
            if(State.phase==5&&State.Done("N3_00")&&!State.hungDead)
            {
                if(State.activeTask=="N3_08"){TickTank(0,true);roles["Hùng"].StopAt(Along(HungCanal,State.taskProgress*Length(HungCanal)));pansDone=Mathf.FloorToInt(State.taskProgress*3);}
                else if(State.Done("N3_08"))roles["Hùng"].StopAt(ChargePoint);else roles["Hùng"].GoTo(sites["ditch"].position,1.8f);
            }
            if(State.Available(workTask))
            {
                if(workTask.kind=="escortScout"||workTask.kind=="escortHung"||workTask.kind=="patrolHung")
                {if(workTask.id=="D2_04")StartScoutEscort(true);else{escort=roles[workTask.kind=="escortScout"?"Tâm":"Hùng"];escort.Follow(Mission.Player,1.35f);}}
                else if(workTask.kind=="rescueFamily"||workTask.kind=="rescueChild")
                {if(State.introduced.Contains(workTask.id))StartRescueEscort(workTask);}
            }
            handsBusy=!string.IsNullOrEmpty(State.carry);combat.SetHandsOccupied(handsBusy);
            player.SurfaceSpeedMultiplier=State.wounded?.7f:1;
            player.SprintAllowed=()=>Mission.CharacterCanSprint;
            detectionClock=0;scannedAt=-99;patrolLoadGrace=2;
            if(State.phase==1&&State.Done("N1_03")&&!State.Done("N1_04"))roles["Hùng"].GoTo(sites["nightOneRest"].position,1.25f);
            ArrangeDayFamily();UpdateMarkers();SyncDayProps();TickLight(10f);
            TickTank(0,true);TickBoats(0,true);
            var def=Map02ChapterDefinition.Load();
            if(State.phase==4&&State.Available(def.Find("D3_05")))roles["Hùng"].StopAt(HungAtSupper);
            if(State.Done("N3_09")&&!State.finished)BeginScene(def.Find("DAWN"));
            else if(State.Done("N3_05C")&&!State.Done("N3_WOUND"))BeginScene(def.Find("N3_WOUND"));
            else if(State.Done("N3_08")&&!State.Done("N3_09"))BeginScene(def.Find("N3_09"));
        }
        public Map02ChapterState Capture()
        {
            State.actors=roles.Select(pair=>new Map02ChapterState.ActorSnapshot{role=pair.Key,position=pair.Value.transform.position}).ToList();
            State.enemies=Enemies.Where(e=>e!=null).Select(e=>new Map02ChapterState.UnitSnapshot{id=e.id,group=e.group,position=e.transform.position,health=e.HitPoints.Current,looted=e.Looted}).ToList();
            return JsonUtility.FromJson<Map02ChapterState>(JsonUtility.ToJson(State));
        }
        public void Cancel()
        {
            EndScoutEscort();
            ReleaseWorkWeapon();ReleaseDayScene();ReleaseDayFreePose();CloseDay(false);dayReturnInput=false;dayArrivalStarting=dayDockEnding=false;
            if(health!=null&&sceneTask!=null)health.CinematicInvulnerable=protectedBefore;
            ClearEntrances();DropBarks();ReleaseConversation();ReleaseSceneAction();phaseFade=0;binocularHeld=false;binocular=false;observed=0;detectionClock=0;if(rig!=null)rig.SetBinoculars(false,1);if(combat!=null)combat.SetHandsOccupied(false);handsBusy=false;if(namPose!=null){namPose.Dispose();namPose=null;combat?.RestoreAfterCinematic();}foreach(var w in hiddenWeapons)if(w!=null)w.SetActive(true);hiddenWeapons.Clear();if(signalAudio!=null){signalAudio.Stop();Destroy(signalAudio);signalAudio=null;}voice?.Stop();if(Patching)EndPatch();sceneTask=null;doingWork=false;lines=null;escort?.StopAt(escort.transform.position);escort=null;secondaryEscort?.StopAt(secondaryEscort.transform.position);secondaryEscort=null;rig?.ClearCinematicView();
        }
        void ApplyState()
        {
            Mission.World.story.stage=State.finished?Map02Stage.Complete:State.phase==0?(State.Done("D1_01")?Map02Stage.VillageHelp:Map02Stage.Contact):State.phase==2?Map02Stage.MissingScout:State.phase<5?Map02Stage.QuietPreparation:State.Done("N3_07")?Map02Stage.Breakout:Map02Stage.HoldLanes;
            bool phaseChanged=placedActors&&placedPhase!=State.phase;bool fresh=State.completed.Count==0;
            if(!placedActors||placedPhase!=State.phase)
            {
                if(!fresh)roles["Anh Thành"].StopAt(State.phase==6?sites["ditch"].position+Vector3.right*2.5f:sites["command"].position);
                roles["Anh Lực"].StopAt(State.phase==6?sites["ditch"].position+Vector3.left*2.5f:(State.phase==4?sites["ditch"].position:State.phase==5?LucNightThree():sites["gate"].position));
                var safe=sites["southStore"].position;
                roles["Chị Lành"].StopAt(State.Done("N3_02")?safe+Vector3.left*1.4f:State.phase<=1?DayFamily+Vector3.left*.8f:sites["family"].position);
                roles["Ông Sáu"].StopAt(State.Done("N3_02")?safe+Vector3.right*1.4f:State.phase<=1?DayFamily+Vector3.forward*3.5f+Vector3.left*1.4f:sites["family"].position+Vector3.right*1.4f);   // Day 1 / Night 1: the family's seat; later days: the yard
                roles["Bé Tí"].StopAt(State.Done("N3_03")?safe+Vector3.forward*1.4f:State.phase<=1?DayFamily+Vector3.left*1.3f+Vector3.forward*3.1f:sites[State.phase==5?"child":"family"].position+Vector3.back*.8f);   // Night 3: lost at the buffalo pen
                roles["Chú Bảy"].StopAt(sites["dock"].position);
                roles["Tâm"].StopAt(sites[State.phase==5&&State.Done("N3_CRAWL")?"tamCover":State.Done("D2_04")?"bamboo":"scout"].position);
                if(!fresh&&!State.hungDead&&escort!=roles["Hùng"])roles["Hùng"].StopAt(sites[State.phase==5?(State.Done("N3_00")?"ditch":"rest"):State.phase==4?"family":State.phase>=2?"radio":State.phase==1?(State.Done("N1_03")?"gate":State.Done("N1_02")?"bamboo":"family"):State.Done("D1_02")?"family":"arrival"].position+Vector3.right*1.4f);
                placedActors=true;placedPhase=State.phase;
            }
            roles["Hùng"].SetDead(State.hungDead);
            roles["Tâm"].SetVisible(State.phase>=2);
            roles["Tâm"].SetInjured(State.phase==2&&!State.Done("D2_04")&&State.activeTask!="D2_04");
            PlaceChildWithMother();
            if(phaseChanged){phaseFade=1.2f;player.RestoreMotion(false);TickLight(10f);}
            health.CinematicInvulnerable=State.Done("N3_WOUND")&&!State.Done("N3_CRAWL")||State.activeTask=="N3_08";
            combat.ReloadDurationMultiplier=State.wounded?1.35f:1;
            if(State.finished)health.CinematicInvulnerable=true;
            if(State.phase==5&&State.activeTask=="N3_08")health.CinematicInvulnerable=true;
            foreach(var unit in Enemies)if(unit!=null){if(State.Done("N3_09"))unit.gameObject.SetActive(false);else if(unit.group=="scout_patrol")unit.gameObject.SetActive(State.phase==2);}
            roles["Hùng"].SetLocomotion(State.phase<=1?DayClip("InjuredIdle"):null,State.phase<=1?HungMapOneWalk:null);
            roles["Hùng"].PrimeLocomotion();roles["Anh Thành"].SetQuietRifle(State.phase<=1);roles["Anh Thành"].SetLocomotion(State.phase<=1?dayNamAnimator.runtimeAnimatorController.animationClips.FirstOrDefault(c=>c.name=="Idle_Tuned"):null,State.phase<=1?ordinaryWalk:null);ArrangeDayFamily();
            SyncDayProps();RefreshMemorial();UpdateMarkers();
        }
        void PlaceChildWithMother()
        {
            // Keep the authored lost-child/rescue segment in Night 3; normal village life stays by Lành.
            if(State.phase<=1||State.phase==5&&!State.Done("N3_03"))return;
            var mother=roles["Chị Lành"].transform;
            roles["Bé Tí"].StopAt(mother.position+Vector3.right*1.05f+Vector3.back*.55f);
            roles["Bé Tí"].SetSitting(false);
        }
        void TickLight(float dt)
        {
            bool night=State.phase==1||State.phase==3||State.phase==5;bool afternoon=State.phase==0&&State.Available(Map02ChapterDefinition.Load().Find("D1_06"));
            // Day 1 / Night 1 the family lamp is the small one on the supper mat: it lights the tray and little else.
            foreach(var lamp in lamps){bool supperLamp=lamp.name.EndsWith("family")&&State.phase<=1;lamp.range=supperLamp?3.5f:State.phase<=1?6:10;if(supperLamp)lamp.transform.position=dayLampFlame!=null?dayLampFlame.position+Vector3.up*.1f:PorchMeal+Vector3.up*.3f;lamp.intensity=Mathf.Lerp(lamp.intensity,night?(State.phase<=1?(Layout!=null?Layout.dayOneOilLampIntensity:.65f):2.5f):0,Mathf.Clamp01(dt));}
            sweepLight.enabled=State.phase==3&&!State.Done("N2_05");
            if(phaseApplied!=State.phase){phaseApplied=State.phase;}
            if(TickNaturalLighting(dt))return;
            bool golden=State.phase==4; // Day 3: the warmest afternoon of the chapter, against Night 3.
            if(sun!=null){sun.intensity=Mathf.Lerp(sun.intensity,night?(Layout!=null?Layout.nightSunIntensity:.08f):State.phase==6?.45f:golden?sunIntensity*.9f:afternoon?sunIntensity*.65f:sunIntensity,dt*.6f);sun.color=Color.Lerp(sun.color,night?new Color(.5f,.63f,1):golden?new Color(1,.74f,.46f):afternoon?new Color(1,.67f,.36f):new Color(1,.9f,.75f),dt*.6f);sun.transform.rotation=Quaternion.Slerp(sun.transform.rotation,night?Quaternion.Euler(25,190,0):golden?Quaternion.Euler(14,250,0):afternoon?Quaternion.Euler(18,220,0):sunRotation,dt*.6f);}
            if(chapterSky!=null&&chapterSky.HasProperty("_Exposure"))chapterSky.SetFloat("_Exposure",Mathf.Lerp(chapterSky.GetFloat("_Exposure"),night?(Layout!=null?Layout.nightSkyExposure:.08f):1f,dt));
            RenderSettings.ambientLight=Color.Lerp(RenderSettings.ambientLight,night?new Color(.12f,.15f,.23f):golden?Color.Lerp(ambientBefore,new Color(.62f,.48f,.34f),.45f):ambientBefore,dt*.6f);
        }
        void TickPatrol(float dt)
        {
            if(State.phase!=3||State.Done("N2_05"))return;
            float period=Mathf.Max(6,Layout!=null?Layout.patrolSweepSeconds:15);
            State.patrolScanSeconds=Mathf.Repeat(State.patrolScanSeconds+dt,period);float phase=State.patrolScanSeconds;
            // A sweep can notice Nam only with line of sight and without cover; one warning per pass.
            var origin=sites["scout"].position+Vector3.up*1.7f;var delta=Mission.Player.position+Vector3.up-origin;
            sweepLight.transform.position=origin;
            var sweep=Quaternion.Euler(0,Mathf.Lerp(-50,50,phase/period),0)*(sites["store"].position-origin).normalized;
            sweepLight.transform.rotation=Quaternion.LookRotation(sweep);
            if(patrolLoadGrace>0){patrolLoadGrace=Mathf.Max(0,patrolLoadGrace-dt);return;}
            if(!State.Done("N2_02"))return;
            bool clear=Physics.Raycast(origin,delta.normalized,out var seen,delta.magnitude+.2f,~0,QueryTriggerInteraction.Ignore)&&(seen.transform==Mission.Player||seen.transform.IsChildOf(Mission.Player));
            bool exposed=phase>period*.13f&&phase<period*.87f&&Vector3.Angle(sweep,delta)<11&&delta.magnitude<55&&clear;
            detectionClock=exposed?detectionClock+dt:Mathf.Max(0,detectionClock-dt*2);
            if(detectionClock>=(player.IsSneaking?2f:1.4f)&&Time.time-scannedAt>period)
            {
                detectionClock=0;scannedAt=Time.time;State.patrolWarnings++;
                if(State.patrolWarnings>=2){Mission.RetryPatrol();Mission.Say("Bị tuần tra phát hiện. Đã trở lại đầu cầu; dùng bờ mương và vật che để tránh đèn.");}
                else Mission.Say("Hùng: Đèn đang quét. Nằm thấp xuống, chờ nó đi qua.");
            }
        }
        void EnsureGroup(string id,int count,bool hostile=true,string site=null,int minimum=0)
        {
            if(State.combatGroups.Contains(id)||Time.time<spawnRetry)return;
            var task=Map02ChapterDefinition.Load().Find(id);var center=sites[site??(task!=null?task.site:id=="scout_patrol"?"scout":"lane1")].position;var locations=new List<Vector3>();
            for(int i=0;i<36&&locations.Count<count;i++)
            {
                var p=center+Quaternion.Euler(0,i*137.5f,0)*Vector3.forward*(22+i%3*4);
                if(!NavMesh.SamplePosition(p,out var h,5,NavMesh.AllAreas)||Vector3.Distance(h.position,center)<18||Vector3.Distance(h.position,Mission.Player.position)<18)continue;
                var path=new NavMeshPath();if(!NavMesh.CalculatePath(h.position,center,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)continue;
                var view=rig.GetComponent<Camera>().WorldToViewportPoint(h.position+Vector3.up);if(view.z>0&&view.x>0&&view.x<1&&view.y>0&&view.y<1)continue;
                if(locations.Any(q=>Vector3.Distance(q,h.position)<4))continue;locations.Add(h.position);
            }
            if(locations.Count<(minimum>0?minimum:count)){spawnRetry=Time.time+2;Mission.Say("Địch đang cơ động ngoài tuyến. Giữ vị trí có vật che.");return;}
            for(int i=0;i<locations.Count;i++)SpawnEnemy(id+"_"+i,id,locations[i],100,hostile);
            State.combatGroups.Add(id);
        }
        void SpawnEnemy(string id,string group,Vector3 position,float hp,bool hostile=true,bool looted=false)
        {
            var go=new GameObject("Map2Enemy_"+id);go.transform.SetPositionAndRotation(position,Quaternion.identity);go.transform.SetParent(root.transform);
            var unit=go.AddComponent<Map02ChapterEnemy>();unit.Initialize(this,id,group,hp,hostile);unit.Looted=looted;unit.HoldPosition=group==Escort;Enemies.Add(unit);
        }
        void CreateTank()
        {
            tank=new GameObject("Tank art slot — scripted boss, no rifle damage");tank.transform.SetParent(root.transform);tank.transform.position=sites["tankBreach"].position;
            // Temporary prop volume; the art team replaces the Art child, never its gameplay owner.
            var art=Layout!=null&&Layout.tankArt!=null?Instantiate(Layout.tankArt):GameObject.CreatePrimitive(PrimitiveType.Cube);art.name="Art placeholder — tank hull";art.transform.SetParent(tank.transform,false);
            if(Layout!=null&&Layout.tankArt!=null){art.name="Art — M41 tank";art.transform.localPosition=Vector3.zero;BindTankParts(art.transform);} // Modelled tank: sits on the ground, parts move.
            else{art.transform.localPosition=Vector3.up;art.transform.localScale=new Vector3(2.8f,2,5);}
            var smoke=new GameObject("Destroyed smoke");smoke.transform.SetParent(tank.transform,false);smoke.transform.localPosition=Vector3.up*2;
            tankSmoke=smoke.AddComponent<ParticleSystem>();var main=tankSmoke.main;main.playOnAwake=false;main.startLifetime=3;main.startSpeed=1.5f;main.startSize=1.8f;main.startColor=new Color(.15f,.15f,.15f,.6f);tankSmoke.Stop();
            tankSmoke.GetComponent<ParticleSystemRenderer>().sharedMaterial=Map01Fx.Lit;
            main.maxParticles=80;main.simulationSpace=ParticleSystemSimulationSpace.World;
            var emission=tankSmoke.emission;emission.rateOverTime=12;
            var shape=tankSmoke.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=12;shape.radius=.35f;shape.rotation=new Vector3(-90,0,0);
            var fade=tankSmoke.colorOverLifetime;fade.enabled=true;var gradient=new Gradient();
            gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.15f),new GradientAlphaKey(0,1)});fade.color=gradient;
        }
        public void DrawOverlay()
        {
            DrawDayOverlay();
            var label=new GUIStyle(GUI.skin.label){fontSize=24,wordWrap=true};var title=new GUIStyle(label){fontSize=28,fontStyle=FontStyle.Bold};
            DrawEffects();DrawBark(label);DrawAmmoMarks(label);DrawCover(label);
            if(sceneTask!=null&&spoken&&!HoldMealVoice&&sceneTask.id!="N1_04")
            {
                GUI.Box(new Rect(150,700,1300,175),GUIContent.none);var line=lines[lineIndex];
                GUI.Label(new Rect(180,715,1240,38),line.speaker+(line.radio?" (bộ đàm)":""),title);GUI.Label(new Rect(180,755,1240,85),line.text,label);
                GUI.Label(new Rect(180,844,1240,32),$"{lineIndex+1}/{lines.Length} · E tiếp tục · Giữ Esc 1 giây bỏ cảnh",label);
            }
            if(Patching)DrawPatch(label,title);
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
                var cam=rig.GetComponent<Camera>();var point=cam.WorldToViewportPoint(sites["scout"].position+Vector3.up);
                if(point.z>0&&point.x>.05f&&point.x<.95f&&point.y>.08f&&point.y<.9f)GUI.Label(new Rect(point.x*1600-140,(1-point.y)*900-40,280,65),"◇ VÙNG ĐÈN TUẦN TRA",new GUIStyle(label){alignment=TextAnchor.MiddleCenter,fontSize=19});
                else GUI.Label(new Rect(530,120,720,50),"Quay ống nhòm về phía nam cầu để tìm ánh đèn tuần tra.",label);
            }
            if(State.phase==3&&State.Done("N2_02")&&!State.Done("N2_05")&&Vector3.Distance(Mission.Player.position,sites["scout"].position)<55)
                GUI.Label(new Rect(1060,165,500,75),$"Đèn đang quét · Chú ý {State.patrolWarnings}/2\nBị soi thấy: {Mathf.Clamp01(detectionClock/(player.IsSneaking?2f:1.4f))*100:0}% · Dùng vật che",new GUIStyle(label){fontSize=18});
            if(State.finished){GUI.Box(new Rect(430,280,740,220),"MAP 2 HOÀN THÀNH");GUI.Label(new Rect(465,340,670,120),State.Objective,label);}
            if(phaseFade>0){var tint=GUI.color;GUI.color=new Color(0,0,0,Mathf.Clamp01(phaseFade/1.2f));GUI.DrawTexture(new Rect(0,0,1600,900),Texture2D.whiteTexture);GUI.color=tint;}
        }
        void OnDestroy()
        {
            Cancel();ResetEffects();RestoreNaturalLighting();ReleaseDayArt();if(combat!=null){combat.EquipAllowed=null;combat.Attacked-=OnPlayerShot;combat.ReloadDurationMultiplier=1;combat.SetHandsOccupied(false);}if(player!=null){player.SurfaceSpeedMultiplier=1;player.SprintAllowed=null;}
            RenderSettings.skybox=originalSky;RenderSettings.ambientMode=originalAmbientMode;if(chapterSky!=null)Destroy(chapterSky);RenderSettings.ambientLight=ambientBefore;RenderSettings.fog=fogBefore;RenderSettings.fogColor=fogColorBefore;RenderSettings.fogDensity=fogDensityBefore;
            if(sun!=null){sun.intensity=sunIntensity;sun.transform.rotation=sunRotation;sun.color=originalSunColor;}
        }
    }
}
