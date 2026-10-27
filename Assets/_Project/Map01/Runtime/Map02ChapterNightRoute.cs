using System;
using UnityEngine;
using UnityEngine.AI;

namespace ShadowVale.Map01
{
    public sealed partial class Map02ChapterDirector
    {
        static readonly string[] NightRouteIds={"ditchApproach","villageLane","southBank","dockStairs"};
        static readonly string[] NightRouteNames={"bờ mương","lối qua xóm","bờ nam","bậc bến"};
        public Vector3[] NightTwoRoute {get;private set;}=Array.Empty<Vector3>();
        public int NightTwoRouteProgress
        {
            get{int i=0;while(i<NightRouteIds.Length&&State.night2RouteVisited.Contains(NightRouteIds[i]))i++;return i;}
        }
        public Vector3 NightDoorPosition=>sites["nightHatch"].position;
        public Map02UndergroundStore UndergroundStore {get;private set;}
        public Vector3 NightDoorStand=>UndergroundStore!=null?UndergroundStore.TopStand:NightDoorPosition+Vector3.left*1.8f;
        GameObject nightHatchRoot;
        GameObject nightFord;
        AudioSource chainSound,nightHiss;
        float liveRadioRemaining,hissRemaining,chainRemaining;
        bool nightAudioPaused;
        Map02DialogueCatalog.Entry liveRadio;
        bool NightReturnPending=>State.night2GuidanceStage<7||liveRadioRemaining>0||chainRemaining>0;

