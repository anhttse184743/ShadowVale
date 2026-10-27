using UnityEngine;
namespace ShadowVale.Map01
{
    public sealed partial class Map02ChapterDirector
    {
        bool NearbyPost(out int post,out int part)
        {
            post=part=-1;if(!ready||Mission.Paused||ForestMenu.Visible||Busy||State.phase!=1||escort!=null||Mission.Panel!=0)return false;
            float best=2.25f*2.25f;
            for(int p=0;p<2;p++){var id=p==0?"N1_02":"N1_03";if(!State.Available(Map02ChapterDefinition.Load().Find(id))||(Day.postsReached&(1<<p))==0)continue;if(Day.posts[p].Safe&&Vector3.ProjectOnPlane(Mission.Player.position-PostKeeper(id).transform.position,Vector3.up).sqrMagnitude<=9)continue;var props=new[]{postLamps[p],postGates[p],postBundles[p]};for(int i=0;i<3;i++){float distance=Vector3.ProjectOnPlane(Mission.Player.position-props[i].position,Vector3.up).sqrMagnitude;if(distance<best){best=distance;post=p;part=i;}}}
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
        /// <summary>The lamp's beam on the 1600×900 HUD: 0° points out of the village (up), turning clockwise.
        /// GUIUtility.RotateAroundPivot takes a screen-pixel pivot, which misses the lamp whenever the HUD is scaled.</summary>
        public static Matrix4x4 LampBeamMatrix(Matrix4x4 canvas,Vector2 lamp,float lampAngle)=>Map02Navigation.RotateUi(canvas,lamp,lampAngle-90);
        GUIStyle DayText=>new GUIStyle(GUI.skin.label){fontSize=23,wordWrap=true};
        GUIStyle DayHeading=>new GUIStyle(DayText){fontSize=30,fontStyle=FontStyle.Bold};
        bool DayButton(Rect r,string caption)=>GUI.Button(r,caption,new GUIStyle(GUI.skin.button){fontSize=22,wordWrap=true});
        void DrawDayOverlay()
        {
            bool previous=GUI.enabled;GUI.enabled=previous&&(!DayActivityActive||dayInputReady);
            try{DrawDayOverlayContent();}finally{GUI.enabled=previous;}
        }
        void DrawDayOverlayContent()
        {
            if(!DayActivityActive||Mission.Paused||ForestMenu.Visible){DrawNearbyDayProps();return;}
            if(dayActivity==DayActivity.Dock&&!DockHammering){DrawDockBoardPanel();return;}
            if(dayActivity!=DayActivity.Packing){DrawWorkStrip();return;}
            var prior=GUI.color;GUI.color=new Color(0,0,0,.88f);GUI.DrawTexture(new Rect(80,75,1440,745),Texture2D.whiteTexture);GUI.color=prior;
            GUI.Label(new Rect(115,100,1350,48),"HẦM DƯỚI BỤI CHUỐI · CẤT VẬT TƯ",DayHeading);
            if(DayButton(new Rect(1300,105,175,42),"Đóng [Esc]")){RequestCloseDay();return;}
            DrawPacking();
            GUI.Label(new Rect(115,735,1340,72),dayInputReady?dayFeedback:"Nhả phím/chuột mở bảng để bắt đầu thao tác.",DayText);
        }
        // The dock and the night posts are worked in the world (WorkView): a strip along the bottom, the camera on the
        // stake, the steps or the post, so the blows, chú Bảy and the boards going down are seen, not covered.
        GUIStyle StripText=>new GUIStyle(DayText){fontSize=21};
        GUIStyle StripHint=>new GUIStyle(DayText){fontSize=18,normal={textColor=new Color(.78f,.8f,.72f)}};
        void DrawWorkStrip()
        {
            var prior=GUI.color;GUI.color=new Color(.03f,.04f,.03f,.82f);GUI.DrawTexture(new Rect(80,676,1440,206),Texture2D.whiteTexture);GUI.color=prior;
            GUI.Label(new Rect(105,682,640,38),dayActivity==DayActivity.Dock?"BẾN NAM · SỬA BẬC LÊN XUỐNG":"KIỂM TRA CHỐT · "+(postIndex==0?"BỜ TRE":"ĐẦU LÀNG"),new GUIStyle(DayHeading){fontSize=24});
            if(DayButton(new Rect(1355,684,150,38),"Đóng [Esc]")){RequestCloseDay();return;}
            if(dayActivity==DayActivity.Dock)DrawDock();else DrawPost();
            GUI.Label(new Rect(105,842,1400,36),dayInputReady?dayFeedback:"Nhả phím/chuột mở bảng để bắt đầu thao tác.",StripText);
        }
        void Cell(Rect r,string text,Color color)
        {var tint=GUI.color;GUI.color=color;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=tint;GUI.Box(r,text,new GUIStyle(GUI.skin.box){fontSize=21,alignment=TextAnchor.MiddleCenter});}
        void DrawDock()
        {
            int step=DockHammering?0:Day.DockReady?2:1;
            string[] steps={$"1. Cọc {Day.stakeHits/3}/2",$"2. Ván {Day.BoardCount}/3","3. Kiểm tra bến"};
            for(int i=0;i<3;i++)GUI.Label(new Rect(760+i*190,688,185,32),(i==step?"▶ ":"")+steps[i],i==step?StripText:StripHint);
            if(DockHammering)
            {
                int stake=VisibleStakeIndex;float width=Map02DayOneState.ZoneWidth(stake),needle=Mathf.PingPong(dayClock/Map02DayOneState.SweepSeconds(stake),1);
                int hit=Mathf.Clamp(Day.stakeHits-stake*3,0,3);
                GUI.Label(new Rect(105,730,320,32),$"Cọc {stake+1}/2 · đã đóng {hit}/3",StripText);
                for(int i=0;i<3;i++)Cell(new Rect(108+i*36,768,28,28),i<hit?"✓":"",i<hit?new Color(.78f,.62f,.28f):new Color(.22f,.24f,.2f));
                var bar=new Rect(450,738,780,54);Cell(bar,"",new Color(.2f,.22f,.2f));Cell(new Rect(bar.x+bar.width*(.5f-width*.5f),bar.y,bar.width*width,bar.height),"✓",new Color(.5f,.6f,.3f));Cell(new Rect(bar.x+bar.width*needle-3,bar.y-8,6,bar.height+16),"",Color.white);
                GUI.Label(new Rect(1250,731,250,80),StrikeRecovering?"Đang hạ / nâng búa…":daySpaceReleased?"Bấm SPACE khi kim nằm trong ✓":"Nhả SPACE trước nhịp tiếp theo",StripHint);
                GUI.Label(new Rect(105,804,1400,32),"Mỗi lần nhấn là một nhát. Chờ búa nâng lên; trượt nhịp giữ nguyên các nhịp đã đạt.",StripHint);return;
            }
        }
        void DrawPost()
        {
            var p=Day.posts[postIndex];
            GUI.Label(new Rect(105,804,1100,32),"Chốt sẵn sàng khi: đèn không lọt sáng ra ngoài · cửa rào khép, cài chốt · lối đi không vướng đồ.",StripHint);
            if(dayActivity==DayActivity.Lamp)
            {
                GUI.Label(new Rect(105,728,820,70),"A/D xoay chụp đèn. Vũng sáng phải rơi vào phía trong làng, không hắt ra mé ngoài.",StripText);
                var center=new Vector2(1065,774);GUI.Box(new Rect(965,716,200,116),GUIContent.none);
                GUI.Label(new Rect(965,716,200,22),"▲ NGOÀI LÀNG",new GUIStyle(StripHint){fontSize=14,alignment=TextAnchor.MiddleCenter});GUI.Label(new Rect(965,810,200,22),"▼ TRONG LÀNG",new GUIStyle(StripHint){fontSize=14,alignment=TextAnchor.MiddleCenter});
                var matrix=GUI.matrix;GUI.matrix=LampBeamMatrix(matrix,center,p.lampAngle);Cell(new Rect(center.x,center.y-4,32,8),"",new Color(1,.8f,.35f));Cell(new Rect(center.x+26,center.y-9,10,18),"",new Color(1,.8f,.35f));GUI.matrix=matrix;
                Cell(new Rect(center.x-7,center.y-7,14,14),"",new Color(.35f,.3f,.22f));
                if(DayButton(new Rect(1210,746,295,50),"Xác nhận hướng đèn [E]"))ConfirmPostPart();
            }
            else if(dayActivity==DayActivity.Gate)
            {
                GUI.Label(new Rect(105,728,690,32),"A khép cửa sát khung · D trượt chốt vào ngàm · E xác nhận.",StripText);
                GUI.Label(new Rect(105,764,690,32),$"Cánh cửa: {(p.gateClosed?"khép sát":"đang hé")} · thanh chốt: {(p.latchSeated?"đã vào ngàm":"chưa cài")}",StripHint);
                if(DayButton(new Rect(810,746,190,50),p.gateClosed?"Mở hé":"Khép cửa [A]")){p.gateClosed=!p.gateClosed;if(!p.gateClosed)p.latchSeated=false;SyncDayProps();}
                if(DayButton(new Rect(1010,746,190,50),"Trượt chốt [D]")){if(!p.gateClosed)dayFeedback="Phải khép cửa sát khung trước.";else{p.latchSeated=true;dayFeedback="✓ Chốt đã vào ngàm.";SyncDayProps();}}
                if(DayButton(new Rect(1210,746,295,50),"Kiểm tra [E]"))ConfirmPostPart();
            }
            else
            {
                GUI.Label(new Rect(105,728,690,70),p.bundleStowed?"Lối gác đã thông; bó củi ở giá bên trong. Có thể xác nhận.":"Giữ trống lối đi gác và khoảng mở cửa rào: bó củi đang nằm giữa lối.",StripText);
                if(DayButton(new Rect(810,746,190,50),"Đặt cạnh cửa [1]"))dayFeedback="Bó củi sẽ chắn khoảng mở cửa. Chọn vị trí khác.";
                if(DayButton(new Rect(1010,746,190,50),"Cất vào giá [2]")){p.bundleStowed=true;SyncDayProps();dayFeedback="✓ Lối gác đã thông. Quay lại "+(postIndex==0?"tổ gác":"anh Lực")+" khi kiểm tra xong.";}
                if(DayButton(new Rect(1210,746,295,50),"Xác nhận [E]"))ConfirmPostPart();
            }
        }
    }
}
