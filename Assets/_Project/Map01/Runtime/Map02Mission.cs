using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Player;
using ShadowVale.Gameplay.Skills;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ShadowVale.Map01
{
    /// <summary>Map 2 session. Real world events own rescues/combat; props own cargo transactions.</summary>
    [DefaultExecutionOrder(105)]
    public sealed partial class Map02Mission : MonoBehaviour
    {
        [Serializable] public sealed class Item { public string id; public int count; }
        [Serializable] public sealed class Snapshot
        {
            public int version=1;
            public Map02WorldState world;
            public Map02ChapterState chapter;
            public Map01Inventory.Layout bagLayout;
            public MapExplorationState exploration;
            public bool hasCharacterState;
            public float stamina,craftRemaining;
            public string crafting;
            public GroundSnapshot[] ground;
            public Vector3 position;
            public float yaw, health, playSeconds, attackCooldown;
            public int magazine, equipped, stones;
            public int rifleMagazine=-1,pistolMagazine=-1,sniperMagazine=-1;
            public string rifleItemId;
            public Item[] inventory;
            public SkillProgress skills;
            public SkillMissionProgress skillMission;
            public string checkpoint, patrolCheckpoint, skillRunId;
        }
        public Map02ChapterDirector Chapter {get;private set;}
        public string ObjectiveText=>Chapter!=null?Chapter.State.Objective:World.Objective;
        bool ObjectiveAvailable(Map02ObjectiveObject t)=>Chapter!=null?Chapter.IsAvailable(t):World.Available(t.work);
        public void RefreshChapter(){SyncSkills();targets=FindObjectsByType<Map02ObjectiveObject>(FindObjectsSortMode.None);Notify();}
        public Map02WorldState World { get; private set; } = new Map02WorldState();
        public Transform Player => player != null ? player.transform : null;
        public bool Ready { get; private set; }
        public bool Paused { get; private set; }
        public bool Cinematic => !Ready || dialogue?.Active == true || (Chapter?.Busy==true||Chapter?.Transitioning==true) || World.story.IsCinematic;
        public bool InputBlocked => !Ready || Paused || Panel != 0 || dialogue?.Active == true || Chapter?.LocksInput==true || World.story.failed || World.story.IsCinematic;
        public int Panel { get; private set; } // 1 inventory, 2 journal, 3 map
        public readonly Dictionary<string,int> Inventory = new Dictionary<string,int>();
        public int Stones { get; private set; }
        public float PlaySeconds { get; private set; }
        string RifleItemId
        {
            get
            {
                var gun = combat != null ? combat.WeaponFor(WeaponKind.Rifle) : null;
                return gun != null ? gun.GetComponent<Map01RifleVisual>()?.ItemId ?? "rifle_standard" : "rifle_standard";
            }
        }
        public string RifleAmmo => RifleItemId == Map01M16.Id ? Map01M16.Ammo : "ammo_rifle";
        public event Action<Map02Work> WorkRequested;
        public event Action Changed;
        Map02ConversationSession dialogue;
        Map02DialogueCatalog.Entry[] lines;
        DialogueVoice voice;
        float lineStartedAt;
        bool voiced;
        PlayerController player;
        PlayerCombat combat;
        Health health;
        ThirdPersonCamera cameraRig;
        Map02Arrival arrival;
        string checkpoint;
        string patrolCheckpoint;
        string message;
        float messageUntil;
        Map02ObjectiveObject nearest;
        bool waitWorldERelease;float worldEAfter;
        public void RequireInteractionRelease(){waitWorldERelease=Keyboard.current?.eKey.isPressed==true;worldEAfter=Time.unscaledTime+.15f;}
        bool WorldEReady(Keyboard kb){if(waitWorldERelease){if(kb.eKey.isPressed)return false;waitWorldERelease=false;}return Time.unscaledTime>=worldEAfter;}

        Map02ObjectiveObject[] targets=Array.Empty<Map02ObjectiveObject>();
        Map02QuestLayout questLayout;
        GUIStyle body, heading, small;
        static Snapshot pending;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            SceneManager.sceneLoaded -= Loaded;
            SceneManager.sceneLoaded += Loaded;
        }
        static void Loaded(Scene scene, LoadSceneMode mode)
        {
            if(scene.name=="Map 2")Map02BridgeRails.Install(scene);
            if (scene.name == "Map 2" && FindFirstObjectByType<Map02Mission>() == null)
                new GameObject("Map 2 mission session").AddComponent<Map02Mission>();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureCurrentScene() => Loaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        IEnumerator Start()
        {
            arrival = FindFirstObjectByType<Map02Arrival>();
            var exploration=FindFirstObjectByType<Map02Exploration>();
            if(arrival!=null&&exploration!=null)
            {
                exploration.enabled=false;
                if(exploration.player!=null)exploration.player.gameObject.SetActive(false);
            }
            while (arrival != null && arrival.IsPlaying) yield return null;
            player=arrival != null && arrival.Passengers != null ? arrival.Passengers[0].GetComponent<PlayerController>() : exploration != null ? exploration.player : FindFirstObjectByType<PlayerController>();
            if (player == null) { Debug.LogError("Map 2 has no player to bind.",this); yield break; }
            combat=player.GetComponent<PlayerCombat>();health=player.GetComponent<Health>();
            cameraRig=FindObjectsByType<ThirdPersonCamera>(FindObjectsSortMode.None).FirstOrDefault(c=>c.GetComponent<Camera>() != null && c.GetComponent<Camera>().enabled);
            if (arrival != null) { foreach(var pair in arrival.Inventory) Inventory[pair.Key]=pair.Value; Stones=arrival.Stones; }
            // The exploration-only R reset conflicts with gameplay reload. Its scene instance
            // stays disabled after the mission takes ownership, never the shared prefab.
            if (exploration != null) exploration.enabled=false;
            if (combat != null)
            {
                combat.enabled=true;combat.DrawsOwnWeaponHud=false;
                combat.InputAllowed=()=>!InputBlocked;combat.TryConsumeRound=null;
                combat.ReserveRounds=()=>Inventory.TryGetValue(RifleAmmo,out int n)?n:0;
                combat.DrawRounds=requested=>{int n=Mathf.Min(requested,combat.ReserveRounds());Inventory[RifleAmmo]=combat.ReserveRounds()-n;return n;};
            }
            player.InputAllowed=()=>!InputBlocked&&Chapter?.MovementLocked!=true;
            if(cameraRig!=null){cameraRig.SetTarget(Player);cameraRig.InputAllowed=()=>!InputBlocked;}
            voice=DialogueVoice.For(gameObject);voice.CanSpeak=()=>!Paused&&!ForestMenu.Visible;
            if(FindFirstObjectByType<Map02QuestLayout>()==null)
            {
                var layout=Resources.Load<Map02QuestLayout>("Story/Map02QuestObjects");if(layout!=null)Instantiate(layout);
            }
            targets=FindObjectsByType<Map02ObjectiveObject>(FindObjectsSortMode.None);
            questLayout=FindFirstObjectByType<Map02QuestLayout>();
            dialogue=new Map02ConversationSession(World.story);
            Ready=true;
            InstallMapSafety();
            arrival?.ReleasePassengerAnimation();
            Chapter=gameObject.AddComponent<Map02ChapterDirector>();Chapter.Initialize(this,questLayout);RefreshChapter();
            InitializeCharacter();
            StartCoroutine(PrepareHudChart());
            if (pending != null) { var data=pending; pending=null; Restore(data); }
            if (string.IsNullOrEmpty(checkpoint)) StoreCheckpoint();
            ForestMenu.RefreshGameplayBinding();
        }
        void Update()
        {
            if(Ready)SyncSkills();
            TickCharacterEffects();
            if (!Ready || Paused || ForestMenu.Visible) return;
            TickMapSafety();
            Chapter?.UpdateMarkers();
            TickCharacter();
            RefreshRoute();
            if (!World.story.failed) PlaySeconds+=Time.deltaTime;
            if (health != null && health.IsDead && !World.story.failed) { World.story.Fail("Nam đã hy sinh. Trở về checkpoint để thử lại.");CancelConversation();Notify(); }
            // What [E] would use, and its outline, whatever the input device.
            if(!dialogue.Active&&!World.story.failed&&Chapter?.LocksInput!=true&&Panel==0)
            {
                nearest=Chapter?.HasNearbyPost==true?null:NearestObjective();
                InteractHighlight.Focus(nearest!=null?Chapter?.HighlightFor(nearest)??nearest.transform:NearestGroundItem()?.transform);
            }
            var kb=Keyboard.current;if(kb==null)return;bool worldEReady=WorldEReady(kb);
            if(dialogue.Active)
            {
                if(kb.escapeKey.wasPressedThisFrame){CancelConversation();return;}
                if(kb.eKey.wasPressedThisFrame&&Time.unscaledTime-lineStartedAt>.3f)
                    AdvanceConversation();
                return;
            }
            if(World.story.failed||Chapter?.LocksInput==true)return;
            if(kb.tabKey.wasPressedThisFrame){TogglePanel(1);return;}
            if(kb.jKey.wasPressedThisFrame){TogglePanel(2);return;}
            if(kb.mKey.wasPressedThisFrame){TogglePanel(3);return;}
            if(Panel!=0)return;
            if(Chapter?.HasNearbyPost==true){nearest=null;if(worldEReady)Chapter.TryInteractNearbyPost();return;}
            if(worldEReady&&kb.eKey.wasPressedThisFrame){if(nearest!=null)Interact(nearest);else TryCharacterInteraction();}
        }
        Map02ObjectiveObject NearestObjective()
        {
            Map02ObjectiveObject found=null;float best=float.PositiveInfinity;
            foreach(var target in targets)
                if(target!=null)
                if(ObjectiveAvailable(target)&&(Chapter==null||Chapter.ShowInteractPrompt(target))&&target.WithinReach(Player))
                {float d=(Player.position-target.transform.position).sqrMagnitude;if(target==TrackedObjective)d=-1;if(d<best){found=target;best=d;}}
            return found;
        }
        public bool Interact(Map02ObjectiveObject target)
        {
            if(Chapter!=null)return Chapter.Interact(target);
            if(InputBlocked||target==null||!target.WithinReach(Player)||!ObjectiveAvailable(target))return false;
            switch(target.work)
            {
                case Map02Work.Contact:return BeginConversation("m2_contact");
                case Map02Work.ScoutOrder:return BeginConversation("m2_scout_order");
                case Map02Work.ScoutReport:return BeginConversation("m2_prepare");
                case Map02Work.MedicinePickup:case Map02Work.MedicineDelivery:
                case Map02Work.CratePickup:case Map02Work.CrateDelivery:
                case Map02Work.SouthStockPickup:case Map02Work.SouthStockDelivery:
                    if(!World.Transfer(target.work))return false;
                    Notify();StoreCheckpoint();
                    if(target.work==Map02Work.MedicineDelivery)BeginConversation("m2_family_medicine");
                    return true;
                // Aid, rallying and boarding request physical controllers. They cannot commit
                // by merely pressing E on an empty placeholder.
                default:WorkRequested?.Invoke(target.work);return true;
            }
        }
        public bool BeginConversation(string id)
        {
            if(!Ready||Paused||Panel!=0||dialogue.Active||!dialogue.Begin(id))return false;
            player.RestoreMotion(false);DialogueVoice.StopAll();lines=Map02DialogueCatalog.Conversation(id);SpeakCurrent();return true;
        }
        void SpeakCurrent()
        {
            voice.Stop();lineStartedAt=Time.unscaledTime;
            voiced=Map02DialogueCatalog.HasApprovedRecording(lines[dialogue.Index]);
            if(voiced)voice.Play(lines[dialogue.Index].RecordingId);
        }
        public bool AdvanceConversation()
        {
            if(!Ready||Paused||!dialogue.Active||Time.unscaledTime-lineStartedAt<=.3f)return false;
            bool finished=!voiced||(!voice.HasSpeech&&Time.unscaledTime-lineStartedAt>=DialogueVoice.Length(lines[dialogue.Index].RecordingId));
            int previous=dialogue.Index;var oldStage=World.story.stage;
            bool advanced=dialogue.Advance(finished);
            if(!dialogue.Active){voice.Stop();lines=null;Notify();if(World.story.stage!=oldStage)StoreCheckpoint();}
            else if(previous!=dialogue.Index)SpeakCurrent();
            return advanced;
        }
        public void CancelConversation(){voice?.Stop();dialogue?.Cancel();lines=null;}
        public void SetPaused(bool value)
        {
            Paused=value;Time.timeScale=value?0:1;Cursor.visible=value||Panel!=0;Cursor.lockState=Cursor.visible?CursorLockMode.None:CursorLockMode.Locked;
        }
        void TogglePanel(int panel){Panel=Panel==panel?0:panel;player.RestoreMotion(false);Cursor.visible=Panel!=0;Cursor.lockState=Cursor.visible?CursorLockMode.None:CursorLockMode.Locked;}
        public bool CloseGameplayPanel(){if(Panel==0)return false;Panel=0;Cursor.visible=false;Cursor.lockState=CursorLockMode.Locked;return true;}
        void Notify()
        {
            foreach(var target in targets)if(target!=null&&(target.work==Map02Work.MedicinePickup||target.work==Map02Work.CratePickup))
            {
                // Under the chapter these pre-chapter pickups are not used; the chapter shows its own supplies.
                bool visible=Chapter==null&&(target.work==Map02Work.MedicinePickup?!World.medicineTaken:!World.crateTaken);
                foreach(var r in target.GetComponentsInChildren<Renderer>())r.enabled=visible;
                foreach(var c in target.GetComponents<Collider>())c.enabled=visible;
            }
            Changed?.Invoke();
        }
        public void Say(string text){message=text;messageUntil=Time.unscaledTime+5;}
        public string SaveBlockReason()
        {
            if(!Ready)return "Đang bàn giao từ cảnh cập bến.";
            if(health!=null&&health.IsDead)return "Không thể lưu khi Nam đã gục ngã.";
            if(dialogue.Active)return "Hãy kết thúc hoặc đóng hội thoại trước khi lưu.";
            if(Chapter!=null&&!Chapter.MaySave)return Chapter.SaveReason;
            if(!World.story.MaySave)return "Không thể lưu trong cảnh chuyển hoặc lúc nhiệm vụ thất bại.";
            if(combat!=null&&combat.IsReloading)return "Đợi nạp đạn xong trước khi lưu.";
            return "";
        }
        public bool CanSave()=>string.IsNullOrEmpty(SaveBlockReason());
        public Snapshot Capture(){SyncSkills();SyncBagFromTotals();return new Snapshot {
            hasCharacterState=Bag!=null,stamina=Stamina,bagLayout=Bag?.CaptureLayout(),exploration=Exploration?.Capture(),
            rifleMagazine=combat?.MagazineRounds(WeaponKind.Rifle)??-1,pistolMagazine=combat?.MagazineRounds(WeaponKind.Pistol)??-1,sniperMagazine=combat?.MagazineRounds(WeaponKind.Sniper)??-1,
            crafting=Bag?.CraftingId,craftRemaining=Bag?.CraftRemaining??0,ground=groundItems.Where(g=>g!=null).Select(g=>new GroundSnapshot{id=g.id,count=g.count,condition=g.condition,position=g.transform.position}).ToArray(),
            rifleItemId=RifleItemId,
            chapter=Chapter?.Capture(),world=JsonUtility.FromJson<Map02WorldState>(JsonUtility.ToJson(World)),position=Player.position,yaw=Player.eulerAngles.y,
            health=health.Current,magazine=combat!=null?combat.RoundsInMagazine:0,equipped=combat!=null?(int)combat.EquippedKind:0,
            attackCooldown=combat!=null?combat.AttackCooldownRemaining:0,playSeconds=PlaySeconds,stones=Stones,
            inventory=CaptureCharacterItems(),
            skills=player.GetComponent<PlayerSkills>()?.Capture(),skillMission=player.GetComponent<PlayerSkills>()?.CaptureMission(),checkpoint=checkpoint,patrolCheckpoint=patrolCheckpoint,skillRunId=player.GetComponent<SkillCampaignPersistence>()?.RunId
        };}
        public static Snapshot Parse(string json)
        {
            var data=JsonUtility.FromJson<Snapshot>(json);
            ValidateSnapshot(data);
            if(!string.IsNullOrEmpty(data.patrolCheckpoint))
            {
                var patrol=JsonUtility.FromJson<Snapshot>(data.patrolCheckpoint);ValidateSnapshot(patrol);
                if(!string.IsNullOrEmpty(patrol.checkpoint)||!string.IsNullOrEmpty(patrol.patrolCheckpoint)||patrol.chapter==null||
                    !patrol.chapter.PatrolRouteActive||patrol.chapter.Done("N2_03")||patrol.chapter.Done("N2_04")||patrol.chapter.patrolWarnings!=0)
                    throw new InvalidDataException("Checkpoint tuần tra Map 2 không hợp lệ.");
            }
            return data;
        }
        static void ValidateSnapshot(Snapshot data)
        {
            if(data==null||data.version!=1||data.world==null||data.world.story==null||data.inventory==null||
               !Enum.IsDefined(typeof(Map02Stage),data.world.story.stage)||data.health<0||!float.IsFinite(data.health)||!float.IsFinite(data.yaw)||!float.IsFinite(data.playSeconds)||data.playSeconds<0||data.magazine<0||data.stones<0||!Enum.IsDefined(typeof(WeaponKind),data.equipped)||
               !float.IsFinite(data.position.x)||!float.IsFinite(data.position.y)||!float.IsFinite(data.position.z)||
               data.inventory.Any(i=>i==null||string.IsNullOrEmpty(i.id)||i.count<0)||data.inventory.Select(i=>i.id).Distinct().Count()!=data.inventory.Length)
                throw new InvalidDataException("Checkpoint Map 2 không hợp lệ.");
            data.chapter?.Validate();
            if(data.hasCharacterState&&(!float.IsFinite(data.stamina)||data.stamina<0||!float.IsFinite(data.craftRemaining)||data.craftRemaining<0))throw new InvalidDataException("Trạng thái HUD nhân vật không hợp lệ.");
            if(data.ground!=null&&data.ground.Any(g=>g==null||string.IsNullOrEmpty(g.id)||g.count<=0||!float.IsFinite(g.condition)||!float.IsFinite(g.position.x)||!float.IsFinite(g.position.y)||!float.IsFinite(g.position.z)))throw new InvalidDataException("Đồ vật dưới đất không hợp lệ.");
            if(!string.IsNullOrEmpty(data.skillRunId))ForestSaveSlots.SkillCareerPath(data.skillRunId);
        }
        public void Restore(Snapshot data)
        {
            Chapter?.Cancel();CancelConversation();World=JsonUtility.FromJson<Map02WorldState>(JsonUtility.ToJson(data.world));dialogue=new Map02ConversationSession(World.story);
            Inventory.Clear();foreach(var item in data.inventory)Inventory[item.id]=item.count;Stones=data.stones;PlaySeconds=data.playSeconds;checkpoint=data.checkpoint;patrolCheckpoint=data.patrolCheckpoint;
            var cc=player.GetComponent<CharacterController>();bool enabledBefore=cc!=null&&cc.enabled;if(cc!=null)cc.enabled=false;
            Player.SetPositionAndRotation(data.position,Quaternion.Euler(0,data.yaw,0));if(cc!=null)cc.enabled=enabledBefore;
            if(cameraRig!=null){cameraRig.SetTarget(Player);cameraRig.ClearCinematicView();cameraRig.ClearRecoil();}
            player.RestoreMotion(false);health.RestoreHealth(data.health);
            if(combat!=null)
            {
                Map01RifleVisual.Apply(combat.WeaponFor(WeaponKind.Rifle), data.rifleItemId == Map01M16.Id ? Map01M16.Id : "rifle_standard");
                combat.Equip((WeaponKind)data.equipped);
                if(data.hasCharacterState){combat.RestoreMagazine(WeaponKind.Rifle,data.rifleMagazine);combat.RestoreMagazine(WeaponKind.Pistol,data.pistolMagazine);combat.RestoreMagazine(WeaponKind.Sniper,data.sniperMagazine);}
                combat.RestoreMagazine(data.magazine);combat.RestoreAttackCooldown(data.attackCooldown);
            }
            RestoreSkills(data);
            if(Chapter!=null)Chapter.Restore(data.chapter??Map02ChapterState.Migrate(World));
            SyncSkills();RestoreCharacter(data);
            if(Chapter?.State.PatrolRouteActive==true)EnsurePatrolCheckpoint();else patrolCheckpoint=null;
            ResetMapSafety();
            TickMapSafety();
            Notify();
        }
        public void EnsurePatrolCheckpoint()
        {
            if(Chapter?.State.PatrolRouteActive!=true||!string.IsNullOrEmpty(patrolCheckpoint))return;
            // Old v3 saves may already be at the store/dock. Rebuild only the route baseline,
            // preserving the saved inventory, weapon and health rather than inventing supplies.
            var data=Capture();data.checkpoint=data.patrolCheckpoint=null;
            data.chapter.ResetPatrolRoute();data.position=Chapter.Sites["bridge"].position;
            patrolCheckpoint=JsonUtility.ToJson(data);
        }
        public bool RetryPatrol()
        {
            if(Chapter?.State.PatrolRouteActive!=true)return false;
            EnsurePatrolCheckpoint();var saved=patrolCheckpoint;
            if(string.IsNullOrEmpty(saved))return false;
            Restore(Parse(saved));patrolCheckpoint=saved;checkpoint=saved;SetPaused(false);return true;
        }
        public void StoreCheckpoint(){if(!Ready||!CanSave())return;if(Chapter?.State.PatrolRouteActive!=true)patrolCheckpoint=null;var data=Capture();data.checkpoint=null;checkpoint=JsonUtility.ToJson(data);}
        public bool Retry(){if(string.IsNullOrEmpty(checkpoint))return false;var saved=checkpoint;Restore(Parse(saved));checkpoint=saved;SetPaused(false);return true;}
        public bool SaveSlot(int slot,out string error)
        {
            error=SaveBlockReason();if(!string.IsNullOrEmpty(error))return false;
            var career=player.GetComponent<SkillCampaignPersistence>();if(career!=null&&!career.TryFlush(out error))return false;
            try{ForestSaveSlots.Write(slot,new ForestSaveSlots.Entry{sceneName="Map 2",savedAt=DateTime.UtcNow.ToString("o"),playSeconds=PlaySeconds,location=ObjectiveText,checkpoint=JsonUtility.ToJson(Capture())});return true;}
            catch(Exception e){error="Không thể lưu Map 2: "+e.Message;return false;}
        }
        public bool AutoSaveOnExit(out string error)=>SaveSlot(ForestSaveSlots.AutoSlot,out error);
        public static void BeginGame(ForestSaveSlots.Entry entry)
        {
            if(entry==null||entry.sceneName!="Map 2")throw new InvalidDataException("Bản lưu không thuộc Map 2.");
            foreach(var career in FindObjectsByType<SkillCampaignPersistence>(FindObjectsSortMode.None))
                if(!career.TryFlush(out var error))throw new IOException(error);
            pending=Parse(entry.checkpoint);Time.timeScale=1;SceneManager.LoadScene("Map 2");
        }
        void OnDestroy(){DestroySkills();DestroyCharacter();voice?.Stop();if(hudChart!=null)Destroy(hudChart);if(hudDisc!=null)Destroy(hudDisc);if(hudMiniChart!=null)Destroy(hudMiniChart);if(hudArrow!=null)Destroy(hudArrow);if(hudGoal!=null)Destroy(hudGoal);if(hudHealth!=null)Destroy(hudHealth);Time.timeScale=1;}
        void OnGUI()
        {
            if(!Ready||ForestMenu.Visible)return;
            body??=new GUIStyle(GUI.skin.label){font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"),fontSize=24,wordWrap=true,normal={textColor=new Color(.94f,.88f,.70f)}};
            heading??=new GUIStyle(body){fontSize=28,fontStyle=FontStyle.Bold};small??=new GUIStyle(body){fontSize=20};
            var matrix=GUI.matrix;var color=GUI.color;int depth=GUI.depth;
            GUI.color=Color.white;GUI.depth=-50;
            GUI.matrix=Matrix4x4.Scale(new Vector3(Screen.width/1600f,Screen.height/900f,1));
            if(dialogue.Active)
            {
                GUI.Box(new Rect(180,702,1240,176),GUIContent.none);
                var line=lines[dialogue.Index];GUI.Label(new Rect(208,717,1184,38),line.speaker,heading);GUI.Label(new Rect(208,757,1184,76),line.text,body);
                GUI.Label(new Rect(208,842,1184,30),$"{dialogue.Index+1}/{lines.Length} · E: tiếp tục / xác nhận · Esc: đóng",small);
            }
            else if(CharacterHud!=null) { /* Shared character HUD owns panels and gameplay presentation. */ }
            else if(Panel!=0)
            {
                GUI.color=new Color(.025f,.035f,.025f,.94f);GUI.DrawTexture(new Rect(160,100,1280,700),Texture2D.whiteTexture);GUI.color=Color.white;
                GUI.Box(new Rect(160,100,1280,700),GUIContent.none);GUI.Label(new Rect(200,125,1200,45),Panel==1?"TÚI ĐỒ":Panel==2?"NHIỆM VỤ":"BẢN ĐỒ MAP 2",heading);
                if(Panel==1)DrawInventoryPanel();
                else if(Panel==2)DrawObjectiveChoices(new Rect(200,190,1120,500));
                else {DrawMap(new Rect(205,190,510,510));DrawObjectiveChoices(new Rect(750,190,620,500));}
                GUI.Label(new Rect(200,730,1120,45),"Esc: đóng",small);
            }
            else
            {
                if(Chapter?.Busy!=true)DrawGameplayHud();
            }
            if(Panel==0)Chapter?.DrawOverlay();
            if(World.story.failed){GUI.Box(new Rect(420,290,760,240),GUIContent.none);GUI.Label(new Rect(455,320,690,95),World.story.failureCause,body);if(GUI.Button(new Rect(650,440,300,55),"Thử lại checkpoint"))Retry();}
            if(Panel==0&&CharacterHud==null&&Chapter?.Busy!=true&&Chapter?.Transitioning!=true&&Time.unscaledTime<messageUntil)GUI.Label(new Rect(400,250,800,90),message,body);
            GUI.matrix=matrix;GUI.color=color;GUI.depth=depth;
        }
        void DrawMap(Rect area)
        {
            GUI.Box(area,GUIContent.none);
            if(hudChart!=null)GUI.DrawTexture(area,hudChart,ScaleMode.StretchToFill);
            DrawRoute(area,false);
            Vector2 Plot(Vector3 p)=>Map02Navigation.Plot(p,area);
            var previous=GUI.color;
            if(questLayout!=null&&questLayout.homePositions!=null)
                foreach(var home in questLayout.homePositions){var p=Plot(home);GUI.color=new Color(.56f,.48f,.28f);GUI.DrawTexture(new Rect(p.x-6,p.y-6,12,12),Texture2D.whiteTexture);}
            GUI.color=Color.white;
            foreach(var target in targets)if(target!=null&&ObjectiveAvailable(target))
            {
                var p=Plot(target.standPoint!=null?target.standPoint.position:target.transform.position);
                if(target==TrackedObjective)DrawObjectivePin(p,14);else {GUI.color=new Color(.65f,.65f,.65f,.8f);GUI.DrawTexture(new Rect(p.x-3,p.y-3,6,6),Texture2D.whiteTexture);GUI.color=Color.white;}
            }
            DrawNamArrow(Plot(Player.position),25);GUI.color=previous;
            GUI.Label(new Rect(area.x+10,area.y+8,220,30),"BẮC ↑",small);
            GUI.Label(new Rect(area.x,area.yMax+8,area.width,40),"▲ Xanh: Nam · ◆ Vàng: việc đang theo dõi · J đổi việc",small);
        }
    }
}