        RiverWater[] nightRivers=Array.Empty<RiverWater>();
        static readonly Vector3[] NightCapsuleOffsets={Vector3.zero,Vector3.left*.3f,Vector3.right*.3f,Vector3.forward*.3f,Vector3.back*.3f};
        public Vector3[] NightBankRoute {get;private set;}=Array.Empty<Vector3>();
        public bool NightBankMovementRestricted=>State.phase==3&&!State.Done("N2_03");
        public bool TryUnlockedRiverSurface(Vector3 position,out float height)
        {
            height=0;if(NightBankMovementRestricted)return false;
            foreach(var river in nightRivers)
                if(river!=null&&river.TrySample(position,river.Clock,out var sample)){height=sample.height;return true;}
            return false;
        }
        Vector3 NightBankScanPoint(Vector3 origin)
        {
            var route=NightBankRoute.Length>1?NightBankRoute:Layout.nightTwoBankRoute;
            var nearest=route[0];float distance=float.MaxValue;
            for(int i=1;i<route.Length;i++){var p=NearestPlanarPoint(origin,route[i-1],route[i]);float d=(p-origin).sqrMagnitude;if(d<distance){nearest=p;distance=d;}}
            return nearest;
        }
        void InitializeNightBankRoute()
        {
            Site("nightHatch",Layout.nightTwoHatch);
            sites["nightPatrol"].position=NavMesh.SamplePosition(Layout.nightTwoPatrolCenter,out var floor,1,NavMesh.AllAreas)?floor.position:Layout.nightTwoPatrolCenter;
            NightBankRoute=(Vector3[])Layout.nightTwoBankRoute.Clone();
            for(int i=0;i<NightBankRoute.Length;i++)if(NavMesh.SamplePosition(NightBankRoute[i],out var hit,1,NavMesh.AllAreas))NightBankRoute[i]=hit.position;
            if(NightBankRoute.Length>0)NightBankRoute[NightBankRoute.Length-1]=NightDoorPosition;
            nightRivers=FindObjectsByType<RiverWater>(FindObjectsSortMode.None);
        }
        static Vector3 NearestPlanarPoint(Vector3 p,Vector3 a,Vector3 b)
        {
            a.y=b.y=p.y;var line=b-a;
            return a+line*Mathf.Clamp01(Vector3.Dot(p-a,line)/Mathf.Max(.0001f,line.sqrMagnitude));
        }
        bool OnNightBridge(Vector3 p)
        {
            var deck=NightObservationBridge;if(deck==null)return false;
            var local=deck.transform.InverseTransformPoint(p)-deck.center;
            // Shrink to the walkable deck, so the exception never admits the river below it.
            return Mathf.Abs(local.x)<=deck.size.x*.5f+.4f&&Mathf.Abs(local.z)<=deck.size.z*.5f-.25f&&p.y>=deck.bounds.max.y-.5f;
        }
        public bool NightBankPositionAllowed(Vector3 p)
        {
            if(!NightBankMovementRestricted)return true;
            if(OnNightBridge(p))return true;
            foreach(var river in nightRivers)
            {
                if(river==null)continue;
                // Test the capsule footprint regardless of jump height: jumping cannot bypass water bounds.
                foreach(var offset in NightCapsuleOffsets)
                    if(river.TrySample(p+offset,0,out _))return false;
            }
            // Only the actual river is blocked. Dry roads, banks and rice fields remain walkable.
            return true;
        }
        bool NightMovementSegmentAllowed(Vector3 a,Vector3 b)
        {
            int steps=Mathf.Max(1,Mathf.CeilToInt(Vector3.ProjectOnPlane(b-a,Vector3.up).magnitude/.15f));
            for(int i=1;i<=steps;i++)if(!NightBankPositionAllowed(Vector3.Lerp(a,b,(float)i/steps)))return false;
            return true;
        }
        public Vector3 ConstrainNightMovement(Vector3 from,Vector3 displacement)
        {
            if(UndergroundStore?.ContainsAuthorizedPlayer(from)==true)return displacement;
            if(!NightBankMovementRestricted)return displacement;
            // Vertical motion is untouched. Clip horizontal travel before CharacterController.Move,
            // then slide along the boundary instead of teleporting the player back every frame.
            var planar=Vector3.ProjectOnPlane(displacement,Vector3.up);
            if(NightMovementSegmentAllowed(from,from+planar))return displacement;
            var x=new Vector3(planar.x,0,0);var z=new Vector3(0,0,planar.z);
            if(x.sqrMagnitude>z.sqrMagnitude&&NightMovementSegmentAllowed(from,from+x))return x+Vector3.up*displacement.y;
            if(NightMovementSegmentAllowed(from,from+z))return z+Vector3.up*displacement.y;
            if(NightMovementSegmentAllowed(from,from+x))return x+Vector3.up*displacement.y;
            float low=0,high=1;
            for(int i=0;i<10;i++){float mid=(low+high)*.5f;if(NightMovementSegmentAllowed(from,from+planar*mid))low=mid;else high=mid;}
            return planar*low+Vector3.up*displacement.y;
        }
        void SyncNightBankState()
        {
            if(nightHatchRoot!=null)nightHatchRoot.SetActive(true);
            UndergroundStore?.SyncState();
            if(nightFord!=null)nightFord.SetActive(State.Done("N2_03"));
            if(UndergroundStore?.ContainsAuthorizedPlayer(Mission.Player.position)==true)return;
            if(!NightBankMovementRestricted||NightBankPositionAllowed(Mission.Player.position))return;
            // Migrate old saves/checkpoints from the retired field observation marker.
            var body=Mission.Player.GetComponent<CharacterController>();bool enabled=body!=null&&body.enabled;
            if(body!=null)body.enabled=false;Mission.Player.position=NightObservationSpawn;if(body!=null)body.enabled=enabled;
            player.RestoreMotion(false);
        }
        void BuildNightRiverCrossing()
        {
            // Guidance connects the two bank NavMeshes; Nam walks on the existing river bed.
            // No renderer, raised floor or extra collider is created across the water.
            var start=Layout.nightTwoRiverEntry;
            var end=Layout.nightTwoRiverExit;
            if(NavMesh.SamplePosition(start,out var a,2,NavMesh.AllAreas))start=a.position;
            if(NavMesh.SamplePosition(end,out var b,2,NavMesh.AllAreas))end=b.position;
            nightFord=new GameObject("Đêm 2 — chỉ dẫn lội sông tự nhiên");nightFord.transform.SetParent(root.transform,false);nightFord.SetActive(false);
            var link=nightFord.AddComponent<Unity.AI.Navigation.NavMeshLink>();link.startPoint=start;link.endPoint=end;link.width=2;link.bidirectional=true;
        }
        void ReleaseNightRiverCrossing(){if(nightFord!=null)Destroy(nightFord);}

