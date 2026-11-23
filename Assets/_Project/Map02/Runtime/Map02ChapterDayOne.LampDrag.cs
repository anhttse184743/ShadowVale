using UnityEngine;
namespace ShadowVale.Map01
{
    public sealed partial class Map02ChapterDirector
    {
        int lampDragControl;Vector2 lampDragPointer;
        void DrawLampDragSurface(Rect area)
        {
            int id=GUIUtility.GetControlID(731923,FocusType.Passive,area);var e=Event.current;
            if(!GUI.enabled)return;
            if(e.type==EventType.MouseDown&&e.button==0&&area.Contains(e.mousePosition))
            {GUIUtility.hotControl=id;lampDragControl=id;lampDragPointer=e.mousePosition;e.Use();}
            else if(e.type==EventType.MouseDrag&&GUIUtility.hotControl==id)
            {TurnPostLamp((e.mousePosition.x-lampDragPointer.x)*.36f);lampDragPointer=e.mousePosition;dayFeedback="Kéo chụp che cho cửa sáng quay vào trong làng, rồi nhả chuột.";e.Use();}
            else if(e.type==EventType.MouseUp&&GUIUtility.hotControl==id)
            {GUIUtility.hotControl=0;lampDragControl=0;e.Use();}
        }
        void EndLampDrag(){if(lampDragControl!=0&&GUIUtility.hotControl==lampDragControl)GUIUtility.hotControl=0;lampDragControl=0;}
        void DrawNightPropDirections()
        {
            if(State.phase!=1||sceneTask!=null||DayActivityActive||Mission.Paused||ForestMenu.Visible)return;
            int p=State.activeTask=="N1_02"?0:State.activeTask=="N1_03"?1:-1;
            if(p<0||(Day.postsReached&(1<<p))==0)return;
            var post=Day.posts[p];var camera=rig.GetComponent<Camera>();
            var props=new[]{postLamps[p],postGates[p],postBundles[p]};
            for(int i=0;i<3;i++){
                if(i==0&&post.LampSafe||i==1&&post.gateClosed&&post.latchSeated||i==2&&post.bundleStowed)continue;
                float distance=Vector3.Distance(Mission.Player.position,props[i].position);if(distance<2.3f||distance>22)continue;
                var point=camera.WorldToScreenPoint(props[i].position+Vector3.up*(i==1?1.7f:i==0?.65f:.5f));if(point.z<=0)continue;
                var box=new Rect(point.x*1600/Screen.width-115,(Screen.height-point.y)*900/Screen.height-45,230,48);
                if(box.x<0||box.xMax>1600||box.y<45||box.yMax>880)continue;
                Fill(box,new Color(.035f,.055f,.035f,.9f));Frame(box,DayGold,1);
                GUI.Label(box,(i==0?"ĐÈN CHỤP CHE":i==1?"CỔNG CHỐT":"LỐI ĐI GÁC")+$" · {distance:0} m",StoreText(18,DayInk,FontStyle.Bold,TextAnchor.MiddleCenter));
            }
        }
    }
}
