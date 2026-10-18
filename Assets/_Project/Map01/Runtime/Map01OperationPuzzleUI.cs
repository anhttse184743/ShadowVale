using System;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace ShadowVale.Map01
{
    public sealed class Map01OperationPuzzleUI : MonoBehaviour
    {
        public bool Active=>session!=null;
        public bool IsRadio {get;private set;}
        public bool IsPower=>Power!=null;
        public Map01PowerSabotage Power {get;private set;}
        public Map01SupplyLock Lock {get;private set;}
        public Map01RadioCircuit Circuit {get;private set;}
        Map01Mission mission;
        Map01OperationSession session;
        Action<Map01OperationResult> completed;
        bool instructions,paused,roundReady;
        public bool AwaitingNextRound=>roundReady;
        string interactionCheckpoint;
        int selected,code;
        float elapsed;
        Vector3 focus;
        AnimationClip panelPose,walkPose;
        Vector3[] approachPath;
        int approachCorner;
        float walkTime,poseTime;
        Quaternion panelFacing;
        Transform rightFinger,leftFinger;
        bool startRequested;
        float failureDelay;
        string failureMessage;
        Vector2 historyScroll;
        bool Approaching=>(IsRadio||IsPower)&&approachPath!=null&&approachCorner<approachPath.Length;
        public bool Preparing=>Approaching;
        GUIStyle title,body,small,digit,button;
        public bool Begin(Map01Mission owner,Map01SupplyLock puzzle,Map01RadioCircuit circuit,Vector3 point,Action<Map01OperationResult> done,Map01PowerSabotage power=null)
        {
            if(Active||owner.Stopped)return false;
            mission=owner;Lock=puzzle;Circuit=circuit;IsRadio=circuit!=null;Power=power;focus=point;
            approachPath=null;approachCorner=0;walkTime=poseTime=0;startRequested=false;panelPose=null;failureDelay=0;failureMessage=null;historyScroll=Vector2.zero;
            if(IsRadio||IsPower){
                var layout=owner.GetComponent<Map01Story>().Layout;var stand=IsPower?layout.sabotage.powerApproach:layout.radioApproach;var route=new NavMeshPath();
                if(!NavMesh.CalculatePath(owner.player.position,stand.position,NavMesh.AllAreas,route)||route.status!=NavMeshPathStatus.PathComplete){owner.Say("Không có đường tới mặt đài. Đứng gần vị trí thao tác rồi thử lại.",4);return false;}
                approachPath=route.corners;panelFacing=stand.rotation;panelPose=Resources.Load<AnimationClip>("OperationsAnimation/Nam_RadioPanel");
                while(approachCorner<approachPath.Length&&Vector3.Distance(owner.player.position,approachPath[approachCorner])<.035f)approachCorner++;
                var controller=owner.player.GetComponentInChildren<Animator>().runtimeAnimatorController;
                walkPose=controller.animationClips.FirstOrDefault(c=>c.name=="Nam_Walk_F")??controller.animationClips.FirstOrDefault(c=>c.name.Contains("Walk")&&!c.name.Contains("Crouch"));
                var nodes=owner.player.GetComponentInChildren<Animator>().GetComponentsInChildren<Transform>();rightFinger=nodes.FirstOrDefault(t=>t.name=="RightHandIndex3");leftFinger=nodes.FirstOrDefault(t=>t.name=="LeftHandMiddle1");
            }
            interactionCheckpoint=owner.GetComponent<Map01SaveSystem>().CaptureInteractionCheckpoint();
            completed=done;instructions=true;paused=false;roundReady=false;selected=code=0;elapsed=0;session=new Map01OperationSession(owner);return true;
        }
        public void StartPuzzle(){startRequested=true;roundReady=false;if(!Approaching)instructions=false;}
        public void TogglePause(){if(!Active)return;paused=!paused;mission.SetPaused(paused);}
        void Update()
        {
            if(!Active)return;
            if(Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame)TogglePause();
            if(paused||mission.Paused||ForestMenu.Visible)return;
            elapsed+=Time.deltaTime;
            if(failureMessage!=null){failureDelay-=Time.deltaTime;if(failureDelay<=0)Finish(Map01OperationResult.Failure);return;}
            if(Approaching){
                var goal=approachPath[approachCorner];goal.y=mission.player.position.y;
                var next=Map01OperationSession.GroundPoint(Vector3.MoveTowards(mission.player.position,goal,1.25f*Time.deltaTime),mission.player);var direction=Vector3.ProjectOnPlane(next-mission.player.position,Vector3.up);
                if(direction.sqrMagnitude>.00001f)mission.player.rotation=Quaternion.Slerp(mission.player.rotation,Quaternion.LookRotation(direction),Time.deltaTime*8);
                mission.player.position=next;walkTime+=Time.deltaTime*1.25f/1.8f;
                if(Vector3.ProjectOnPlane(next-approachPath[approachCorner],Vector3.up).magnitude<.035f)approachCorner++;
                return;
            }
            poseTime+=Time.deltaTime;
            if(startRequested)instructions=false;
            if(instructions||roundReady)return;
            if(IsPower){
                Power.Tick(Time.deltaTime);
                if(Power.Complete&&!Power.Won)FailCircuit("QUÁ THỜI GIAN. Chưa ngắt được nguồn; có thể thử lại.");
                else if(Keyboard.current!=null&&Keyboard.current.spaceKey.wasPressedThisFrame)SubmitPower();
            }
            else if(IsRadio){Circuit.Tick(Time.deltaTime);if(Circuit.Remaining<=0)FailCircuit("HẾT THỜI GIAN. Dây chưa nối xong; đài đã phát báo động.");}
            else {
                var kb=Keyboard.current;if(kb==null)return;
                if(kb.aKey.wasPressedThisFrame)selected=(selected+2)%3;
                if(kb.dKey.wasPressedThisFrame)selected=(selected+1)%3;
                if(kb.wKey.wasPressedThisFrame)Change(1);if(kb.sKey.wasPressedThisFrame)Change(-1);
                if(kb.enterKey.wasPressedThisFrame||kb.numpadEnterKey.wasPressedThisFrame)SubmitLock(code);
            }
        }
        void LateUpdate(){
            if(!Active)return;
            if(Approaching){session.Sample(walkPose,walkTime,focus,elapsed/.6f);return;}
            if(IsRadio||IsPower)mission.player.rotation=Quaternion.Slerp(mission.player.rotation,panelFacing,Time.deltaTime*6);
            session.Sample(panelPose,poseTime,focus,elapsed/.6f);
            if((IsRadio||IsPower)&&panelPose!=null){
                var layout=mission.GetComponent<Map01Story>().Layout;var actor=session.Actor;
                if(IsPower){var position=layout.sabotage.powerRightContact.localPosition;position.x=-.16f+Mathf.Min(2,Power.Step)*.16f;layout.sabotage.powerRightContact.localPosition=position;}
                var rightContact=IsPower?layout.sabotage.powerRightContact:layout.radioRightContact;var leftContact=IsPower?layout.sabotage.powerLeftContact:layout.radioLeftContact;var chest=actor.GetBoneTransform(HumanBodyBones.Chest);float contact=Mathf.SmoothStep(0,1,poseTime/.8f);
                // The field set is on a low timber desk. Lean from the spine before reaching;
                // moving the character closer would put his capsule through the solid table.
                var spine=actor.GetBoneTransform(HumanBodyBones.Spine);
                if(spine!=null)spine.rotation=Quaternion.AngleAxis(30*contact,mission.player.right)*spine.rotation;
                var rightHand=actor.GetBoneTransform(HumanBodyBones.RightHand);var leftHand=actor.GetBoneTransform(HumanBodyBones.LeftHand);
                for(int pass=0;pass<2;pass++){
                    if(chest!=null&&rightFinger!=null&&rightContact!=null)Map01ArmContact.Reach(actor,false,rightContact.position-(rightFinger.position-rightHand.position),chest.position+mission.player.right*.45f+mission.player.forward*.25f-Vector3.up*.15f,contact);
                    if(chest!=null&&leftFinger!=null&&leftContact!=null)Map01ArmContact.Reach(actor,true,leftContact.position-(Vector3.Lerp(leftHand.position,leftFinger.position,.6f)-leftHand.position),chest.position-mission.player.right*.45f+mission.player.forward*.25f-Vector3.up*.15f,contact);
                }
            }
        }
        void Change(int delta){var digits=Map01SupplyLock.Digits(code);digits[selected]=(digits[selected]+delta+6)%6;code=Map01SupplyLock.Code(digits[0],digits[1],digits[2]);Map01OperationAudio.Play("lock_turn",focus,.22f);}
        public void SubmitLock(int guess)
        {
            if(!Active||IsRadio||IsPower||instructions||Lock.Remaining<=0)return;
            bool won=Lock.Submit(guess);historyScroll.y=float.MaxValue;if(won)Finish(Map01OperationResult.Success);
        }
        public void SubmitPower(){
            if(!Active||!IsPower||instructions||roundReady||failureMessage!=null)return;
            if(!Power.Pull()){FailCircuit("NGẮT SAI NHỊP. Cầu chì vẫn còn điện. Thử lại sau khi Nam rời bảng.");return;}
            Map01OperationAudio.Play("lock_turn",focus,.45f);
            if(Power.Won)Finish(Map01OperationResult.Success);else roundReady=true;
        }
        public void SubmitCircuit(){
            if(!Active||!IsRadio||instructions||roundReady||failureMessage!=null)return;
            if(Circuit.Remaining<=0){FailCircuit("HẾT 10 GIÂY. Đài đã phát báo động.");return;}
            if(Circuit.Solved()){
                if(Circuit.AdvanceRound()){roundReady=true;Map01OperationAudio.Play("lock_turn",focus,.3f);}
                else Finish(Map01OperationResult.Success);
            }
            else FailCircuit($"ĐƯỜNG CÒN ĐỨT: mới nối {Circuit.ConnectedCount}/20 đoạn. Xác nhận sai khiến đài phát báo động.");
        }
        void FailCircuit(string reason){failureMessage=reason;failureDelay=2.5f;}
        public void Finish(Map01OperationResult result)
        {
            if(!Active)return;if(paused){paused=false;mission.SetPaused(false);}var done=completed;completed=null;session.Dispose();session=null;done?.Invoke(result);
        }
        void OnDisable(){Finish(Map01OperationResult.Cancelled);}
        void OnGUI()
        {
            if(!Active||ForestMenu.Visible)return;
            title??=new GUIStyle(GUI.skin.label){fontSize=26,fontStyle=FontStyle.Bold,wordWrap=true};
            body??=new GUIStyle(GUI.skin.label){fontSize=19,wordWrap=true};small??=new GUIStyle(body){fontSize=16};
            digit??=new GUIStyle(GUI.skin.button){fontSize=32};button??=new GUIStyle(GUI.skin.button){fontSize=19,wordWrap=true};
            var old=GUI.matrix;float scale=Mathf.Min(Screen.width/1060f,Screen.height/760f);GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1060*scale)/2,(Screen.height-760*scale)/2),Quaternion.identity,Vector3.one*scale);
            Fill(new Rect(75,25,910,710),new Color(.025f,.045f,.033f,.98f));
            GUI.Label(new Rect(105,46,850,44),IsPower?"ĐIỂM 2 · PHÁ NGUỒN CẤP ĐIỆN":IsRadio?"ĐIỂM 2 · BẢNG MẠCH ĐÀI LIÊN LẠC":"ĐIỂM 1 · KHÓA HÒM TIẾP TẾ",title);
            if(paused){
                GUI.Label(new Rect(170,200,740,100),"ĐANG TẠM DỪNG\nThời gian và tiến độ giải đố được giữ nguyên.",body);
                if(GUI.Button(new Rect(345,350,370,55),"Tiếp tục",button))TogglePause();
                if(GUI.Button(new Rect(345,425,370,55),"Trở về checkpoint trước thao tác",button)){var checkpoint=interactionCheckpoint;Finish(Map01OperationResult.Cancelled);mission.GetComponent<Map01SaveSystem>().RestartInteractionCheckpoint(checkpoint);}
            }else if(roundReady){
                GUI.Label(new Rect(170,190,740,140),IsPower?$"ĐÃ NGẮT {Power.Step}/3 CẦU CHÌ\n\nCầu chì tiếp theo có 10 giây. Nhả phím trước khi bắt đầu.":$"VÒNG {Circuit.Round}/{Circuit.TotalRounds} ĐÃ XONG\n\nVòng tiếp theo có 10 giây. Đồng hồ đang dừng.",body);
                if(GUI.Button(new Rect(330,420,400,60),IsPower?$"CẦU CHÌ {Power.Step+1} — BẮT ĐẦU":$"BẮT ĐẦU VÒNG {Circuit.Round+1}",button))StartPuzzle();
            }else if(instructions) {
                GUI.Label(new Rect(115,125,825,390),IsPower?
                    "PHÁ NGUỒN DỰ PHÒNG\nĐài đã hỏng, nhưng vẫn có nguồn để sửa lại. Ngắt ba cầu chì để vô hiệu hóa bộ cấp điện.\n\nQuan sát kim di chuyển. Bấm SPACE hoặc nút NGẮT CẦU CHÌ khi kim nằm trong dải an toàn. Mỗi cầu chì có 10 giây; vị trí dải thay đổi theo lượt.\n\nNgắt sai hoặc hết giờ: thao tác thất bại, có thể thử lại. Nhả phím giữa các lượt. Sau cầu chì cuối, nguồn cháy hỏng và bốc khói.":IsRadio?
                    "MỤC TIÊU\nNối MỘT dây liền mạch từ NGUỒN đến NGẮT ĐÀI. Mỗi lượt có 2–3 vòng ngẫu nhiên; mỗi vòng 10 giây.\n\nChuột trái: xoay phải · Chuột phải: xoay trái.\nTự tìm chỗ đứt và xoay các đoạn dây để nối nguồn tới đầu ngắt đài. Mọi ô đều xoay được. Dây sáng biểu thị phần đã có điện. Bấm XÁC NHẬN VÒNG khi dây đã liền mạch; vòng cuối mới ngắt đài.\n\nXoay sai KHÔNG bị phạt. Chỉ xác nhận khi dây đã nối đủ; xác nhận sai hoặc hết giờ sẽ gây báo động. Lần sai đầu gọi ba lính.":
                    "MỤC TIÊU\nTìm mã gồm ba chữ số từ 0 đến 5. Chọn số KHÔNG mất lượt; nhấn KIỂM TRA MÃ mới tính một lần thử.\n\nPhản hồi cho biết SỐ LƯỢNG, không chỉ ra ô nào:\nĐúng vị trí: số có trong mã và đúng ô.\nĐúng nhưng sai vị trí: giữ số đó, thử đổi chỗ.\nKhông khớp: số không có trong mã, hoặc bị lặp thừa.\n\nVí dụ mã 241, thử 214: 1 đúng vị trí, 2 đúng nhưng sai vị trí. Sáu lần mỗi lượt; hết lượt được thử lại, giữ lịch sử. Không giới hạn thời gian.",body);
                if(Approaching)GUI.Label(new Rect(115,517,820,25),"Nam đang bước tới đài. Đồng hồ chưa bắt đầu.",small);
                if(GUI.Button(new Rect(365,545,330,55),"Đã hiểu — Bắt đầu",button))StartPuzzle();
                GUI.Label(new Rect(115,650,820,32),"Esc: tạm dừng · Không tự giải khi bỏ qua",small);
            } else if(IsPower)DrawPower();else if(IsRadio)DrawCircuit();else DrawLock();
            GUI.matrix=old;
        }
        void DrawLock()
        {
            if(Event.current.type==EventType.ScrollWheel&&new Rect(115,105,345,190).Contains(Event.current.mousePosition)){
                Change(Event.current.delta.y<0?1:-1);Event.current.Use();
            }
            var digits=Map01SupplyLock.Digits(code);
            for(int i=0;i<3;i++){
                int at=i;var color=GUI.color;if(i==selected)GUI.color=new Color(1,.85f,.45f);
                if(GUI.Button(new Rect(130+i*110,150,90,88),digits[i].ToString(),digit))selected=at;GUI.color=color;
                if(GUI.Button(new Rect(130+i*110,111,90,31),"▲",button)){selected=at;Change(1);}
                if(GUI.Button(new Rect(130+i*110,247,90,31),"▼",button)){selected=at;Change(-1);}
            }
            GUI.Label(new Rect(125,297,335,63),$"Còn {Lock.Remaining}/6 lần kiểm tra.\nĐổi số không mất lượt.",small);
            if(Lock.Remaining>0){if(GUI.Button(new Rect(130,380,300,50),"KIỂM TRA MÃ",button))SubmitLock(code);}
            else if(GUI.Button(new Rect(130,380,300,50),"Hết lượt — Thử lại",button))Lock.Retry();
            Fill(new Rect(115,455,345,175),new Color(.08f,.12f,.09f));
            var last=Lock.History.Count==0?null:Lock.History[Lock.History.Count-1];
            GUI.Label(new Rect(130,466,315,33),last==null?"KẾT QUẢ KIỂM TRA":$"KẾT QUẢ MÃ {last.code:000}",small);
            GUI.Label(new Rect(130,505,315,120),Map01SupplyLock.Feedback(last),body);
            GUI.Label(new Rect(495,110,445,34),"LỊCH SỬ · GIỮ LẠI QUA CÁC LƯỢT",small);
            GUI.Label(new Rect(505,153,70,49),"Mã",small);
            GUI.Label(new Rect(595,153,100,49),"Đúng\nvị trí",small);
            GUI.Label(new Rect(710,153,100,49),"Sai\nvị trí",small);
            GUI.Label(new Rect(820,153,100,49),"Không\nkhớp",small);
            historyScroll=GUI.BeginScrollView(new Rect(495,208,445,420),historyScroll,new Rect(0,0,420,Mathf.Max(420,Lock.History.Count*36)));
            for(int i=0;i<Lock.History.Count;i++){
                var g=Lock.History[i];float y=i*36;
                if(i==Lock.History.Count-1)Fill(new Rect(0,y,420,34),new Color(.12f,.17f,.13f));
                GUI.Label(new Rect(10,y,70,32),$"{g.code:000}",body);
                GUI.Label(new Rect(120,y,70,32),g.exact.ToString(),body);
                GUI.Label(new Rect(235,y,70,32),g.misplaced.ToString(),body);
                GUI.Label(new Rect(345,y,70,32),(3-g.exact-g.misplaced).ToString(),body);
            }
            GUI.EndScrollView();
            GUI.Label(new Rect(125,670,800,32),"A/D chọn · W/S / cuộn tại vòng đổi số · Enter kiểm tra · Esc tạm dừng",small);
        }
        void DrawPower(){
            GUI.Label(new Rect(130,110,800,45),$"CẦU CHÌ {Power.Step+1}/3 · CÒN {Power.Remaining:0.0}s",body);
            for(int i=0;i<3;i++){
                Fill(new Rect(265+i*190,190,150,105),i<Power.Step?new Color(.12f,.25f,.14f):new Color(.25f,.23f,.16f));
                GUI.Label(new Rect(275+i*190,207,130,70),i<Power.Step?"ĐÃ NGẮT":$"CẦU CHÌ {i+1}",body);
            }
            var bar=new Rect(160,355,740,64);Fill(bar,new Color(.13f,.16f,.14f));
            Fill(new Rect(bar.x+(Power.Center-Map01PowerSabotage.Window*.5f)*bar.width,bar.y,bar.width*Map01PowerSabotage.Window,bar.height),new Color(.22f,.52f,.29f));
            Fill(new Rect(bar.x+Power.Needle*bar.width-3,bar.y-10,6,bar.height+20),new Color(1,.83f,.4f));
            GUI.Label(new Rect(160,460,740,80),failureMessage??"Ngắt đúng lúc kim vào dải an toàn. SPACE hoặc chuột bấm một lần.",body);
            if(failureMessage==null){if(GUI.Button(new Rect(340,570,380,60),"NGẮT CẦU CHÌ [SPACE]",button))SubmitPower();}
            else GUI.Label(new Rect(230,580,620,60),"Nam đang rời bảng — có thể thử lại.",body);
        }
        void DrawCircuit()
        {
            GUI.Label(new Rect(130,94,800,35),$"VÒNG {Circuit.Round+1}/{Circuit.TotalRounds}   ·   {Circuit.Remaining:0.0}s   ·   ĐÃ NỐI {Circuit.ConnectedCount}/20",body);
            Fill(new Rect(130,132,800,7),new Color(.18f,.22f,.18f));
            Fill(new Rect(130,132,800*Circuit.Remaining/Map01RadioCircuit.Seconds,7),Circuit.Remaining<=3?new Color(.9f,.3f,.2f):new Color(.9f,.72f,.3f));
            var powered=Circuit.Powered();
            GUI.Label(new Rect(115,177,140,45),"NGUỒN →",body);
            GUI.Label(new Rect(755,465,180,55),"→ NGẮT ĐÀI",small);
            for(int at=0;at<Map01RadioCircuit.Count;at++){
                var r=new Rect(260+(at%Map01RadioCircuit.Columns)*96,150+(at/Map01RadioCircuit.Columns)*96,96,96);
                Fill(new Rect(r.x+2,r.y+2,92,92),new Color(.10f,.14f,.115f));
                int mask=Circuit.Mask(at);var center=r.center;Color wire=powered[at]?new Color(.98f,.78f,.3f):new Color(.55f,.65f,.59f);
                for(int d=0;d<4;d++)if((mask&(1<<d))!=0){var line=d==0?new Rect(center.x-4,r.y,8,48):d==1?new Rect(center.x,center.y-4,48,8):d==2?new Rect(center.x-4,center.y,8,48):new Rect(r.x,center.y-4,48,8);Fill(line,wire);}
                Fill(new Rect(center.x-7,center.y-7,14,14),wire);
                GUI.Label(new Rect(r.x+6,r.y+4,34,23),$"{(char)('A'+at%Map01RadioCircuit.Columns)}{at/Map01RadioCircuit.Columns+1}",small);
                if(failureMessage==null&&Event.current.type==EventType.MouseDown&&(Event.current.button==0||Event.current.button==1)&&r.Contains(Event.current.mousePosition)){Circuit.Rotate(at,Event.current.button==1?-1:1);Event.current.Use();}
            }
            GUI.Label(new Rect(130,548,800,62),failureMessage??Circuit.Feedback,body);
            if(failureMessage==null){if(GUI.Button(new Rect(355,617,350,50),Circuit.Round+1==Circuit.TotalRounds?"NGẮT ĐÀI — XÁC NHẬN":"XÁC NHẬN VÒNG",button))SubmitCircuit();}
            else GUI.Label(new Rect(215,622,650,40),"Đang trả điều khiển — chuẩn bị đối phó quân địch.",small);
            GUI.Label(new Rect(130,680,810,47),"Chuột trái/phải: xoay · Esc: tạm dừng.\nChỉ xác nhận sai hoặc hết giờ mới gây báo động.",small);
        }
        static void Fill(Rect r,Color c){var old=GUI.color;GUI.color=c;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=old;}
    }
}