        void BuildNightTwoRoute()
        {
            var points=new Vector3[Layout.nightTwoEvacuationRoute.Length+1];
            for(int i=0;i<points.Length;i++)
            {
                var p=i==points.Length-1?sites["dock"].position:Layout.nightTwoEvacuationRoute[i];
                if(!NavMesh.SamplePosition(p,out var floor,4,NavMesh.AllAreas)){Debug.LogWarning("Night 2 route point outside NavMesh: "+p);NightTwoRoute=Array.Empty<Vector3>();return;}
                points[i]=floor.position;
            }
            NightTwoRoute=points;
            var art=Resources.Load<GameObject>("Story/NightStore/UndergroundStore");
            if(art==null)throw new InvalidOperationException("Missing Blender-authored NightStore prefab. Run Map02UndergroundStoreSetup.Build.");
            nightHatchRoot=Instantiate(art,root.transform);nightHatchRoot.name="Kho ngầm 8 x 5 x 4 — Blender";nightHatchRoot.transform.position=NightDoorPosition;
            UndergroundStore=nightHatchRoot.AddComponent<Map02UndergroundStore>();UndergroundStore.Initialize(this);
            chainSound=new GameObject("Xích sắt rất xa — cue riêng").AddComponent<AudioSource>();chainSound.transform.SetParent(root.transform,false);chainSound.clip=Resources.Load<AudioClip>("OperationsAudio/night2_chain");chainSound.playOnAwake=false;chainSound.spatialBlend=.85f;chainSound.rolloffMode=AudioRolloffMode.Linear;chainSound.minDistance=12;chainSound.maxDistance=180;chainSound.volume=.65f;
            nightHiss=gameObject.AddComponent<AudioSource>();nightHiss.playOnAwake=false;nightHiss.clip=Resources.Load<AudioClip>("OperationsAudio/radio_off");nightHiss.volume=.08f;nightHiss.spatialBlend=0;
            BuildNightRiverCrossing();
        }
        bool InteractNightRoute(Map02ChapterTask task)
        {
            if(task.kind=="patrolReport")
            {
                State.night2GuidanceStage=1;Finish(task);ShowNightRadio(0);return true;
            }
            if(task.kind=="inspectDoor")
            {
                if(UndergroundStore?.CanInspect!=true){Mission.Say("E mở nắp · tới điểm Space để bám thang. Xuống kho, đến kệ vật tư rồi nhấn E kiểm tra.");return false;}
                Mission.Say("Kho vật tư còn an toàn. Lên thang trở lại mặt đất rồi qua sông kiểm tra tuyến sơ tán.");Finish(task);return true;
            }
            if(task.kind=="inspectRoute")
            {
                if(NightTwoRouteProgress!=NightRouteIds.Length-1){Mission.Say("Theo các ghim để kiểm tra lần lượt bờ mương, lối qua xóm và bờ nam.");return false;}
                State.night2RouteVisited.Add(NightRouteIds[NightTwoRouteProgress]);Finish(task);Mission.Say("Bậc bến chắc chắn. Đường sơ tán đã được kiểm tra đủ bốn đoạn.");return true;
            }
            if(task.id=="N2_05"&&NightReturnPending)
            {
                if(!State.night2ChainCuePlayed){liveRadio=null;liveRadioRemaining=0;State.night2GuidanceStage=5;}
                Mission.Say("Có tiếng kim loại xa ngoài đồng. Nghe cùng Hùng rồi nhấn E để nói chuyện.");return true;
            }
            return false;
        }
        void TickNightTwoRoute(float dt)
        {
            if(State.phase!=3)return;
            if(State.Available(Map02ChapterDefinition.Load().Find("N2_04")))
            {
                int i=NightTwoRouteProgress;
                if(NightTwoRoute.Length==4&&i<3&&Vector3.Distance(Mission.Player.position,NightTwoRoute[i])<2.4f)
                {
                    State.night2RouteVisited.Add(NightRouteIds[i]);Mission.Say($"Đã kiểm tra {NightRouteNames[i]} · {i+1}/4 đoạn.");UpdateMarkers();Mission.StoreCheckpoint();
                }
            }
        }
        Vector3 NightRouteMarker=>NightTwoRoute.Length==4?NightTwoRoute[Mathf.Min(3,NightTwoRouteProgress)]:sites["dock"].position;
        void ShowNightRadio(int index)
        {
            var entries=Map02ChapterDefinition.Load().Lines("lights");if(index<0||index>=entries.Length)return;
            liveRadio=entries[index];voice.Stop();
            bool recording=Map02DialogueCatalog.HasApprovedRecording(liveRadio);
            liveRadioRemaining=recording?DialogueVoice.Length(liveRadio.id):Mathf.Max(2.5f,liveRadio.text.Length/12f);
            if(recording)voice.Play(liveRadio.id);
            if(liveRadio.radio&&nightHiss!=null){nightHiss.Play();hissRemaining=.18f;}
        }
        void TickNightRadio(float dt,float nearestAngle,bool approaching,float distance)
        {
            if(State.patrolFocusSeconds>0||NightSuspicionSpeaking||NightPatrolFailureActive)return;
            if(State.phase!=3||!State.Done("N2_02")||State.Done("N2_05"))return;
            if(hissRemaining>0){hissRemaining-=dt;if(hissRemaining<=0&&nightHiss!=null)nightHiss.Stop();}
            if(liveRadioRemaining>0){liveRadioRemaining=Mathf.Max(0,liveRadioRemaining-dt);if(liveRadioRemaining>0)return;liveRadio=null;}
            if(chainRemaining>0){chainRemaining=Mathf.Max(0,chainRemaining-dt);return;}
            switch(State.night2GuidanceStage)
            {
                case 0:State.night2GuidanceStage=1;break;
                case 1:
                    if(approaching&&nearestAngle<40&&distance<55){ShowNightRadio(1);State.night2GuidanceStage=2;}
                    else if(distance>55){ShowNightRadio(2);State.night2GuidanceStage=3;}
                    break;
                case 2:if(nearestAngle>30||distance>55){ShowNightRadio(2);State.night2GuidanceStage=3;}break;
                case 3:ShowNightRadio(3);State.night2GuidanceStage=4;break;
                case 4:ShowNightRadio(4);State.night2GuidanceStage=5;break;
                case 5:
                    if(!State.night2ChainCuePlayed){State.night2ChainCuePlayed=true;PlayChainSound();chainRemaining=chainSound!=null&&chainSound.clip!=null?chainSound.clip.length:3.8f;Mission.StoreCheckpoint();}
                    State.night2GuidanceStage=6;break;
                case 6:ShowNightRadio(5);State.night2GuidanceStage=7;break;
            }
        }
        void PlayChainSound()
        {
            if(chainSound==null||chainSound.clip==null)return;
            chainSound.transform.position=State.phase==5&&tank!=null?tank.transform.position:NightPatrolPosition+new Vector3(28,0,-18);chainSound.Play();
        }
        void CancelNightRadio()
        {
            liveRadio=null;liveRadioRemaining=hissRemaining=chainRemaining=0;previousNightAngle=180;nightAudioPaused=false;if(chainSound!=null)chainSound.Stop();if(nightHiss!=null)nightHiss.Stop();
        }
        void SyncNightAudioPause()
        {
            bool pause=Mission.Paused||Mission.Panel!=0||ForestMenu.Visible||Time.timeScale<=0;
            if(pause==nightAudioPaused)return;nightAudioPaused=pause;
            if(pause){if(chainSound!=null)chainSound.Pause();if(nightHiss!=null)nightHiss.Pause();performanceSound?.Pause();nightTheme?.Pause();}
            else{if(chainSound!=null)chainSound.UnPause();if(nightHiss!=null)nightHiss.UnPause();performanceSound?.UnPause();nightTheme?.UnPause();}
        }
        void DrawLiveNightRadio(GUIStyle label,GUIStyle title)
        {
            if(liveRadio==null||liveRadioRemaining<=0||sceneTask!=null)return;
            GUI.Box(new Rect(370,700,990,155),GUIContent.none);GUI.Label(new Rect(390,710,950,35),liveRadio.speaker+(liveRadio.radio?" · bộ đàm":""),title);GUI.Label(new Rect(390,752,950,90),liveRadio.text,label);
        }
    }
}
