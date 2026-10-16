using System.Linq;
using ShadowVale.Gameplay.Combat;
using UnityEngine;

namespace ShadowVale.Map01
{
    public sealed partial class Map02Mission
    {
        Texture2D hudChart, hudPanel, hudCombatIcons, hudDisc, hudHealth;
        GUIStyle hudStencil, hudCaption;
        public int AvailableObjectiveCount => targets.Count(t=>t!=null&&World.Available(t.work));
        public bool HasCheckpoint => !string.IsNullOrEmpty(checkpoint);
        public Map02ObjectiveObject TrackedObjective => targets.Where(t=>t!=null&&World.Available(t.work))
            .OrderBy(t=>(Point(t)-Player.position).sqrMagnitude).FirstOrDefault();
        static Vector3 Point(Map02ObjectiveObject target)=>target.standPoint!=null?target.standPoint.position:target.transform.position;

        System.Collections.IEnumerator PrepareHudChart()
        {
            yield return null;
            if(hudChart!=null||Player==null)yield break;
            // One snapshot of the actual village, never a second live camera every frame.
            var survey=new GameObject("Map 2 HUD survey camera");
            var surveyCamera=survey.AddComponent<Camera>();surveyCamera.enabled=false;
            survey.transform.SetPositionAndRotation(new Vector3(0,180,0),Quaternion.Euler(90,0,0));
            surveyCamera.orthographic=true;surveyCamera.orthographicSize=110;
            surveyCamera.nearClipPlane=.1f;surveyCamera.farClipPlane=260;
            surveyCamera.clearFlags=CameraClearFlags.SolidColor;surveyCamera.backgroundColor=new Color(.15f,.23f,.17f);
            surveyCamera.useOcclusionCulling=false;
            var rt=RenderTexture.GetTemporary(768,768,24);
            var previous=RenderTexture.active;
            try
            {
                surveyCamera.targetTexture=rt;surveyCamera.Render();RenderTexture.active=rt;
                hudChart=new Texture2D(768,768,TextureFormat.RGB24,false){name="Map 2 village survey",wrapMode=TextureWrapMode.Clamp};
                hudChart.ReadPixels(new Rect(0,0,768,768),0,0);hudChart.Apply();
            }
            finally{surveyCamera.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);Destroy(survey);}
        }
        void HudPlate(Rect rect)
        {
            hudPanel??=Resources.Load<Texture2D>("Hud/Panel");
            if(hudPanel!=null)GUI.DrawTexture(rect,hudPanel,ScaleMode.StretchToFill);
            else GUI.Box(rect,GUIContent.none);
        }
        void DrawGameplayHud()
        {
            hudStencil??=new GUIStyle(heading){font=Resources.Load<Font>("Menu/Fonts/BlackOpsOne-Regular")??heading.font,fontSize=23};
            hudCaption??=new GUIStyle(small){fontSize=17,wordWrap=true};
            if(hudDisc==null)
            {
                hudDisc=new Texture2D(128,128,TextureFormat.RGBA32,false);
                for(int y=0;y<128;y++)for(int x=0;x<128;x++)
                {float d=Vector2.Distance(new Vector2(x,y),new Vector2(63.5f,63.5f))/63.5f;hudDisc.SetPixel(x,y,d>.99f?Color.clear:d>.965f?new Color(.87f,.68f,.32f):new Color(.06f,.09f,.05f,.95f));}
                hudDisc.Apply();
            }
            var map=new Rect(28,24,230,230);GUI.DrawTexture(map,hudDisc);
            // Crop a 90 m north-up window around Nam; horizontal strips mask it to a disc.
            var center=new Vector2(map.center.x,map.center.y);float radius=109;
            for(float y=-radius;y<radius;y+=3)
            {
                float half=Mathf.Sqrt(Mathf.Max(0,radius*radius-(y+1.5f)*(y+1.5f)));
                var strip=new Rect(center.x-half,center.y+y,half*2,3);
                var uv=new Rect((Player.position.x+110)/220-half/radius*45/220,
                    (Player.position.z+110)/220-(y+3)/radius*45/220,half/radius*90/220,3/radius*45/220);
                if(hudChart!=null)GUI.DrawTextureWithTexCoords(strip,hudChart,uv);
            }
            GUI.Label(new Rect(map.x+88,map.y+4,60,30),"BẮC",hudCaption);
            var saved=GUI.matrix;GUIUtility.RotateAroundPivot(Player.eulerAngles.y,center);
            GUI.Label(new Rect(center.x-11,center.y-16,28,35),"▲",heading);GUI.matrix=saved;
            foreach(var target in targets.Where(t=>t!=null&&World.Available(t.work)))
            {
                var delta=Point(target)-Player.position;var pos=center+new Vector2(delta.x,-delta.z)/45*radius;
                if(Vector2.Distance(pos,center)>radius-14)pos=center+(pos-center).normalized*(radius-14);
                DrawObjectivePin(pos);
            }
            GUI.Label(new Rect(52,254,235,28),"M  MỞ BẢN ĐỒ",hudCaption);
            float plateHeight=Mathf.Max(132,70+hudCaption.CalcHeight(new GUIContent(World.Objective),300));
            HudPlate(new Rect(25,300,345,plateHeight));
            GUI.Label(new Rect(44,317,300,35),"NHIỆM VỤ",hudStencil);
            GUI.Label(new Rect(44,358,300,plateHeight-62),World.Objective,hudCaption);
            var objective=TrackedObjective;
            if(objective!=null)
            {
                float distance=Vector3.Distance(Player.position,Point(objective));
                HudPlate(new Rect(560,25,490,63));
                GUI.Label(new Rect(581,37,450,44),objective.prompt+" · "+distance.ToString("0")+" m",hudCaption);
                var cam=cameraRig!=null?cameraRig.GetComponent<Camera>():Camera.main;
                if(cam!=null)
                {
                    var screen=cam.WorldToViewportPoint(Point(objective)+Vector3.up*1.6f);
                    if(screen.z>0&&screen.x>.15f&&screen.x<.9f&&screen.y>.18f&&screen.y<.85f)
                    {var at=new Vector2(screen.x*1600,(1-screen.y)*900);DrawObjectivePin(at,16);GUI.Label(new Rect(at.x-25,at.y+13,80,30),distance.ToString("0")+" m",hudCaption);}
                }
            }
            GUI.Label(new Rect(28,310+plateHeight,350,30),HasCheckpoint?"CHECKPOINT SẴN SÀNG · Esc lưu / tải":"CHƯA CÓ CHECKPOINT",hudCaption);
            hudCombatIcons??=Resources.Load<Texture2D>("Hud/CombatIcons");
            if(hudHealth==null)
            {
                hudHealth=new Texture2D(128,128,TextureFormat.RGBA32,false);
                for(int y=0;y<128;y++)for(int x=0;x<128;x++)
                {
                    float v=(y/127f-.05f)/.9f;
                    float r=v<.35f?.44f*Mathf.Sqrt(Mathf.Max(0,1-Mathf.Pow((v-.35f)/.35f,2))):.44f*(1-Mathf.Pow((v-.35f)/.65f,1.15f));
                    hudHealth.SetPixel(x,y,v>=0&&v<=1&&Mathf.Abs(x/127f-.5f)<=r?Color.white:Color.clear);
                }
                hudHealth.Apply();
            }
            float bx=1230,by=610;var kind=combat!=null?combat.EquippedKind:WeaponKind.Unarmed;
            DrawWeaponIcon(new Rect(bx+10,by+10,146,118),new Rect(672,107,536,438),kind==WeaponKind.Rifle);
            DrawWeaponIcon(new Rect(bx+249,by+29,82,81),new Rect(96,695,491,476),kind==WeaponKind.Knife);
            DrawWeaponIcon(new Rect(bx+286,by+145,54,69),new Rect(783,724,335,429),kind==WeaponKind.Unarmed);
            GUI.Label(new Rect(bx+60,by+125,35,28),"1",hudCaption);GUI.Label(new Rect(bx+280,by+112,35,28),"2",hudCaption);GUI.Label(new Rect(bx+308,by+220,35,28),"3",hudCaption);
            var tint=GUI.color;var drop=new Rect(bx+158,by+124,118,136);
            GUI.color=new Color(.4f,.43f,.37f,.45f);GUI.DrawTexture(drop,hudHealth);
            GUI.color=new Color(.94f,.88f,.70f);float fill=.05f+.9f*Mathf.Clamp01(health.Current/health.Max);
            GUI.DrawTextureWithTexCoords(new Rect(drop.x,drop.yMax-drop.height*fill,drop.width,drop.height*fill),hudHealth,new Rect(0,0,1,fill));
            GUI.color=tint;
            string rounds=kind!=WeaponKind.Rifle?"—":combat.IsReloading?$"{combat.ReloadProgress*100:0}%":$"{combat.RoundsInMagazine}/{(Inventory.TryGetValue("ammo_rifle",out int n)?n:0)}";
            GUI.Label(new Rect(bx+10,by+170,160,44),rounds,heading);
            GUI.Label(new Rect(bx+10,by+216,165,30),kind==WeaponKind.Rifle?"BĂNG / DỰ TRỮ":"CẬN CHIẾN",hudCaption);
            GUI.Label(new Rect(bx+154,by+260,185,32),$"{health.Current:0} / {health.Max:0} HP",hudCaption);
            GUI.Label(new Rect(25,865,1100,32),"E Tương tác · Tab Túi đồ · J Nhiệm vụ · M Bản đồ · Esc Menu",hudCaption);
            if(nearest!=null){HudPlate(new Rect(435,770,730,65));GUI.Label(new Rect(457,784,685,44),"[E] "+nearest.prompt,body);}
        }
        void DrawWeaponIcon(Rect area,Rect pixels,bool active)
        {
            if(hudCombatIcons==null)return;var tint=GUI.color;
            GUI.color=active?Color.white:new Color(.59f,.61f,.59f,.8f);
            GUI.DrawTextureWithTexCoords(area,hudCombatIcons,new Rect(pixels.x/hudCombatIcons.width,1-pixels.yMax/hudCombatIcons.height,pixels.width/hudCombatIcons.width,pixels.height/hudCombatIcons.height));GUI.color=tint;
        }
        void DrawObjectivePin(Vector2 position,float size=12)
        {
            var matrix=GUI.matrix;var tint=GUI.color;GUIUtility.RotateAroundPivot(45,position);
            GUI.color=new Color(.04f,.05f,.025f,.95f);GUI.DrawTexture(new Rect(position.x-size*.5f-2,position.y-size*.5f-2,size+4,size+4),Texture2D.whiteTexture);
            GUI.color=new Color(1,.77f,.23f);GUI.DrawTexture(new Rect(position.x-size*.5f,position.y-size*.5f,size,size),Texture2D.whiteTexture);
            GUI.matrix=matrix;GUI.color=tint;
        }
    }
}
