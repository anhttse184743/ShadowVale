using System.Linq;
using UnityEngine;
namespace ShadowVale.Map01
{
    public sealed partial class Map01Hud
    {
        string mapTwoObjective;
        void DrawMapTwoCharacterHud()
        {
            HudStyles();HookPickups();var m=mission.MapTwo;
            var matrix=GUI.matrix;var color=GUI.color;int depth=GUI.depth;
            float scale=HudScale,width=Screen.width/scale,height=Screen.height/scale;
            GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));GUI.color=Color.white;GUI.depth=-10;
            try
            {
                if(m.Panel==4){DrawBench(width,height);return;}
                if(m.Panel==1)
                {
                    DrawInventory(width);
                    if(inventory.NearWorkbench()&&GUI.Button(new Rect(28,100,250,50),"CHẾ TẠO / SỬA SÚNG",hudKey))mission.SetBenchOpen(true);
                    DrawHudFeedback(width,height);return;
                }
                if(m.Panel==2||m.Panel==3)
                {
                    if(m.Panel==3)
                    {
                        var plate=new Rect((width-1150)/2,55,1150,height-110);HudPanel(plate);
                        GUI.Label(new Rect(plate.x+24,plate.y+16,600,40),"BẢN ĐỒ ĐỊA HÌNH",hudHeading);
                        float size=Mathf.Min(620,height-235);m.DrawCharacterMap(new Rect(plate.x+24,plate.y+70,size,size),false,hudSmall);
                        m.DrawCharacterObjectives(new Rect(plate.x+size+46,plate.y+70,450,height-230));
                        GUI.Label(new Rect(plate.x+24,plate.yMax-57,plate.width-48,45),"Bạn ▲  Đồng đội ●  Mục tiêu ◇  Địch △ · Vùng tối: chưa khám phá · M / Esc đóng",hudSmall);
                    }
                    else
                    {
                        var plate=new Rect(width/2-440,70,880,height-140);HudPanel(plate);
                        GUI.Label(new Rect(plate.x+24,plate.y+16,800,50),"NHIỆM VỤ · "+Map02ChapterState.PhaseName(m.Chapter.State.phase),hudHeading);
                        m.DrawCharacterObjectives(new Rect(plate.x+24,plate.y+85,plate.width-48,plate.height-130));
                    }
                    return;
                }
                if(m.Chapter.Binoculars)DrawMapTwoBinoculars(width,height);
                DrawCombatHud(width,height);DrawPickups(width);DrawStoneAim(width,height);
                var map=new Rect(40,30,221,221);m.DrawCharacterMap(map,true,hudSmall);
                HudFill(new Rect(map.center.x-95,map.yMax+9,190,27),new Color(.02f,.03f,.02f,.5f));
                GUI.Label(new Rect(map.center.x-95,map.yMax+9,190,27),"M  MỞ BẢN ĐỒ",hudKey);
                if(mapTwoObjective!=m.Chapter.TrackedTask?.id){mapTwoObjective=m.Chapter.TrackedTask?.id;ObjectiveShown=true;objectiveSecondsLeft=5;}
                DrawObjective();
                GUI.Label(new Rect(28,305,Mathf.Min(700,width-60),45),Map02ChapterState.PhaseName(m.Chapter.State.phase),hudSmall);
                var goal=m.TrackedObjective;
                if(goal!=null)
                {
                    var point=goal.standPoint!=null?goal.standPoint.position:goal.transform.position;
                    var forward=Vector3.ProjectOnPlane(m.GameCamera.transform.forward,Vector3.up);
                    var toward=Vector3.ProjectOnPlane(m.CharacterSteerPoint-m.Player.position,Vector3.up);
                    var pivot=new Vector2(width/2-210,56);var prior=GUI.matrix;
                    GUI.matrix=Map02Navigation.RotateUi(prior,pivot,Vector3.SignedAngle(forward,toward,Vector3.up));
                    GUI.DrawTexture(new Rect(pivot.x-18,pivot.y-18,36,36),compassArrow);GUI.matrix=prior;
                    ShadowLabel(new Rect(pivot.x+30,35,460,60),goal.prompt+" · "+Vector3.Distance(m.Player.position,point).ToString("0")+" m",hudSmall);
                    var at=m.GameCamera.WorldToViewportPoint(point+Vector3.up*1.6f);
                    if(at.z>0&&at.x>.12f&&at.x<.88f&&at.y>.18f&&at.y<.8f)m.DrawCharacterPin(new Vector2(at.x*width,(1-at.y)*height));
                }
                m.DrawCharacterAwareness(width,height,hudSmall);
                DrawHudFeedback(width,height);
                if(!string.IsNullOrEmpty(m.InteractionPrompt))GUI.Label(new Rect(width/2-360,height-160,720,50),"[E] "+m.InteractionPrompt,hudBody);
                GUI.Label(new Rect(28,height-52,width-430,50),"Tab Túi đồ · 1 Súng chính · 2 Súng ngắn · 3 Dao · 4 Đồ ném · R Nạp đạn · H Hồi máu\nM Bản đồ · J Nhiệm vụ · Esc Menu",hudSmall);
            }
            finally{GUI.matrix=matrix;GUI.color=color;GUI.depth=depth;}
        }
        void DrawMapTwoBinoculars(float width,float height)
        {
            if(binocularMask==null)binocularMask=MakeBinocularMask();
            float maskWidth=height*2,x=(width-maskWidth)/2;GUI.DrawTexture(new Rect(x,0,maskWidth,height),binocularMask);
            if(x>0){HudFill(new Rect(0,0,x,height),Color.black);HudFill(new Rect(width-x,0,x,height),Color.black);}
            var plate=new Rect(width/2-300,height-150,600,64);HudPanel(plate);GUI.Label(new Rect(plate.x+16,plate.y+10,plate.width-32,48),"ỐNG NHÒM · Quan sát ánh đèn, giữ yên để xác nhận.\nF đóng ống nhòm",hudSmall);
        }
        public void DrawCharacterEye(Vector2 at,bool spotted)
        {
            if(!spotted){DrawHeard(at);return;}
            if(eyeFill==null){eyeFill=MakeEye(false);eyeOutline=MakeEye(true);}
            float size=50*(1+.12f*Mathf.Abs(Mathf.Sin(Time.unscaledTime*7)));var eye=new Rect(at.x-size/2,at.y-size/4,size,size/2);
            var tint=GUI.color;GUI.color=new Color(0,0,0,.6f);GUI.DrawTexture(new Rect(eye.x-2,eye.y-2,eye.width+4,eye.height+4),eyeFill);
            GUI.color=Warning;GUI.DrawTexture(eye,eyeFill);GUI.color=new Color(1,.95f,.82f);GUI.DrawTexture(eye,eyeOutline);GUI.color=tint;
        }
    }
}
