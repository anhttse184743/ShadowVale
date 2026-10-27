using System.Linq;
using ShadowVale.Gameplay.Combat;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
namespace ShadowVale.Map01
{
    public sealed partial class Map02ChapterDirector
    {
        Vector3 DayFamily=>sites["dayOneFamily"].position;
        Vector3 DayTaskPoint(Map02ChapterTask t)=>t.id=="N1_04"?sites["nightOneRest"].position:(t.id=="D1_02"||t.id=="D1_03"||t.id=="N1_01")?DayFamily:sites[t.site].position;
        enum DayActivity { None, Packing, Dock, Lamp, Gate, Path }
        DayActivity dayActivity;
        Map02DayOneState Day=>State.dayOne;
        int selectedCrate=-1,selectedBoard=-1,postIndex,postPart;
        bool selectionRotated,daySpaceReleased,dayInputReady,dayCloseRequested;
        float dayClock;int dayAttemptCycle=-1;
        int dockSlot;float dockOffset;
        Vector2Int packingCursor=new Vector2Int(2,0);
        bool dayArrivalStarting,dayDockEnding;
        string dayFeedback="";
        bool dayCursorVisible;CursorLockMode dayCursorLock;
        WeaponKind dayWeapon;bool dayReturnInput;float dayReturnUntil;
        bool DayInputHeld=>Mouse.current?.leftButton.isPressed==true||Mouse.current?.rightButton.isPressed==true||Keyboard.current?.spaceKey.isPressed==true||Keyboard.current?.eKey.isPressed==true||Keyboard.current?.enterKey.isPressed==true||Keyboard.current?.numpadEnterKey.isPressed==true;
        bool DayReturningInput=>dayReturnInput&&(DayInputHeld||Time.unscaledTime<dayReturnUntil);
        Transform dayProps;
        readonly System.Collections.Generic.List<Transform> physicalCrates=new();
        readonly System.Collections.Generic.List<Transform> physicalStakes=new();
        readonly System.Collections.Generic.List<Transform> physicalBoards=new();
        readonly System.Collections.Generic.List<Transform> postLamps=new(),postGates=new(),postLatches=new(),postBundles=new();
        public bool DayActivityActive=>dayActivity!=DayActivity.None;
        public string DayActivityName=>dayActivity.ToString();
        public string ReservedMedicine=>"Vật phẩm nhiệm vụ: thuốc cho ông Sáu × "+((Day.medicineMask&1)==0?1:0)+" · thuốc tổ gác × "+((Day.medicineMask&2)==0?1:0)+" (không dùng hồi máu / không vứt bỏ)";
        System.Collections.IEnumerator PreloadDayOneVoices(){
            var def=Map02ChapterDefinition.Load();var scenes=def.tasks.Where(t=>t.phase<=1&&!string.IsNullOrEmpty(t.scene)).Select(t=>"m2v3_"+t.scene).Concat(new[]{"m2v3_arrivalbank","m2v3_repairdone","m2v3_duckevent"}).ToHashSet();
            foreach(var line in def.entries.Where(e=>scenes.Contains(e.conversation)&&!string.IsNullOrWhiteSpace(e.text)))yield return DialogueVoice.Preload(line.id);
        }
        void EnsureDayState()
        {
            State.dayOne??=new Map02DayOneState();
            if(!Day.initialized){Day.Initialize(Random.Range(1,int.MaxValue));Day.stockCredited=State.Done("D1_04");if(State.Done("D1_03"))Day.medicineMask|=1;if(State.Done("N1_03"))Day.medicineMask|=2;if(State.Done("D1_05")){Day.stakeHits=6;for(int i=0;i<3;i++)Day.boards[i]=new Map02DayOneState.Placement{x=i,y=i};}}
        }
        bool RouteDayInteraction(Map02ChapterTask t)
        {
            EnsureDayState();
            if(t.id=="D1_04")
            {
                if(State.Count(t.id)>=3){OpenDay(DayActivity.Packing,t);return true;}
                if(string.IsNullOrEmpty(State.carry)){dayWeapon=combat.EquippedKind;Day.carriedWeapon=(int)dayWeapon;State.carry=t.id;combat.Equip(WeaponKind.Unarmed);Mission.Say("Mang thùng tới khu nhận hàng trước kho kín.");}
                else{State.carry=null;State.workCount=State.Increment(t.id);if(System.Enum.IsDefined(typeof(WeaponKind),Day.carriedWeapon))combat.Equip((WeaponKind)Day.carriedWeapon);Day.carriedWeapon=-1;Mission.Say(State.workCount==3?"Đã chuyển 3/3 thùng — nhấn E tại kho để sắp xếp.":$"Đã chuyển {State.workCount}/3 thùng.");Mission.StoreCheckpoint();}
                UpdateMarkers();SyncDayProps();return true;
            }
            if(t.id=="D1_05"){if(!State.introduced.Contains(t.id)){BeginScene(t);scenePurpose=4;}else OpenDay(DayActivity.Dock,t);return true;}
            if((t.id=="N1_02"||t.id=="N1_03")&&(Day.postsReached&(t.id=="N1_02"?1:2))!=0)
            {
                postIndex=t.id=="N1_02"?0:1;
                if(!Day.posts[postIndex].Safe){Mission.Say("Chốt chưa sẵn sàng: "+Day.posts[postIndex].Problems+". E kiểm tra đồ vật ở gần.");return true;}
                BeginScene(t);return true;
            }
            return false;
        }
        void OpenDay(DayActivity activity,Map02ChapterTask task)
        {
            dayInputReady=false;dayCloseRequested=false;packingMouseControl=false;ResetStakeStrike();dayActivity=activity;workTask=task;dayClock=0;packingCursor=new Vector2Int(2,0);dockSlot=System.Array.FindIndex(Day.boards,p=>p.x<0);if(dockSlot<0)dockSlot=0;dockOffset=.24f;dayFeedback="";selectedCrate=selectedBoard=-1;selectionRotated=false;daySpaceReleased=false;dayAttemptCycle=-1;
            if(activity==DayActivity.Packing){selectedCrate=System.Array.FindIndex(Day.crates,p=>p.x<0);if(selectedCrate>=0)selectionRotated=Day.crates[selectedCrate].rotated;}   // the first crate still to store is in hand
            dayWeapon=combat.EquippedKind;combat.Equip(WeaponKind.Unarmed);combat.SetHandsOccupied(true);player.RestoreMotion(false);
            dayCursorVisible=Cursor.visible;dayCursorLock=Cursor.lockState;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        }
        void RequestCloseDay()
        {
            if(dayActivity==DayActivity.Dock&&StrikeRecovering){dayCloseRequested=true;dayFeedback="Đợi nhát búa kết thúc rồi rời việc.";return;}
            CloseDay();
        }
        void CloseDay(bool checkpoint=true)
        {
            if(!DayActivityActive)return;
            dayActivity=DayActivity.None;ResetStakeStrike();dayCloseRequested=false;EndWorkView();dayReturnInput=DayInputHeld;dayReturnUntil=Time.unscaledTime+.12f;Cursor.visible=dayCursorVisible;Cursor.lockState=dayCursorLock;
            combat.Equip(dayWeapon);combat.SetHandsOccupied(false);combat.RestoreAfterCinematic();player.RestoreMotion(false);ForestMenu.SuppressKeysAfterCutscene();
            SyncDayProps();if(checkpoint)Mission.StoreCheckpoint();
        }
        void StartPost(Map02ChapterTask t)
        {
            EnsureDayState();postIndex=t.id=="N1_02"?0:1;Day.postsReached|=1<<postIndex;Mission.RequireInteractionRelease();
            Mission.Say("Kiểm tra đèn, cửa rào nhỏ và lối đi; xong trở lại người gác báo chốt sẵn sàng.");UpdateMarkers();Mission.StoreCheckpoint();
        }
        bool TickDay(float dt)
        {
            EnsureDayState();TickDuck(dt);
            if(dayReturnInput){if(DayReturningInput)return true;dayReturnInput=false;}
            if(State.phase==0&&!Day.arrivalSeen&&!State.Done("D1_01")&&sceneTask==null&&!DayActivityActive){dayArrivalStarting=true;BeginScene(Map02ChapterDefinition.Load().Find("D1_01"));scenePurpose=5;return true;}
            if(DayActivityActive)
            {
                Cursor.visible=true;Cursor.lockState=CursorLockMode.None;
                var kb=Keyboard.current;
                if(!dayInputReady){if(!DayInputHeld){dayInputReady=true;dayClock=0;}return true;}
                TickStakeCommit();
                if(dayCloseRequested&&!StrikeRecovering){CloseDay();return true;}
                if(kb?.escapeKey.wasPressedThisFrame==true){RequestCloseDay();return true;}
                dayClock+=Time.deltaTime;
                TickDayKeyboard(kb);if(!DayActivityActive)return true;
                if(kb?.spaceKey.isPressed!=true)daySpaceReleased=true;
                if(dayActivity==DayActivity.Dock&&Day.stakeHits<6&&kb?.spaceKey.wasPressedThisFrame==true&&daySpaceReleased)
                {
                    daySpaceReleased=false;
                    if(StrikeRecovering){dayFeedback="Đợi búa nâng lên rồi nhấn nhịp tiếp theo.";return true;}
                    int cycle=Mathf.FloorToInt(dayClock/Map02DayOneState.SweepSeconds(Day.stakeHits/3));
                    if(cycle==dayAttemptCycle){dayFeedback="Đợi kim quay lại lượt mới; mỗi lượt chỉ nhận một nhát.";return true;}
                    dayAttemptCycle=cycle;
                    float needle=Mathf.PingPong(dayClock/Map02DayOneState.SweepSeconds(Day.stakeHits/3),1);
                    bool good=Mathf.Abs(needle-.5f)<=Map02DayOneState.ZoneWidth(Day.stakeHits/3)*.5f;
                    StrikeStake(good);dayFeedback=good?"✓ Đúng nhịp — đang hạ búa.":"Trượt nhịp. Nhả Space, đợi lượt kim mới rồi thử lại.";
                }
                if(dayActivity==DayActivity.Dock&&Day.stakeHits>=6&&!DockHammering&&kb!=null){
                    dockOffset=Mathf.Clamp(dockOffset+((kb.dKey.isPressed?1:0)-(kb.aKey.isPressed?1:0))*.3f*Time.deltaTime,-.45f,.45f);
                }
                if(dayActivity==DayActivity.Lamp&&kb!=null){var p=Day.posts[postIndex];p.lampAngle=Mathf.Repeat(p.lampAngle+((kb.dKey.isPressed?1:0)-(kb.aKey.isPressed?1:0))*65*dt,360);SyncDayProps();}
                return true;
            }
            if(State.activeTask!="N1_02")duckPoint=null;
            if(State.phase==1&&sceneTask==null&&escort!=null&&!Day.duckSeen&&State.activeTask=="N1_02"&&DuckReached())
            {Day.duckSeen=true;BeginDuckStartle();}
            if(State.phase!=1||sceneTask!=null||escort!=null||Mission.Panel!=0)return false;
            return false;
        }
        // The duck in the bushes (doc, Night 1): on the way to the bamboo post, not at it. Halfway along the walk
        // from where the patrol sets out; the old 9 m ring round the post stays as the fallback so it always happens.
        public Vector3? DuckPoint=>duckPoint;
        Vector3? duckPoint;
        bool DuckReached()
        {
            var nam=Mission.Player.position;var post=sites["bamboo"].position;
            duckPoint??=RouteMiddle(nam,post);
            if(Vector3.Distance(escort.transform.position,nam)>6)return false;   // Hùng has to be beside Nam to grab his shoulder
            return Vector3.Distance(nam,duckPoint.Value)<3.5f||Vector3.Distance(nam,post)<9;
        }
        static Vector3 RouteMiddle(Vector3 from,Vector3 to)
        {
            var path=new NavMeshPath();
            if(!NavMesh.SamplePosition(from,out var a,2,NavMesh.AllAreas)||!NavMesh.SamplePosition(to,out var b,2,NavMesh.AllAreas)||!NavMesh.CalculatePath(a.position,b.position,NavMesh.AllAreas,path)||path.corners.Length<2)return Vector3.Lerp(from,to,.5f);
            float half=0;for(int i=1;i<path.corners.Length;i++)half+=Vector3.Distance(path.corners[i-1],path.corners[i]);
            return Along(path.corners,half*.5f);
        }
        void TickDayKeyboard(Keyboard kb)
        {
            if(kb==null)return;
            int number=kb.digit1Key.wasPressedThisFrame?0:kb.digit2Key.wasPressedThisFrame?1:kb.digit3Key.wasPressedThisFrame?2:kb.digit4Key.wasPressedThisFrame?3:kb.digit5Key.wasPressedThisFrame?4:-1;
            if(dayActivity==DayActivity.Packing){
                if(kb.leftArrowKey.wasPressedThisFrame||kb.rightArrowKey.wasPressedThisFrame||kb.upArrowKey.wasPressedThisFrame||kb.downArrowKey.wasPressedThisFrame)packingMouseControl=false;
                if(number>=0&&number<3)SelectPackingCrate(number);
                if(kb.rightArrowKey.wasPressedThisFrame||kb.leftArrowKey.wasPressedThisFrame)packingCursor.x=Mathf.Clamp(packingCursor.x+(kb.rightArrowKey.wasPressedThisFrame?1:-1),0,5);
                if(kb.upArrowKey.wasPressedThisFrame||kb.downArrowKey.wasPressedThisFrame)packingCursor.y=Mathf.Clamp(packingCursor.y+(kb.upArrowKey.wasPressedThisFrame?1:-1),0,3);
                if(kb.rKey.wasPressedThisFrame&&selectedCrate>=0)selectionRotated=!selectionRotated;
                if(kb.eKey.wasPressedThisFrame)PlacePackingSelection(packingCursor.x,packingCursor.y);
                if(kb.backspaceKey.wasPressedThisFrame){int under=CrateAt(packingCursor.x,packingCursor.y);if(under>=0)LiftCrate(under);}
                if(kb.enterKey.wasPressedThisFrame)CompletePacking();
            }else if(dayActivity==DayActivity.Dock&&Day.stakeHits>=6){
                if(number>=0)SelectDockBoard(number);
                if(kb.rKey.wasPressedThisFrame&&selectedBoard>=0)selectionRotated=!selectionRotated;
                if(kb.upArrowKey.wasPressedThisFrame)dockSlot=(dockSlot+2)%3;if(kb.downArrowKey.wasPressedThisFrame)dockSlot=(dockSlot+1)%3;
                if(kb.eKey.wasPressedThisFrame)ConfirmDockBoard();
            }else if(dayActivity==DayActivity.Lamp){if(kb.eKey.wasPressedThisFrame)ConfirmPostPart();}
            else if(dayActivity==DayActivity.Gate){
                var p=Day.posts[postIndex];if(kb.aKey.wasPressedThisFrame){p.gateClosed=true;SyncDayProps();}
                if(kb.dKey.wasPressedThisFrame){if(p.gateClosed){p.latchSeated=true;SyncDayProps();}else dayFeedback="Phải khép cửa sát khung trước.";}
                if(kb.eKey.wasPressedThisFrame)ConfirmPostPart();
            }else if(dayActivity==DayActivity.Path){
                if(number==0)dayFeedback="Bó củi sẽ chắn khoảng mở cửa. Chọn vị trí khác.";
                if(number==1){Day.posts[postIndex].bundleStowed=true;SyncDayProps();}
                if(kb.eKey.wasPressedThisFrame)ConfirmPostPart();
            }
        }
        void ConfirmPostPart(){var p=Day.posts[postIndex];bool okay=dayActivity==DayActivity.Lamp?p.LampSafe:dayActivity==DayActivity.Gate?p.gateClosed&&p.latchSeated:p.bundleStowed;if(okay)CloseDay();else dayFeedback=dayActivity==DayActivity.Lamp?"Ánh sáng vẫn lọt ra ngoài làng.":dayActivity==DayActivity.Gate?"Cửa rào chưa khép và cài chắc.":"Bó vật liệu vẫn nằm trên lối đi.";}
        void SaveDayProgress()
        {
            // Session state is already atomic. Allow a checkpoint only between completed units.
            var saved=dayActivity;var equipped=combat.EquippedKind;dayActivity=DayActivity.None;combat.Equip(dayWeapon);Mission.StoreCheckpoint();combat.Equip(equipped);dayActivity=saved;
        }
        void SyncRepairCount()
        {
            int completed=Day.stakeHits/3+Day.BoardCount;
            while(State.Count("D1_05")<completed)State.Increment("D1_05");State.workCount=completed;
        }
        void CompletePacking()
        {
            if(!Day.Packed){dayFeedback="Chưa xếp đủ ba thùng hợp lệ.";return;}
            CloseDay(false);if(!Day.stockCredited){Day.stockCredited=true;StoreRounds(workTask);}Finish(workTask);SyncDayProps();
        }
        void ConfirmDockBoard()
        {
            if(DockHammering){dayFeedback="Đợi nhát búa cuối kết thúc rồi kê ván.";return;}
            if(Day.DockReady){CompleteDock();return;}
            if(Day.LayBoard(selectedBoard,dockSlot,selectionRotated,dockOffset,out var reason)){
                SyncRepairCount();SyncDayProps();SaveDayProgress();dayFeedback="✓ Ván đã kê chắc. Chọn tấm tiếp theo; đủ ba tấm thì kiểm tra bến.";
                selectedBoard=-1;selectionRotated=false;dockOffset=.24f;int next=System.Array.FindIndex(Day.boards,p=>p.x<0);if(next>=0)dockSlot=next;
            }else dayFeedback=reason;
        }
        void CompleteDock()
        {
            if(!Day.DockReady){dayFeedback="Cần hoàn tất hai cọc và ba tấm ván.";return;}
            CloseDay(false);dayDockEnding=true;BeginScene(workTask); // Short confirmation after the physical work.
        }
        Map02DialogueCatalog.Entry[] DayLines(Map02ChapterTask task,Map02DialogueCatalog.Entry[] standard)
        {
            if(dayArrivalStarting)return Map02ChapterDefinition.Load().Lines("arrivalbank");
            if(dayDockEnding)return Map02ChapterDefinition.Load().Lines("repairdone");
            return standard;
        }
        void PlayDaySound(string id,float volume=.35f,Vector3? at=null)
        {var clip=Resources.Load<AudioClip>("Map02DayOne/"+id);if(clip!=null)AudioSource.PlayClipAtPoint(clip,at??Mission.Player.position+Mission.Player.forward*2,volume);}
        string DayInstruction(Map02ChapterTask t)
        {
            if(t.id=="D1_04"&&State.Count(t.id)>=3)return "Đã chuyển 3/3 thùng — tới kho kín, nhấn E sắp xếp vào kho.";
            if(t.id=="D1_05")return $"Hai cọc: {Day.stakeHits/3}/2 · ba ván: {Day.BoardCount}/3. E làm việc cùng chú Bảy.";
            if((t.id=="N1_02"||t.id=="N1_03")&&(Day.postsReached&(t.id=="N1_02"?1:2))!=0)return "Kiểm tra ba hạng mục: đèn dầu, cửa rào và lối đi. Đến gần từng vật để thấy nhắc E. Sửa xong, quay lại "+(t.id=="N1_02"?"tổ gác":"anh Lực")+" ở ghim vàng báo chốt sẵn sàng.";
            return null;
        }
    }
}
