using System;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace ShadowVale.Map01
{
    public sealed partial class Map02ChapterDirector
    {
        public const string EvacuationPatrolGroup="night2_evacuation_patrol";
        public const float EvacuationLightRange=10,EvacuationLightAngle=60,EvacuationObservationSeconds=1;
        public const float EvacuationPatrolHalfLength=10,EvacuationPatrolPauseSeconds=3,EvacuationPatrolWalkSpeed=1.6f;
        public const int EvacuationSideGuard=1;
        static readonly string[] EvacuationNames={"Chọn hướng kiểm tra","Kiểm tra đường ruộng","Kiểm tra đường qua sông","Kiểm tra đường từ trong làng"};
        static readonly string[] EvacuationScenes={"","evac_field","evac_river","evac_village"};
        Map02DialogueCatalog.Entry[] EvacuationReport=>Map02ChapterDefinition.Load().Lines(EvacuationScenes[evacuationReportChoice]);
        readonly Vector3[] evacuationPoints=new Vector3[4];
        readonly Vector3[] evacuationMarchStart=new Vector3[5],evacuationMarchEnd=new Vector3[5],evacuationMarchNegative=new Vector3[5];
        readonly Light[] evacuationLights=new Light[5];
        readonly Mesh[] evacuationLightMeshes=new Mesh[5];
        readonly GameObject[] evacuationLightBeams=new GameObject[5];
        Material evacuationBeamMaterial;
        Map02SightCover evacuationSightCover;
        bool evacuationMenu,evacuationConfigured;
        GameObject evacuationBush;
        float evacuationObserved,evacuationLineClock;
        int evacuationReportChoice,evacuationLine;
        bool EvacuationRadioActive=>evacuationReportChoice!=0;
        bool EvacuationModal=>evacuationMenu||EvacuationRadioActive;
        public bool EvacuationChoicesActive=>State.Available(Map02ChapterDefinition.Load().Find("N2_04"));
        public Vector3 EvacuationChoicePoint(int choice)=>evacuationPoints[Mathf.Clamp(choice,0,3)];
        public bool EvacuationMenuVisible=>evacuationMenu;
        public string EvacuationInstruction
        {
            get
            {
                if(UndergroundStore?.Inside==true)return "Lên khỏi hầm rồi tới điểm kiểm tra gần kho để chọn một trong ba hướng.";
                if(!State.night2EvacuationStarted)return "Tới ghim gần hầm kho để bắt đầu kiểm tra đường sơ tán.";
                if(evacuationMenu)return "Chọn hướng bằng chuột hoặc phím 1 / 2 / 3. Chỉ cần tìm một đường sơ tán phù hợp.";
                if(State.night2EvacuationChoice==0)return "Về điểm kiểm tra gần kho, nhấn E để chọn hướng.";
                if(State.night2EvacuationChoice==1)return State.night2FieldObserved?"Đã phát hiện địch. Trong bụi cây ở ghim, nhấn E báo cáo chỉ huy. Z nằm tránh đèn; N chọn hướng khác.":$"Trong bụi cây: đứng hoặc ngồi, giữ yên F quan sát 1 giây ({Mathf.Clamp01(evacuationObserved)*100:0}%). Một lính quét bên hông khi đi. Z nằm tránh đèn, nằm không quan sát được. N chọn hướng khác.";
                return State.night2EvacuationChoice==2?"Tới ghim bên bờ sông, nhấn E báo cáo đường phải lội nước. N chọn hướng khác.":"Tới ghim đường trong làng, nhấn E báo cáo kết quả kiểm tra. N chọn hướng khác.";
            }
        }
        public string EvacuationPrompt=>State.night2EvacuationChoice==0?"Chọn hướng kiểm tra đường sơ tán":
            State.night2EvacuationChoice==1&&!State.night2FieldObserved?"Nấp trong bụi cây · giữ F quan sát 1 giây":"Báo cáo chỉ huy qua bộ đàm";
        Vector3 EvacuationMarker=>evacuationConfigured?EvacuationChoicePoint(State.night2EvacuationChoice):Layout.nightEvacuationStart;

        bool TryEvacuationFloor(Vector3 wanted,out Vector3 point)
        {
            point=wanted;
            if(!NavMesh.SamplePosition(wanted,out var hit,2,NavMesh.AllAreas))return false;
            point=hit.position;return true;
        }
        void BuildEvacuationChoices()
        {
            var wanted=new[]{Layout.nightEvacuationStart,Layout.nightEvacuationFieldBush,Layout.nightEvacuationRiver,Layout.nightEvacuationVillage};
            evacuationConfigured=true;
            for(int i=0;i<4;i++)if(!TryEvacuationFloor(wanted[i],out evacuationPoints[i]))evacuationConfigured=false;
            var direction=Vector3.ProjectOnPlane(Layout.nightEvacuationPatrolEnd-Layout.nightEvacuationPatrolStart,Vector3.up).normalized;
            var side=Vector3.Cross(Vector3.up,direction);
            var center=(Layout.nightEvacuationPatrolStart+Layout.nightEvacuationPatrolEnd)*.5f;
            // Keep both pairs inside the 20 m segment even when the formation reverses.
            for(int i=0;i<5;i++)
            {
                float row=i==0?0:1.3f*((i+1)/2),lateral=i==0?0:i%2==1?-.6f:.6f;
                if(!TryEvacuationFloor(center-direction*row+side*lateral,out evacuationMarchStart[i])||
                    !TryEvacuationFloor(center+direction*(EvacuationPatrolHalfLength-row)+side*lateral,out evacuationMarchEnd[i])||
                    !TryEvacuationFloor(center-direction*(EvacuationPatrolHalfLength-row)+side*lateral,out evacuationMarchNegative[i])){evacuationConfigured=false;continue;}
                var path=new NavMeshPath();
                if(!NavMesh.CalculatePath(evacuationMarchStart[i],evacuationMarchEnd[i],NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete||
                    !NavMesh.CalculatePath(evacuationMarchEnd[i],evacuationMarchNegative[i],NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)evacuationConfigured=false;
            }
            if(!evacuationConfigured){Debug.LogError("Night 2 evacuation choices need valid NavMesh points and a complete field patrol path.");return;}
            if(root.GetComponent<Map02EvacuationLightSync>()==null)root.AddComponent<Map02EvacuationLightSync>().owner=this;
            if(Layout.nightEvacuationBushArt!=null)
            {
                evacuationBush=Instantiate(Layout.nightEvacuationBushArt,root.transform);
                evacuationBush.name="Bụi cây quan sát đường ruộng";evacuationBush.transform.position=evacuationPoints[1];
                // This foliage is concealment, not a solid wall around Nam or an opaque binocular screen.
                foreach(var collider in evacuationBush.GetComponentsInChildren<Collider>(true))collider.enabled=false;
                evacuationBush.SetActive(false);
            }
        }
        void OpenEvacuationMenu()
        {
            evacuationMenu=true;player.RestoreMotion(false);SetBinoculars(false);
            Cursor.visible=true;Cursor.lockState=CursorLockMode.None;
        }
        public bool SelectEvacuationChoice(int choice)
        {
            if(!evacuationMenu||!State.SelectEvacuationRoute(choice))return false;
            evacuationMenu=false;evacuationObserved=0;CloseEvacuationCursor();UpdateMarkers();Mission.RequireInteractionRelease();Mission.StoreCheckpoint();return true;
        }
        void CloseEvacuationCursor()
        {
            Cursor.visible=Mission.Paused||Mission.Panel!=0;
            Cursor.lockState=Cursor.visible?CursorLockMode.None:CursorLockMode.Locked;
        }
        bool InteractEvacuationChoice(Map02ChapterTask task)
        {
            if(!evacuationConfigured){Mission.Say("Tuyến kiểm tra chưa được bố trí trên NavMesh.");return false;}
            if(UndergroundStore?.Inside==true){Mission.Say("Tới chân thang, Space bám thang rồi W lên mặt đất trước khi chọn hướng khảo sát.");return false;}
            var point=EvacuationChoicePoint(State.night2EvacuationChoice);
            if(Vector3.Distance(Mission.Player.position,point)>3)return false;
            if(State.night2EvacuationChoice==0){State.night2EvacuationStarted=true;OpenEvacuationMenu();return true;}
            if(State.night2EvacuationChoice==1&&Vector3.ProjectOnPlane(Mission.Player.position-evacuationPoints[1],Vector3.up).sqrMagnitude>2.25f)
            {Mission.Say("Trở vào bụi cây ở ghim để báo cáo an toàn.");return false;}
            if(!State.CanReportEvacuationRoute){Mission.Say("Đứng trong bụi cây, giữ F và nhìn rõ toán lính đủ 1 giây trước khi báo cáo.");return false;}
            evacuationReportChoice=State.night2EvacuationChoice;evacuationLine=0;evacuationLineClock=0;
            CancelNightRadio();voice?.Stop();DropBarks();SetBinoculars(false);player.RestoreMotion(false);Mission.RequireInteractionRelease();SpeakEvacuationLine();return true;
        }
        void SpeakEvacuationLine()
        {
            voice?.Stop();var line=EvacuationReport[evacuationLine];
            if(Map02DialogueCatalog.HasApprovedRecording(line))voice?.Play(line.RecordingId);
        }
        void TickEvacuationModal(float dt)
        {
            var keyboard=Keyboard.current;
            if(evacuationMenu)
            {
                Cursor.visible=true;Cursor.lockState=CursorLockMode.None;
                if(keyboard?.digit1Key.wasPressedThisFrame==true)SelectEvacuationChoice(1);
                else if(keyboard?.digit2Key.wasPressedThisFrame==true)SelectEvacuationChoice(2);
                else if(keyboard?.digit3Key.wasPressedThisFrame==true)SelectEvacuationChoice(3);
                return;
            }
            if(!EvacuationRadioActive)return;
            evacuationLineClock+=dt;
            var report=EvacuationReport;
            float duration=Mathf.Max(2,report[evacuationLine].text.Length/13f);
            bool advance=evacuationLineClock>.35f&&keyboard?.eKey.wasPressedThisFrame==true;
            if(!advance&&(evacuationLineClock<duration||voice?.HasSpeech==true))return;
            voice?.Stop();
            evacuationLineClock=0;evacuationLine++;
            if(evacuationLine<report.Length){SpeakEvacuationLine();return;}
            var task=Map02ChapterDefinition.Load().Find("N2_04");
            int choice=evacuationReportChoice;evacuationReportChoice=0;
            if(!State.ResolveEvacuationReport())return;
            Mission.RequireInteractionRelease();
            if(choice==3)
            {
                // State resolved the task only after the commander's complete response.
                State.night2GuidanceStage=7;State.night2ChainCuePlayed=true;CommitFinishedTask(task);
            }
            else
            {
                UpdateMarkers();Mission.StoreCheckpoint();OpenEvacuationMenu();
            }
        }
        void TickEvacuationChoices(float dt)
        {
            if(!evacuationConfigured||!EvacuationChoicesActive)return;
            if(State.night2EvacuationStarted&&Keyboard.current?.nKey.wasPressedThisFrame==true&&UndergroundStore?.Inside!=true)
            {OpenEvacuationMenu();return;}
            if(!State.night2EvacuationStarted&&UndergroundStore?.Inside!=true&&Vector3.Distance(Mission.Player.position,evacuationPoints[0])<2.5f)
            {
                State.night2EvacuationStarted=true;Mission.StoreCheckpoint();OpenEvacuationMenu();return;
            }
            if(State.night2EvacuationStarted&&State.night2EvacuationChoice==0&&!evacuationMenu){OpenEvacuationMenu();return;}
            SyncEvacuationPatrol();
            TickEvacuationFieldPatrol(dt);
            if(State.night2EvacuationChoice!=1||State.night2FieldObserved)return;
            if(ObserveEvacuationPatrol(dt))
            {
                State.night2FieldObserved=true;UpdateMarkers();Mission.Say("Đã phát hiện toán địch. Nhấn E trong bụi cây để báo cáo chỉ huy.");Mission.StoreCheckpoint();
            }
        }
        bool ObserveEvacuationPatrol(float dt)
        {
            var cam=rig.GetComponent<Camera>();
            bool bush=Vector3.ProjectOnPlane(Mission.Player.position-evacuationPoints[1],Vector3.up).sqrMagnitude<=2.25f&&
                Mathf.Abs(Mission.Player.position.y-evacuationPoints[1].y)<1.5f;
            bool visible=bush&&binocular&&!player.IsMoving&&!player.IsProne&&Enemies.Any(unit=>unit!=null&&unit.group==EvacuationPatrolGroup&&!unit.Dead&&
                Vector3.Distance(cam.transform.position,unit.transform.position)<65&&
                Vector3.Angle(cam.transform.forward,unit.transform.position+Vector3.up-cam.transform.position)<Mathf.Min(25,cam.fieldOfView*.5f)&&
                EvacuationObservationVisible(cam,unit));
            evacuationObserved=visible?evacuationObserved+dt:0;
            return evacuationObserved>=EvacuationObservationSeconds;
        }
        bool EvacuationObservationVisible(Camera cam,Map02ChapterEnemy unit)
        {
            var delta=unit.transform.position+Vector3.up*1.3f-cam.transform.position;
            var hit=Physics.RaycastAll(cam.transform.position,delta.normalized,delta.magnitude+.2f,~0,QueryTriggerInteraction.Ignore)
                .Where(h=>h.transform!=Mission.Player&&!h.transform.IsChildOf(Mission.Player)).OrderBy(h=>h.distance).FirstOrDefault();
            return hit.collider!=null&&(hit.transform==unit.transform||hit.transform.IsChildOf(unit.transform));
        }
        void SyncEvacuationPatrol()
        {
            bool active=EvacuationChoicesActive&&evacuationConfigured;
            if(evacuationBush!=null)evacuationBush.SetActive(active);
            if(active)
            {
                for(int i=0;i<5;i++)
                {
                    string id=EvacuationPatrolGroup+"_"+i;
                    if(!Enemies.Any(e=>e!=null&&e.id==id))SpawnEnemy(id,EvacuationPatrolGroup,evacuationMarchStart[i],100,false);
                }
                if(State.night2EvacuationPatrolVersion==0)
                {
                    State.night2EvacuationPatrolVersion=1;State.night2EvacuationPatrolTriggered=false;
                    State.night2EvacuationPatrolAtEnd=false;State.night2EvacuationPatrolClock=0;State.night2EvacuationPatrolDirection=1;
                    State.night2EvacuationSideSweepSeconds=0;
                    foreach(var unit in Enemies.Where(e=>e!=null&&e.group==EvacuationPatrolGroup&&!e.Dead))
                    {
                        var agent=unit.GetComponent<NavMeshAgent>();int i=unit.id[unit.id.Length-1]-'0';
                        if(agent!=null&&agent.enabled){agent.Warp(evacuationMarchStart[i]);if(agent.isOnNavMesh){agent.ResetPath();agent.isStopped=true;}}
                    }
                }
            }
            foreach(var unit in Enemies.Where(e=>e!=null&&e.group==EvacuationPatrolGroup))
            {
                unit.gameObject.SetActive(active);
                if(active)Map02PatrolFlashlight.Bind(unit);
                if(unit.Dead)foreach(var light in unit.GetComponentsInChildren<Light>())light.enabled=false;
            }
        }
        Vector3 EvacuationPatrolDestination(int i)=>State.night2EvacuationPatrolDirection>0?evacuationMarchEnd[i]:evacuationMarchNegative[i];
        void TickEvacuationFieldPatrol(float dt)
        {
            if(EvacuationCaptureActive)return;
            if(!State.night2EvacuationPatrolTriggered)
            {
                if(State.night2EvacuationChoice!=1||Vector3.Distance(Mission.Player.position,evacuationPoints[1])>3)return;
                State.night2EvacuationPatrolTriggered=true;State.night2EvacuationPatrolDirection=1;
                State.night2EvacuationPatrolAtEnd=false;State.night2EvacuationPatrolClock=0;
                Mission.StoreCheckpoint();
            }
            State.night2EvacuationSideSweepSeconds=(State.night2EvacuationSideSweepSeconds+dt)%6;
            if(State.night2EvacuationPatrolAtEnd)
            {
                State.night2EvacuationPatrolClock=Mathf.Min(EvacuationPatrolPauseSeconds,State.night2EvacuationPatrolClock+dt);
                if(State.night2EvacuationPatrolClock<EvacuationPatrolPauseSeconds)return;
                State.night2EvacuationPatrolDirection=-State.night2EvacuationPatrolDirection;
                State.night2EvacuationPatrolAtEnd=false;State.night2EvacuationPatrolClock=0;
                return;
            }
            var alive=Enemies.Where(e=>e!=null&&e.group==EvacuationPatrolGroup&&!e.Dead).ToArray();
            if(alive.Length==0)return;
            if(alive.All(e=>Vector3.Distance(e.transform.position,EvacuationPatrolDestination(e.id[e.id.Length-1]-'0'))<.35f))
            {State.night2EvacuationPatrolAtEnd=true;State.night2EvacuationPatrolClock=0;}
        }
        Vector3 EvacuationFacing(int i)
        {
            var march=(Layout.nightEvacuationPatrolEnd-Layout.nightEvacuationPatrolStart).normalized*State.night2EvacuationPatrolDirection;
            if(!State.night2EvacuationPatrolAtEnd)
            {
                if(i!=EvacuationSideGuard)return march;
                // Sweep the side of the road containing the observation bush, not a tracking lock on Nam.
                var axis=(Layout.nightEvacuationPatrolEnd-Layout.nightEvacuationPatrolStart).normalized;
                var side=Vector3.Cross(Vector3.up,axis);
                if(Vector3.Dot(side,evacuationPoints[1]-(Layout.nightEvacuationPatrolStart+Layout.nightEvacuationPatrolEnd)*.5f)<0)side=-side;
                return Quaternion.AngleAxis(Mathf.Sin(State.night2EvacuationSideSweepSeconds*Mathf.PI/3)*25,Vector3.up)*side;
            }
            // Five observers face different sectors; each flashlight has a full 60-degree cone.
            return Quaternion.AngleAxis(i*72+Mathf.Sin(State.night2EvacuationPatrolClock*Mathf.PI*2/EvacuationPatrolPauseSeconds)*30,Vector3.up)*march;
        }
        public bool ControlsEvacuationUnit(Map02ChapterEnemy unit,NavMeshAgent agent)
        {
            if(unit.group!=EvacuationPatrolGroup)return false;
            if(!EvacuationChoicesActive||!evacuationConfigured){if(agent.isOnNavMesh){agent.ResetPath();agent.isStopped=true;}return true;}
            int i=unit.id[unit.id.Length-1]-'0';if(i<0||i>=5)return true;
            if(State.night2EvacuationPatrolTriggered&&!State.night2EvacuationPatrolAtEnd)
            {
                agent.speed=EvacuationPatrolWalkSpeed;agent.acceleration=8;agent.stoppingDistance=.1f;agent.autoBraking=true;
                var destination=EvacuationPatrolDestination(i);
                if(Vector3.Distance(unit.transform.position,destination)>.2f)
                {agent.isStopped=false;if(!agent.pathPending&&(!agent.hasPath||(agent.destination-destination).sqrMagnitude>.01f))agent.SetDestination(destination);}
                else{agent.ResetPath();agent.isStopped=true;}
            }
            else{agent.ResetPath();agent.isStopped=true;}
            var direction=EvacuationFacing(i);
            var bodyDirection=State.night2EvacuationPatrolAtEnd?direction:
                (Layout.nightEvacuationPatrolEnd-Layout.nightEvacuationPatrolStart).normalized*State.night2EvacuationPatrolDirection;
            unit.transform.rotation=Quaternion.RotateTowards(unit.transform.rotation,Quaternion.LookRotation(bodyDirection),120*Time.deltaTime);
            var prop=unit.GetComponent<Map02PatrolFlashlight>();prop?.SetDirection(direction);
            if(evacuationLights[i]==null)
            {
                var go=new GameObject("Đèn tuần tra đường ruộng · 60° / 10 m");go.transform.SetParent(unit.transform);
                var light=go.AddComponent<Light>();light.type=LightType.Spot;light.range=EvacuationLightRange;light.spotAngle=EvacuationLightAngle;
                light.intensity=6;light.color=new Color(.85f,.9f,1);light.shadows=LightShadows.Soft;evacuationLights[i]=light;
                BuildEvacuationLightBeam(i,light.transform);
            }
            // Presentation and detection share the final hand-held emitter in LateUpdate.
            evacuationLights[i].enabled=false;
            return true;
        }
        public void SyncEvacuationPatrolLights()
        {
            for(int i=0;i<5;i++)
            {
                var light=evacuationLights[i];if(light==null)continue;
                var unit=Enemies.FirstOrDefault(e=>e!=null&&e.id==EvacuationPatrolGroup+"_"+i);
                var agent=unit!=null?unit.GetComponent<NavMeshAgent>():null;
                bool live=!EvacuationCaptureActive&&EvacuationChoicesActive&&State.night2EvacuationPatrolTriggered&&unit!=null&&!unit.Dead&&unit.gameObject.activeInHierarchy&&agent!=null&&agent.enabled&&agent.isOnNavMesh;
                bool scanning=live&&(State.night2EvacuationPatrolAtEnd?agent.isStopped&&Vector3.ProjectOnPlane(agent.velocity,Vector3.up).sqrMagnitude<.0025f:
                    i==EvacuationSideGuard&&!agent.isStopped);
                light.enabled=scanning;
                if(evacuationLightBeams[i]!=null)evacuationLightBeams[i].SetActive(scanning);
                if(!scanning)continue;
                var emitter=unit.GetComponent<Map02PatrolFlashlight>()?.Emitter;
                var from=emitter!=null?emitter.position:unit.transform.position+Vector3.up*1.5f;
                var direction=emitter!=null?emitter.forward:unit.transform.forward;
                light.transform.SetPositionAndRotation(from,Quaternion.LookRotation(direction));
                if(Mission.Ready&&!Mission.Paused&&!ForestMenu.Visible&&!EvacuationModal&&sceneTask==null&&Mission.Panel==0&&!Mission.World.story.failed&&
                    EvacuationBeamSeesPlayer(unit,light.transform.position,light.transform.forward))
                {
                    BeginEvacuationCapture();break;
                }
            }
        }
        void BuildEvacuationLightBeam(int index,Transform lamp)
        {
            if(evacuationLightMeshes[index]!=null)Destroy(evacuationLightMeshes[index]);
            var shader=Resources.Load<Shader>("Effects/Map02PatrolBeam");if(shader==null)return;
            if(evacuationBeamMaterial==null)evacuationBeamMaterial=new Material(shader){name="Đường ruộng · luồng sáng quét 60° / 10 m"};
            const int sides=24;var vertices=new Vector3[sides+1];var colors=new Color[sides+1];var triangles=new int[sides*3];
            colors[0]=new Color(.85f,.9f,1,.08f);vertices[0]=Vector3.forward*.08f;
            float radius=Mathf.Tan(EvacuationLightAngle*.5f*Mathf.Deg2Rad)*EvacuationLightRange;
            for(int i=0;i<sides;i++)
            {
                float angle=i*Mathf.PI*2/sides;vertices[i+1]=new Vector3(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius,EvacuationLightRange);
                colors[i+1]=new Color(.85f,.9f,1,.012f);triangles[i*3]=0;triangles[i*3+1]=i+1;triangles[i*3+2]=(i+1)%sides+1;
            }
            var mesh=new Mesh{name="Evacuation visible beam "+index};mesh.vertices=vertices;mesh.colors=colors;mesh.triangles=triangles;mesh.RecalculateBounds();evacuationLightMeshes[index]=mesh;
            var beam=new GameObject("Luồng đèn quét nhìn thấy được");beam.transform.SetParent(lamp,false);beam.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=beam.AddComponent<MeshRenderer>();renderer.sharedMaterial=evacuationBeamMaterial;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
            beam.SetActive(false);evacuationLightBeams[index]=beam;
        }
        void ReleaseEvacuationLightPresentation()
        {
            foreach(var mesh in evacuationLightMeshes)if(mesh!=null)Destroy(mesh);
            if(evacuationBeamMaterial!=null)Destroy(evacuationBeamMaterial);
        }
        public static bool InEvacuationLightCone(Vector3 from,Vector3 direction,Vector3 target)=>
            (target-from).sqrMagnitude<=EvacuationLightRange*EvacuationLightRange&&Vector3.Angle(direction,target-from)<=EvacuationLightAngle*.5f;
        bool EvacuationBeamSeesPlayer(Map02ChapterEnemy unit,Vector3 from,Vector3 direction)
        {
            if(player.IsProne)return false;
            var target=Mission.Player.position+Vector3.up;
            if(!InEvacuationLightCone(from,direction,target)||Map01Smoke.Blocks(from,target))return false;
            if(evacuationSightCover==null)evacuationSightCover=Mission.GetComponent<Map02SightCover>();
            if(evacuationSightCover!=null&&evacuationSightCover.Blocks(from,target))return false;
            var delta=target-from;
            var hit=Physics.RaycastAll(from,delta.normalized,delta.magnitude+.15f,~0,QueryTriggerInteraction.Ignore)
                .Where(h=>!h.transform.IsChildOf(unit.transform)).OrderBy(h=>h.distance).FirstOrDefault();
            return hit.collider!=null&&(hit.transform==Mission.Player||hit.transform.IsChildOf(Mission.Player));
        }
        void CancelEvacuationChoices()
        {
            bool cursor=evacuationMenu;evacuationMenu=false;evacuationReportChoice=0;evacuationObserved=evacuationLineClock=0;
            if(cursor&&Mission!=null)CloseEvacuationCursor();
        }
        void DrawEvacuationChoices(GUIStyle label,GUIStyle title)
        {
            if(evacuationMenu)
            {
                GUI.Box(new Rect(380,225,840,415),GUIContent.none);
                GUI.Label(new Rect(410,245,780,50),"KIỂM TRA ĐƯỜNG SƠ TÁN",title);
                GUI.Label(new Rect(410,300,780,65),"Chọn một hướng để kiểm tra. Báo cáo qua bộ đàm khi tới nơi.",label);
                for(int i=1;i<=3;i++)
                {
                    bool rejected=(State.night2RejectedRoutes&(1<<(i-1)))!=0;
                    GUI.enabled=!rejected;
                    if(GUI.Button(new Rect(415,380+(i-1)*75,770,58),i+" · "+EvacuationNames[i]+(rejected?" · Đã loại":""),new GUIStyle(GUI.skin.button){fontSize=23}))SelectEvacuationChoice(i);
                    GUI.enabled=true;
                }
            }
            if(EvacuationRadioActive)
            {
                GUI.Box(new Rect(220,690,1160,185),GUIContent.none);
                var line=EvacuationReport[evacuationLine];
                GUI.Label(new Rect(245,705,1110,38),line.speaker+" · bộ đàm",title);
                GUI.Label(new Rect(245,748,1110,85),line.text,label);
                GUI.Label(new Rect(245,835,1110,30),"E · tiếp tục",label);
            }
        }
    }
}
