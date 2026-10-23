using UnityEngine;
namespace ShadowVale.Map01
{
    public sealed partial class Map02ChapterDirector
    {
        bool NearbyPost(out int post,out int part)
        {
            post=part=-1;if(!ready||Mission.Paused||ForestMenu.Visible||Busy||State.phase!=1||escort!=null||Mission.Panel!=0)return false;
            float best=2.25f*2.25f;
            for(int p=0;p<2;p++){var id=p==0?"N1_02":"N1_03";if(!State.Available(Map02ChapterDefinition.Load().Find(id))||(Day.postsReached&(1<<p))==0)continue;if(Day.posts[p].Safe&&Vector3.ProjectOnPlane(Mission.Player.position-roles["Anh Lực"].transform.position,Vector3.up).sqrMagnitude<=9)continue;var props=new[]{postLamps[p],postGates[p],postBundles[p]};for(int i=0;i<3;i++){float distance=Vector3.ProjectOnPlane(Mission.Player.position-props[i].position,Vector3.up).sqrMagnitude;if(distance<best){best=distance;post=p;part=i;}}}
            return part>=0;
        }
        public bool HasNearbyPost=>NearbyPost(out _,out _);
        public bool TryInteractNearbyPost()
        {
            if(!NearbyPost(out var p,out var part))return false;
            if(UnityEngine.InputSystem.Keyboard.current?.eKey.wasPressedThisFrame!=true)return false;
            postIndex=p;postPart=part;OpenDay(part==0?DayActivity.Lamp:part==1?DayActivity.Gate:DayActivity.Path,Map02ChapterDefinition.Load().Find(p==0?"N1_02":"N1_03"));return true;
        }
        void DrawNearbyDayProps()
        {
            if(!NearbyPost(out var p,out var part))return;
            string[] names={"đèn dầu — xoay chụp che sáng","cửa rào — kiểm tra thanh chốt","lối đi — kiểm tra đồ chắn đường"};
            Cell(new Rect(400,690,800,85),"[E] Kiểm tra "+names[part],new Color(.06f,.08f,.05f,.94f));
        }
        GUIStyle DayText=>new GUIStyle(GUI.skin.label){fontSize=23,wordWrap=true};
        GUIStyle DayHeading=>new GUIStyle(DayText){fontSize=30,fontStyle=FontStyle.Bold};
        bool DayButton(Rect r,string caption)=>GUI.Button(r,caption,new GUIStyle(GUI.skin.button){fontSize=22,wordWrap=true});
        void DrawDayOverlay()
        {
            if(!DayActivityActive||Mission.Paused||ForestMenu.Visible){DrawNearbyDayProps();return;}
            var prior=GUI.color;GUI.color=new Color(0,0,0,.88f);GUI.DrawTexture(new Rect(80,75,1440,745),Texture2D.whiteTexture);GUI.color=prior;
            GUI.Label(new Rect(115,100,1350,48),dayActivity==DayActivity.Packing?"KHO KÍN · BỐ TRÍ VẬT TƯ":dayActivity==DayActivity.Dock?"BẾN NAM · SỬA BẬC LÊN XUỐNG":"KIỂM TRA CHỐT · "+(postIndex==0?"BỜ TRE":"ĐẦU LÀNG"),DayHeading);
            if(DayButton(new Rect(1300,105,175,42),"Đóng [Esc]")){CloseDay();return;}
            if(dayActivity==DayActivity.Packing)DrawPacking();else if(dayActivity==DayActivity.Dock)DrawDock();else DrawPost();
            GUI.Label(new Rect(115,735,1340,72),dayFeedback,DayText);
        }
        void Cell(Rect r,string text,Color color)
        {var tint=GUI.color;GUI.color=color;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=tint;GUI.Box(r,text,new GUIStyle(GUI.skin.box){fontSize=21,alignment=TextAnchor.MiddleCenter});}
        void DrawPacking()
        {
            GUI.Label(new Rect(115,160,1320,75),"Chuột chọn thùng và ô đặt, R xoay. Hoặc phím 1–3 chọn, mũi tên chọn ô, E đặt, Enter xác nhận kho. Giữ lối đi và tránh nền ẩm.",DayText);
            for(int y=0;y<4;y++)for(int x=0;x<6;x++)
            {
                var r=new Rect(160+x*105,260+(3-y)*92,102,89);bool wet=System.Array.Exists(Map02DayOneState.Wet(Day.wetPattern),v=>v.x==x&&v.y==y);
                Cell(r,(packingCursor.x==x&&packingCursor.y==y?"⌖ ":"")+(x<2?"LỐI ĐI":wet?"≈ ẨM":""),x<2?new Color(.24f,.26f,.23f):wet?new Color(.17f,.30f,.38f):new Color(.39f,.32f,.21f));
            }
            for(int i=0;i<3;i++)
            {
                var p=Day.crates[i];var size=Map02DayOneState.Size(i,p.rotated);
                if(p.x>=0){var r=new Rect(160+p.x*105,260+(4-p.y-size.y)*92,size.x*105-3,size.y*92-3);Cell(r,$"THÙNG {i+1}\n{size.x}×{size.y}",new Color(.58f,.45f,.24f));}
                if(DayButton(new Rect(875,260+i*83,550,66),$"{(selectedCrate==i?"▶ ":"")}Thùng {i+1} · {Map02DayOneState.Size(i,false).x}×{Map02DayOneState.Size(i,false).y}")){selectedCrate=i;selectionRotated=p.rotated;dayFeedback="Bấm ô làm góc đặt; R đổi hướng.";}
            }
            // Placement uses the same GUI matrix as the game's 1600×900 reference surface.
            var e=Event.current;if(e.type==EventType.MouseDown&&e.button==0&&new Rect(160,260,630,368).Contains(e.mousePosition))
            {
                int x=Mathf.FloorToInt((e.mousePosition.x-160)/105),y=3-Mathf.FloorToInt((e.mousePosition.y-260)/92);
                packingCursor=new Vector2Int(x,y);PlacePackingSelection(x,y);e.Use();
            }
            var cursorBox=new Rect(160+packingCursor.x*105,260+(3-packingCursor.y)*92,102,89);
            var cursorColor=GUI.color;GUI.color=Color.white;
            GUI.DrawTexture(new Rect(cursorBox.x,cursorBox.y,cursorBox.width,3),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(cursorBox.x,cursorBox.yMax-3,cursorBox.width,3),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cursorBox.x,cursorBox.y,3,cursorBox.height),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(cursorBox.xMax-3,cursorBox.y,3,cursorBox.height),Texture2D.whiteTexture);GUI.color=cursorColor;
            GUI.Label(new Rect(875,520,550,70),selectedCrate<0?"Chưa chọn thùng.":$"Hướng đang chọn: {Map02DayOneState.Size(selectedCrate,selectionRotated).x}×{Map02DayOneState.Size(selectedCrate,selectionRotated).y}",DayText);
            if(DayButton(new Rect(875,625,260,55),"Xếp lại")){Day.ResetCrates();SyncDayProps();}
            if(DayButton(new Rect(1150,625,275,55),"Xác nhận cất hàng"))CompletePacking();
        }
        void DrawDock()
        {
            GUI.Label(new Rect(125,158,1260,45),$"1. Đóng cọc {Day.stakeHits/3}/2     →     2. Kê ván {Day.BoardCount}/3     →     3. Kiểm tra bến",DayText);
            if(Day.stakeHits<6)
            {
                int stake=Day.stakeHits/3;float width=Map02DayOneState.ZoneWidth(stake),needle=Mathf.PingPong(dayClock/Map02DayOneState.SweepSeconds(stake),1);
                GUI.Label(new Rect(130,220,1250,75),$"Cọc {stake+1}/2 · đã đóng {Day.stakeHits%3}/3 nhịp. Nhấn rồi nhả SPACE khi kim nằm trong vùng ✓. Sai chỉ thử lại nhịp này.",DayText);
                for(int i=0;i<2;i++){float depth=Mathf.Clamp(Day.stakeHits-i*3,0,3)*18;Cell(new Rect(420+i*600,310+depth,55,150-depth),$"{i+1}",new Color(.54f,.37f,.18f));}
                Cell(new Rect(200,490,1200,65),"",new Color(.2f,.22f,.2f));Cell(new Rect(200+1200*(.5f-width*.5f),490,1200*width,65),"✓",new Color(.5f,.6f,.3f));Cell(new Rect(200+1200*needle-3,480,6,85),"",Color.white);
                GUI.Label(new Rect(130,600,1250,65),"Chú Bảy giữ thấp thân cọc; Nam đóng từ phía đối diện. Hoàn tất hai cọc sẽ chuyển sang kê ván.",DayText);return;
            }
            GUI.Label(new Rect(125,210,1300,70),"Chọn ván bên trái (phím 1–5), chọn bậc (↑/↓), A/D dịch, R xoay, E kê ván. Hai đầu phải tựa lên thanh đỡ. Esc đóng và giữ phần đã xong.",DayText);
            for(int i=0;i<5;i++){
                int board=Day.boardOrder[i];bool used=System.Array.Exists(Day.boards,p=>p.x==board);
                if(DayButton(new Rect(125,300+i*72,420,62),$"{(selectedBoard==board?"▶ ":"")}Ván {i+1}{(used?" · Đã dùng":"")}")){selectedBoard=board;selectionRotated=false;dockOffset=.24f;dayFeedback=board==3?"Quan sát: vết nứt chạy dọc giữa thân ván.":board==4?"Quan sát: tấm này ngắn hơn khoảng cách hai thanh đỡ.":"Thớ gỗ liền. Căn hai đầu ván trên thanh đỡ.";}
                // Visible lengths and damage accompany the description, not color alone.
                float length=board==4?170:290;Cell(new Rect(180,344+i*72,length,9),"",new Color(.67f,.48f,.25f));if(board==3)GUI.Label(new Rect(285,335+i*72,110,22),"╱╲╱",new GUIStyle(DayText){fontSize=18});
            }
            Cell(new Rect(720,300,38,300),"ĐỠ",new Color(.48f,.36f,.2f));Cell(new Rect(1290,300,38,300),"ĐỠ",new Color(.48f,.36f,.2f));
            for(int slot=0;slot<3;slot++){
                if(DayButton(new Rect(570,312+slot*90,130,65),$"{(dockSlot==slot?"▶ ":"")}Bậc {slot+1}"))dockSlot=slot;
                if(Day.boards[slot].x>=0)Cell(new Rect(725,320+slot*90,595,48),"✓ ĐÃ KÊ",new Color(.50f,.39f,.23f));
            }
            if(selectedBoard>=0){
                float length=selectedBoard==4?290:600,cx=1025+dockOffset*600,cy=344+dockSlot*90;
                Cell(new Rect(cx-(selectionRotated?22:length*.5f),cy-(selectionRotated?90:20),selectionRotated?44:length,selectionRotated?180:40),selectedBoard==3?"╱╲╱  NỨT":"VÁN ĐANG CĂN",new Color(.72f,.55f,.30f));
            }
            GUI.Label(new Rect(700,605,700,45),"A ← dịch trái     dịch phải → D     R xoay",DayText);
            if(DayButton(new Rect(700,657,300,52),Day.DockReady?"Kiểm tra bến [E]":"Kê tấm đang chọn [E]"))ConfirmDockBoard();
            if(DayButton(new Rect(1030,657,330,52),"Xem lại hướng dẫn"))dayFeedback="Chọn ván nguyên, đủ dài. Hai thanh đứng là chỗ đỡ; dịch ván để cả hai đầu nằm trên thanh, rồi E. Không mất ván khi làm sai.";
        }
        void DrawPost()
        {
            var p=Day.posts[postIndex];
            if(dayActivity==DayActivity.Lamp)
            {
                GUI.Label(new Rect(140,175,1280,100),"A/D xoay chụp đèn. Ánh sáng cần chiếu vào trong làng, không hắt ra mé ngoài. Nhấn E hoặc nút xác nhận sau khi quan sát hướng sáng.",DayText);
                var center=new Vector2(780,455);GUI.Label(new Rect(650,285,400,45),"PHÍA NGOÀI LÀNG",DayText);GUI.Label(new Rect(650,620,400,45),"PHÍA TRONG LÀNG",DayText);
                GUI.Box(new Rect(600,345,380,260),"ĐÈN");var end=center+new Vector2(Mathf.Sin(p.lampAngle*Mathf.Deg2Rad),-Mathf.Cos(p.lampAngle*Mathf.Deg2Rad))*130;
                var matrix=GUI.matrix;float angle=Mathf.Atan2(end.y-center.y,end.x-center.x)*Mathf.Rad2Deg;GUIUtility.RotateAroundPivot(angle,center);Cell(new Rect(center.x,center.y,130,14),"→",new Color(1,.8f,.35f));GUI.matrix=matrix;
                if(DayButton(new Rect(1050,620,320,60),"Xác nhận hướng đèn")){dayFeedback=p.LampSafe?"✓ Ánh đèn đã được che đúng hướng.":"Ánh sáng vẫn lọt ra ngoài làng.";if(p.LampSafe)CloseDay();}
            }
            else if(dayActivity==DayActivity.Gate)
            {
                GUI.Label(new Rect(140,190,1250,100),"A khép cửa sát khung, D trượt chốt vào ngàm, E xác nhận. Đây là cửa rào nhỏ của chốt, không chặn đường chính.",DayText);
                GUI.Label(new Rect(200,335,1100,70),$"Cánh cửa: {(p.gateClosed?"khép sát":"đang hé")} · thanh chốt: {(p.latchSeated?"đã vào ngàm":"chưa cài")}",DayText);
                if(DayButton(new Rect(230,460,480,80),p.gateClosed?"Mở hé để kiểm tra":"Khép sát cửa")){p.gateClosed=!p.gateClosed;if(!p.gateClosed)p.latchSeated=false;SyncDayProps();}
                if(DayButton(new Rect(840,460,480,80),"Trượt thanh chốt")){if(!p.gateClosed)dayFeedback="Phải khép cửa sát khung trước.";else{p.latchSeated=true;dayFeedback="✓ Chốt đã vào ngàm.";SyncDayProps();}}
                if(DayButton(new Rect(1050,640,320,60),"Kiểm tra / quay lại")){if(p.gateClosed&&p.latchSeated)CloseDay();else dayFeedback="Cửa rào chưa cài chắc.";}
            }
            else
            {
                GUI.Label(new Rect(140,180,1250,90),"Sơ đồ nhìn từ trên: đưa bó vật liệu vào giá chứa, giữ trống lối đi và vùng cửa mở. Bấm vị trí muốn đặt; hoặc 1 đặt cạnh cửa, 2 cất vào giá, E xác nhận.",DayText);
                if(p.bundleStowed)GUI.Label(new Rect(880,260,500,55),"✓ Đồ đã được cất, lối gác đang thông.",DayText);
                Cell(new Rect(220,310,620,300),"",new Color(.15f,.20f,.12f));
                Cell(new Rect(410,310,150,300),"LỐI ĐI\nGÁC",new Color(.48f,.42f,.28f));
                Cell(new Rect(565,315,240,100),"CỬA RÀO\nKhoảng mở cửa",new Color(.38f,.28f,.17f));
                Cell(new Rect(p.bundleStowed?250:425,p.bundleStowed?470:440,110,65),"BÓ CỦI",new Color(.55f,.37f,.18f));
                if(DayButton(new Rect(880,325,500,100),"Đặt cạnh cửa rào"))dayFeedback="Bó củi sẽ chắn khoảng mở cửa. Chọn vị trí khác.";
                if(DayButton(new Rect(880,470,500,100),"Chuyển vào giá chứa trong chốt")){p.bundleStowed=true;SyncDayProps();dayFeedback="✓ Lối gác đã thông. Quay lại anh Lực khi kiểm tra xong.";}
                if(DayButton(new Rect(1000,610,360,55),"Xác nhận và quay lại")){if(p.bundleStowed)CloseDay();else dayFeedback="Bó vật liệu vẫn nằm trên lối đi.";}

            }
            if(DayButton(new Rect(140,650,500,60),"Nhắc lại cần kiểm tra gì?"))dayFeedback="Đèn không lọt sáng; cửa khép và cài chốt; đường đi không vướng đồ, không chắn cửa.";
        }
    }
}
