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
        public Vector3 NightDoorStand=>sites["store"].position;
        public Vector3 NightDoorPosition=>NightDoorStand+Vector3.forward*1.5f;
        Transform nightLatch;
        AudioSource chainSound,nightHiss;
        float liveRadioRemaining,hissRemaining,chainRemaining;
        bool nightAudioPaused;
        Map02DialogueCatalog.Entry liveRadio;
        bool NightReturnPending=>State.night2GuidanceStage<7||liveRadioRemaining>0||chainRemaining>0;

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
            var hatch=new GameObject("Kho kín — cửa hầm và chốt kiểm tra");hatch.transform.SetParent(root.transform,false);hatch.transform.position=NightDoorPosition;
            var wood=new Material(Resources.Load<Material>("Story/Map02QuestWood")){name="Cửa hầm kho kín",color=new Color(.27f,.2f,.12f)};
            var metal=new Material(wood){name="Nẹp và chốt cửa hầm",color=new Color(.46f,.49f,.51f)};dayTwoMaterials.Add(wood);dayTwoMaterials.Add(metal);
            RoutePart("Nắp hầm đóng",PrimitiveType.Cube,hatch.transform,Vector3.up*.07f,new Vector3(1.6f,.12f,1.1f),wood,true);
            for(int i=-1;i<=1;i++)RoutePart("Nẹp cửa",PrimitiveType.Cube,hatch.transform,new Vector3(i*.55f,.145f,0),new Vector3(.045f,.03f,1.02f),metal);
            foreach(int side in new[]{-1,1}){RoutePart("Viền ngang cửa hầm",PrimitiveType.Cube,hatch.transform,new Vector3(0,.15f,side*.55f),new Vector3(1.7f,.045f,.055f),metal);RoutePart("Viền dọc cửa hầm",PrimitiveType.Cube,hatch.transform,new Vector3(side*.8f,.15f,0),new Vector3(.055f,.045f,1.15f),metal);}
            nightLatch=RoutePart("Chốt ngang còn kín",PrimitiveType.Cube,hatch.transform,new Vector3(0,.2f,-.33f),new Vector3(.55f,.065f,.1f),metal).transform;
            chainSound=new GameObject("Xích sắt rất xa — cue riêng").AddComponent<AudioSource>();chainSound.transform.SetParent(root.transform,false);chainSound.clip=Resources.Load<AudioClip>("OperationsAudio/night2_chain");chainSound.playOnAwake=false;chainSound.spatialBlend=.85f;chainSound.rolloffMode=AudioRolloffMode.Linear;chainSound.minDistance=12;chainSound.maxDistance=180;chainSound.volume=.65f;
            nightHiss=gameObject.AddComponent<AudioSource>();nightHiss.playOnAwake=false;nightHiss.clip=Resources.Load<AudioClip>("OperationsAudio/radio_off");nightHiss.volume=.08f;nightHiss.spatialBlend=0;
        }
        bool InteractNightRoute(Map02ChapterTask task)
        {
            if(task.kind=="patrolReport")
            {
                State.night2GuidanceStage=1;Finish(task);ShowNightRadio(0);return true;
            }
            if(task.kind=="inspectDoor")
            {
                var look=Vector3.ProjectOnPlane(rig.transform.forward,Vector3.up).normalized;
                var toward=Vector3.ProjectOnPlane(NightDoorPosition-Mission.Player.position,Vector3.up).normalized;
                if(Vector3.Distance(Mission.Player.position,NightDoorStand)>1.8f||Vector3.Dot(look,toward)<.25f){Mission.Say("Đứng trước nắp hầm, nhìn vào chốt rồi nhấn E kiểm tra.");return false;}
                var from=Mission.Player.position+Vector3.up;var to=NightDoorPosition+Vector3.up*.2f;
                if(Physics.Linecast(from,to,out var hit,~0,QueryTriggerInteraction.Ignore)&&!hit.transform.IsChildOf(nightLatch.parent)){Mission.Say("Tới đúng mặt cửa để kiểm tra chốt.");return false;}
                nightLatch.localRotation=Quaternion.Euler(0,0,8);AudioSource.PlayClipAtPoint(Resources.Load<AudioClip>("OperationsAudio/lock_turn"),NightDoorPosition,.35f);
                Mission.Say("Chốt còn chắc, cửa hầm vẫn kín. Kho vật tư an toàn.");Finish(task);return true;
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
