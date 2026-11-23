using UnityEngine;
namespace ShadowVale.Map01
{
    public sealed partial class Map02ChapterDirector
    {
        bool NearbyPost(out int post,out int part)
        {
            post=part=-1;if(!ready||Mission.Paused||ForestMenu.Visible||Busy||State.phase!=1||escort!=null||Mission.Panel!=0)return false;
            float best=2.25f*2.25f;
            for(int p=0;p<2;p++){
                var id=p==0?"N1_02":"N1_03";if(!State.Available(Map02ChapterDefinition.Load().Find(id))||(Day.postsReached&(1<<p))==0)continue;
                var state=Day.posts[p];var props=new[]{postLamps[p],postGates[p],postBundles[p]};
                for(int i=0;i<3;i++){
                    bool safe=i==0?state.LampSafe:i==1?state.gateClosed&&state.latchSeated:state.bundleStowed;if(safe)continue;
                    var point=props[i].GetComponent<Map02NightWatchVisual>()?.InteractionPosition??props[i].position;
                    float distance=Vector3.ProjectOnPlane(Mission.Player.position-point,Vector3.up).sqrMagnitude;
                    if(distance<best){best=distance;post=p;part=i;}
                }
            }
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
            DrawNightPropDirections();
            if(!NearbyPost(out var p,out var part))return;
            string[] names={"đèn dầu — xoay chụp che sáng","cửa rào — kiểm tra thanh chốt","lối đi — kiểm tra đồ chắn đường"};
            Cell(new Rect(400,690,800,85),"[E] Kiểm tra "+names[part],new Color(.06f,.08f,.05f,.94f));
        }
        /// <summary>The lamp's beam on the 1600×900 HUD: 0° points out of the village (up), turning clockwise.
        /// GUIUtility.RotateAroundPivot takes a screen-pixel pivot, which misses the lamp whenever the HUD is scaled.</summary>
        public static Matrix4x4 LampBeamMatrix(Matrix4x4 canvas,Vector2 lamp,float lampAngle)=>Map02Navigation.RotateUi(canvas,lamp,lampAngle-90);
        GUIStyle DayText=>new GUIStyle(GUI.skin.label){fontSize=23,wordWrap=true,normal={textColor=DayInk}};
        GUIStyle DayHeading=>new GUIStyle(DayText){fontSize=30,fontStyle=FontStyle.Bold};
        bool DayButton(Rect r,string caption)
        {
            bool hover=r.Contains(Event.current.mousePosition);
            Fill(r,GUI.enabled?(hover?new Color(.27f,.35f,.22f):new Color(.14f,.20f,.14f)):new Color(.09f,.12f,.09f));
            Frame(r,GUI.enabled?(hover?DayGold:new Color(.44f,.51f,.35f)):new Color(.23f,.28f,.21f),1);
            GUI.Label(Inset(r,5),caption,StoreText(20,GUI.enabled?DayInk:DayMuted,FontStyle.Bold,TextAnchor.MiddleCenter));
            return GUI.Button(r,GUIContent.none,GUIStyle.none);
        }
        void DrawDayOverlay()
        {
            bool previous=GUI.enabled;GUI.enabled=previous&&(!DayActivityActive||dayInputReady);
            try
            {
                GUI.enabled=GUI.enabled&&!dayHelpVisible;
                if(!dayHelpVisible)DrawDayOverlayContent();
                GUI.enabled=previous&&dayInputReady;
                if(dayHelpVisible&&DayActivityActive)DrawDayHelp();
            }
            finally{GUI.enabled=previous;}
        }
        void DrawDayOverlayContent()
        {
            if(!DayActivityActive||Mission.Paused||ForestMenu.Visible){DrawNearbyDayProps();return;}
            if(dayActivity==DayActivity.Lamp||dayActivity==DayActivity.Gate||dayActivity==DayActivity.Path){DrawPostPanel();return;}
            if(dayActivity==DayActivity.Dock&&!DockHammering){DrawDockBoardPanel();return;}
            if(dayActivity!=DayActivity.Packing){DrawWorkStrip();return;}
            Fill(new Rect(0,0,1600,900),new Color(.01f,.02f,.01f,.65f));DaySurface(new Rect(80,75,1440,750));
            DayHelpButton(new Rect(1080,105,200,42));GUI.Label(new Rect(115,100,930,48),"HẦM DƯỚI BỤI CHUỐI · CẤT VẬT TƯ",DayHeading);
            if(DayButton(new Rect(1300,105,175,42),"Đóng [Esc]")){RequestCloseDay();return;}
            DrawPacking();
            GUI.Label(new Rect(115,742,1340,66),dayInputReady?(string.IsNullOrEmpty(dayFeedback)?"Chọn hòm bên phải, đưa vào vùng khô rồi bấm để đặt.":dayFeedback):"Nhả phím/chuột mở bảng để bắt đầu thao tác.",DayText);
        }
        // The dock and the night posts are worked in the world (WorkView): a strip along the bottom, the camera on the
        // stake, the steps or the post, so the blows, chú Bảy and the boards going down are seen, not covered.
        GUIStyle StripText=>new GUIStyle(DayText){fontSize=21};
        GUIStyle StripHint=>new GUIStyle(DayText){fontSize=18,normal={textColor=new Color(.78f,.8f,.72f)}};
        void DrawWorkStrip()
        {
            DaySurface(new Rect(80,662,1440,220));DayHelpButton(new Rect(1120,684,215,38));
            GUI.Label(new Rect(105,682,640,38),dayActivity==DayActivity.Dock?"BẾN NAM · SỬA BẬC LÊN XUỐNG":"KIỂM TRA CHỐT · "+(postIndex==0?"BỜ TRE":"ĐẦU LÀNG"),new GUIStyle(DayHeading){fontSize=24});
            if(DayButton(new Rect(1355,684,150,38),"Đóng [Esc]")){RequestCloseDay();return;}
            DrawDock();
            GUI.Label(new Rect(105,842,1400,36),dayInputReady?dayFeedback:"Nhả phím/chuột mở bảng để bắt đầu thao tác.",StripText);
        }
        void Cell(Rect r,string text,Color color)
        {var tint=GUI.color;GUI.color=color;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=tint;GUI.Box(r,text,new GUIStyle(GUI.skin.box){fontSize=21,alignment=TextAnchor.MiddleCenter});}
        void DrawDock()
        {
            int step=DockHammering?0:Day.DockReady?2:1;
            string[] steps={$"1. Cọc {Day.stakeHits/3}/2",$"2. Ván {Day.BoardCount}/3","3. Kiểm tra bến"};
            Fill(new Rect(690,616,820,42),new Color(.035f,.05f,.035f,.92f));
            for(int i=0;i<3;i++)GUI.Label(new Rect(700+i*260,625,250,32),(i==step?"▶ ":"")+steps[i],i==step?StripText:StripHint);
            if(DockHammering)
            {
                int stake=VisibleStakeIndex;float width=Map02DayOneState.ZoneWidth(stake),needle=Mathf.PingPong(dayClock/Map02DayOneState.SweepSeconds(stake),1);
                int hit=Mathf.Clamp(Day.stakeHits-stake*3,0,3);
                GUI.Label(new Rect(105,730,320,32),$"Cọc {stake+1}/2 · đã đóng {hit}/3",StripText);
                for(int i=0;i<3;i++)Cell(new Rect(108+i*36,768,28,28),i<hit?"✓":"",i<hit?new Color(.78f,.62f,.28f):new Color(.22f,.24f,.2f));
                var bar=new Rect(450,738,780,54);
                Fill(bar,new Color(.12f,.17f,.12f));Frame(bar,new Color(.42f,.48f,.33f),1);
                var zone=new Rect(bar.x+bar.width*(.5f-width*.5f),bar.y,bar.width*width,bar.height);
                Fill(zone,new Color(.36f,.57f,.29f));
                GUI.Label(zone,"✓",StoreText(27,Color.white,FontStyle.Bold,TextAnchor.MiddleCenter));
                var needleRect=new Rect(bar.x+bar.width*needle-4,bar.y-8,8,bar.height+16);
                Fill(new Rect(needleRect.x-2,needleRect.y-2,12,needleRect.height+4),new Color(.03f,.05f,.03f));
                Fill(needleRect,Color.white);
                GUI.Label(new Rect(1250,731,250,80),StrikeRecovering?"Đang hạ / nâng búa…":daySpaceReleased?"Bấm SPACE khi kim nằm trong ✓":"Nhả SPACE trước nhịp tiếp theo",StripHint);
                GUI.Label(new Rect(105,804,1400,32),"Mỗi lần nhấn là một nhát. Chờ búa nâng lên; trượt nhịp giữ nguyên các nhịp đã đạt.",StripHint);return;
            }
        }
    }
}
