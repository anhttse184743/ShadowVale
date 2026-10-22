using System.Linq;
using ShadowVale.Gameplay.Combat;
using UnityEngine;

namespace ShadowVale.Map01
{
    public sealed partial class Map02Mission
    {
        Texture2D hudChart, hudArrow, hudPanel, hudCombatIcons, hudHealth;
        GUIStyle hudStencil, hudCaption;
        public int AvailableObjectiveCount => targets.Count(t=>t!=null&&ObjectiveAvailable(t));
        public bool HasCheckpoint => !string.IsNullOrEmpty(checkpoint);
        public Map02ObjectiveObject TrackedObjective => Chapter!=null?Chapter.TrackedMarker:targets.Where(t=>t!=null&&ObjectiveAvailable(t)).OrderBy(t=>t.saveId).FirstOrDefault();
        static Vector3 Point(Map02ObjectiveObject target)=>target.standPoint!=null?target.standPoint.position:target.transform.position;

        System.Collections.IEnumerator PrepareHudChart()
        {
            yield return null;
            if(hudChart!=null||Player==null)yield break;
            // One snapshot of the actual village, never a second live camera every frame.
            var survey=new GameObject("Map 2 HUD survey camera");
            var surveyCamera=survey.AddComponent<Camera>();surveyCamera.enabled=false;
            survey.transform.SetPositionAndRotation(new Vector3(0,180,0),Quaternion.Euler(90,0,0));
            surveyCamera.orthographic=true;surveyCamera.orthographicSize=Map02Navigation.HalfSize;
            surveyCamera.nearClipPlane=.1f;surveyCamera.farClipPlane=260;
            surveyCamera.clearFlags=CameraClearFlags.SolidColor;surveyCamera.backgroundColor=new Color(.15f,.23f,.17f);
            surveyCamera.useOcclusionCulling=false;
            var rt=RenderTexture.GetTemporary(768,768,24);
            var previous=RenderTexture.active;
            var sun=FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l=>l.type==LightType.Directional);float sunBefore=sun!=null?sun.intensity:0;var ambient=RenderSettings.ambientLight;
            var actors=FindObjectsByType<Map02ChapterActor>(FindObjectsSortMode.None).SelectMany(a=>a.GetComponentsInChildren<Renderer>()).Concat(Player.GetComponentsInChildren<Renderer>()).Distinct().Where(r=>r.enabled).ToArray();
            foreach(var actor in actors)actor.enabled=false;
            try
            {
                if(sun!=null)sun.intensity=1;RenderSettings.ambientLight=Color.gray;
                surveyCamera.targetTexture=rt;surveyCamera.Render();RenderTexture.active=rt;
                hudChart=new Texture2D(768,768,TextureFormat.RGB24,false){name="Map 2 village survey",wrapMode=TextureWrapMode.Clamp};
                hudChart.ReadPixels(new Rect(0,0,768,768),0,0);hudChart.Apply();

            }
            finally{if(sun!=null)sun.intensity=sunBefore;RenderSettings.ambientLight=ambient;foreach(var actor in actors)if(actor!=null)actor.enabled=true;surveyCamera.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);Destroy(survey);}
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
            var map=new Rect(28,24,230,230);GUI.Box(map,GUIContent.none);
            // Match the complete square survey: radial clamping would move corner landmarks away from their actual position.

            if(hudChart!=null)GUI.DrawTexture(map,hudChart);
            DrawRoute(map,false);
            GUI.Label(new Rect(map.x+88,map.y+4,100,30),"BẮC ↑",hudCaption);
            var objective=TrackedObjective;
            if(objective!=null)DrawObjectivePin(Map02Navigation.Plot(Point(objective),map));
            if(Chapter?.Companion!=null)
            {
                var at=Map02Navigation.Plot(Chapter.Companion.transform.position,map);var companionTint=GUI.color;GUI.color=new Color(.4f,.85f,1);GUI.DrawTexture(new Rect(at.x-4,at.y-4,8,8),Texture2D.whiteTexture);GUI.color=companionTint;
            }
            DrawNamArrow(Map02Navigation.Plot(Player.position,map),18);
            GUI.Label(new Rect(52,254,235,28),"M  MỞ BẢN ĐỒ",hudCaption);
            string title=Chapter?.TrackedTask?.title??ObjectiveText;
            string instruction=Chapter?.Instruction??"Tới ghim vàng để tương tác.";
            float plateHeight=Mathf.Max(184,100+hudCaption.CalcHeight(new GUIContent(title+"\n\n"+instruction),300));
            HudPlate(new Rect(25,300,345,plateHeight));
            GUI.Label(new Rect(44,317,300,35),"VIỆC ĐANG THEO DÕI",hudStencil);
            GUI.Label(new Rect(44,357,300,plateHeight-75),title+"\n\n"+instruction,hudCaption);
            if(Chapter!=null){var phaseTint=GUI.color;GUI.color=new Color(0,0,0,.65f);GUI.DrawTexture(new Rect(560,20,990,38),Texture2D.whiteTexture);GUI.color=phaseTint;GUI.Label(new Rect(574,23,960,34),Map02ChapterState.PhaseName(Chapter.State.phase)+(Chapter.State.phase==5?" · Sơ tán "+Chapter.State.evacuation+"%":""),hudCaption);}
            if(objective!=null)
            {
                float distance=Vector3.Distance(Player.position,Point(objective));
                HudPlate(new Rect(560,65,490,78));
                GUI.Label(new Rect(581,77,450,58),objective.prompt+" · "+distance.ToString("0")+" m",hudCaption);
                var cam=cameraRig!=null?cameraRig.GetComponent<Camera>():Camera.main;
                if(cam!=null)
                {
                    var screen=cam.WorldToViewportPoint(Point(objective)+Vector3.up*1.6f);
                    if(screen.z>0&&screen.x>.15f&&screen.x<.9f&&screen.y>.18f&&screen.y<.85f)
                    {var at=new Vector2(screen.x*1600,(1-screen.y)*900);DrawObjectivePin(at,16);GUI.Label(new Rect(at.x-25,at.y+13,80,30),distance.ToString("0")+" m",hudCaption);}
                }
            }
            GUI.Label(new Rect(28,310+plateHeight,350,50),"J chọn việc / xem hướng dẫn · M bản đồ",hudCaption);
            GUI.Label(new Rect(28,355+plateHeight,350,50),HasCheckpoint?"Đã có checkpoint · Esc lưu / tải":"Chưa có checkpoint",hudCaption);
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
            if (RifleAmmo == Map01M16.Ammo)
                Map01RifleHud.DrawM16(new Rect(bx+10,by+10,146,118),kind==WeaponKind.Rifle);
            else DrawWeaponIcon(new Rect(bx+10,by+10,146,118),new Rect(672,107,536,438),kind==WeaponKind.Rifle);
            DrawWeaponIcon(new Rect(bx+249,by+29,82,81),new Rect(96,695,491,476),kind==WeaponKind.Knife);
            DrawWeaponIcon(new Rect(bx+286,by+145,54,69),new Rect(783,724,335,429),kind==WeaponKind.Unarmed);
            GUI.Label(new Rect(bx+60,by+125,35,28),"1",hudCaption);GUI.Label(new Rect(bx+280,by+112,35,28),"2",hudCaption);GUI.Label(new Rect(bx+308,by+220,35,28),"3",hudCaption);
            var tint=GUI.color;var drop=new Rect(bx+158,by+124,118,136);
            GUI.color=new Color(.4f,.43f,.37f,.45f);GUI.DrawTexture(drop,hudHealth);
            GUI.color=new Color(.94f,.88f,.70f);float fill=.05f+.9f*Mathf.Clamp01(health.Current/health.Max);
            GUI.DrawTextureWithTexCoords(new Rect(drop.x,drop.yMax-drop.height*fill,drop.width,drop.height*fill),hudHealth,new Rect(0,0,1,fill));
            GUI.color=tint;
            string rounds=kind!=WeaponKind.Rifle?"—":combat.IsReloading?$"{combat.ReloadProgress*100:0}%":$"{combat.RoundsInMagazine}/{(Inventory.TryGetValue(RifleAmmo,out int n)?n:0)}";
            GUI.Label(new Rect(bx+10,by+170,160,44),rounds,heading);
            GUI.Label(new Rect(bx+10,by+216,165,30),kind==WeaponKind.Rifle?"BĂNG / DỰ TRỮ":"CẬN CHIẾN",hudCaption);
            GUI.Label(new Rect(bx+154,by+260,185,32),$"{health.Current:0} / {health.Max:0} HP",hudCaption);
            GUI.Label(new Rect(25,865,1100,32),"E Tương tác · Tab Túi đồ · J Nhiệm vụ · M Bản đồ · Esc Menu",hudCaption);
            if(nearest!=null){HudPlate(new Rect(435,770,730,65));GUI.Label(new Rect(457,784,685,44),"[E] "+nearest.prompt,body);}
        }
        void DrawNamArrow(Vector2 at,float size)
        {
            if(hudArrow==null){hudArrow=new Texture2D(32,32,TextureFormat.RGBA32,false);for(int y=0;y<32;y++)for(int x=0;x<32;x++)hudArrow.SetPixel(x,y,Mathf.Abs(x-15.5f)<(31-y)*.45f?Color.white:Color.clear);hudArrow.Apply();}
            var matrix=GUI.matrix;var color=GUI.color;GUI.matrix=Map02Navigation.RotateUi(matrix,at,Player.eulerAngles.y);GUI.color=new Color(.65f,.95f,1);GUI.DrawTexture(new Rect(at.x-size/2,at.y-size/2,size,size),hudArrow);GUI.color=color;GUI.matrix=matrix;
        }
        void DrawObjectiveChoices(Rect area)
        {
            if(Chapter==null)return;
            var rows=Chapter.State.AvailableTasks.ToArray();float y=area.y;
            foreach(var task in rows)
            {
                bool selected=Chapter.TrackedTask?.id==task.id;
                if(GUI.Button(new Rect(area.x,y,area.width,66),(selected?"◆ ĐANG THEO DÕI · ":"Theo dõi · ")+task.title, new GUIStyle(GUI.skin.button){fontSize=20,wordWrap=true}))
                {if(!Chapter.Track(task.id))Say("Hoàn tất việc đang mang hoặc hộ tống trước khi đổi mục tiêu.");}
                GUI.Label(new Rect(area.x+8,y+70,area.width-16,65),task.purpose??"",small);y+=145;
            }
            if(rows.Length==0)GUI.Label(area,Chapter.State.Objective,body);
        }
        void DrawWeaponIcon(Rect area,Rect pixels,bool active)
        {
            if(hudCombatIcons==null)return;var tint=GUI.color;
            GUI.color=active?Color.white:new Color(.59f,.61f,.59f,.8f);
            GUI.DrawTextureWithTexCoords(area,hudCombatIcons,new Rect(pixels.x/hudCombatIcons.width,1-pixels.yMax/hudCombatIcons.height,pixels.width/hudCombatIcons.width,pixels.height/hudCombatIcons.height));GUI.color=tint;
        }
        void DrawObjectivePin(Vector2 position,float size=12)
        {
            var matrix=GUI.matrix;var tint=GUI.color;GUI.matrix=Map02Navigation.RotateUi(matrix,position,45);
            GUI.color=new Color(.04f,.05f,.025f,.95f);GUI.DrawTexture(new Rect(position.x-size*.5f-2,position.y-size*.5f-2,size+4,size+4),Texture2D.whiteTexture);
            GUI.color=new Color(1,.77f,.23f);GUI.DrawTexture(new Rect(position.x-size*.5f,position.y-size*.5f,size,size),Texture2D.whiteTexture);
            GUI.matrix=matrix;GUI.color=tint;
        }
    }
}
