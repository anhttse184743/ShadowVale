using System.Linq;
using UnityEngine;
namespace ShadowVale.Map01
{
    public sealed partial class Map02Mission
    {
        Texture2D explorationFog,characterMapRim,characterMapDisk,characterAlly,characterEnemy;
        readonly System.Collections.Generic.List<Map02ChapterEnemy> seenEnemies=new System.Collections.Generic.List<Map02ChapterEnemy>();
        float senseAt;
        public Vector3 CharacterSteerPoint=>route.Length>1?route[1]:TrackedObjective!=null?Point(TrackedObjective):Player.position+Player.forward;
        public string InteractionPrompt=>nearest!=null?nearest.prompt:groundItems.FirstOrDefault(g=>g!=null&&Vector3.Distance(Player.position,g.transform.position)<3) is Map02GroundItem item?"Nhặt "+ForestInventory.Name(item.id)+" ×"+item.count:Bag!=null&&Bag.NearWorkbench()&&!CharacterGearLocked?"Mở bàn chế tạo / sửa súng":null;
        public void DrawCharacterObjectives(Rect area)=>DrawObjectiveChoices(area);
        public void DrawCharacterPin(Vector2 at)=>DrawObjectivePin(at,16);
        void RefreshExplorationTexture()
        {
            if(explorationFog==null)explorationFog=new Texture2D(MapExploration.Resolution,MapExploration.Resolution,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp};
            var colors=new Color[MapExploration.Resolution*MapExploration.Resolution];
            for(int y=0;y<MapExploration.Resolution;y++)for(int x=0;x<MapExploration.Resolution;x++)colors[y*MapExploration.Resolution+x]=new Color(.015f,.025f,.015f,Exploration.Cell(x,y)?0:.96f);
            explorationFog.SetPixels(colors);explorationFog.Apply();
        }
        void SenseCharacterEnemies()
        {
            if(Time.unscaledTime<senseAt)return;senseAt=Time.unscaledTime+.25f;seenEnemies.Clear();
            foreach(var enemy in Chapter.Enemies)
            {
                if(enemy==null||enemy.Dead||!enemy.gameObject.activeInHierarchy)continue;
                var delta=enemy.transform.position-Player.position;
                if(delta.sqrMagnitude>900||Vector3.Angle(GameCamera.transform.forward,delta)>65)continue;
                var eye=Player.position+Vector3.up*1.5f;var target=enemy.transform.position+Vector3.up*1.2f;
                if(Map01Smoke.Blocks(eye,target))continue;
                if(Physics.Linecast(eye,target,out var hit,~0,QueryTriggerInteraction.Ignore)&&hit.transform!=enemy.transform&&!hit.transform.IsChildOf(enemy.transform))continue;
                seenEnemies.Add(enemy);
            }
        }
        public void DrawCharacterMap(Rect area,bool mini,GUIStyle label)
        {
            if(Exploration==null)return;SenseCharacterEnemies();
            var world=mini?new Rect(Player.position.x-45,Player.position.z-45,90,90):Exploration.Bounds;
            Vector2 Plot(Vector3 p)=>Map01Minimap.Project(p,world,area);
            bool OnMap(Vector2 p)=>area.Contains(p)&&(!mini||Vector2.Distance(p,area.center)<area.width*.46f);
            var old=GUI.color;GUI.color=new Color(.035f,.05f,.025f);if(mini){if(characterMapDisk==null)characterMapDisk=Map01Hud.MakeTexture(128,(u,v)=>Vector2.Distance(new Vector2(u,v),Vector2.one*.5f)<=.5f?Color.white:Color.clear);GUI.DrawTexture(area,characterMapDisk);}else GUI.DrawTexture(area,Texture2D.whiteTexture);GUI.color=old;
            if(explorationFog==null)RefreshExplorationTexture();
            Vector2? disk=mini?area.center:(Vector2?)null;
            if(hudChart!=null)Map01Minimap.DrawTerrainTexture(area,world,disk,hudChart,Exploration.Bounds);
            Map01Minimap.DrawTerrainTexture(area,world,disk,explorationFog,Exploration.Bounds);
            GUI.color=new Color(1,.8f,.3f,.7f);
            for(int i=1;i<route.Length;i++)for(float d=0;d<Vector3.Distance(route[i-1],route[i]);d+=3)
            {var p=Vector3.MoveTowards(route[i-1],route[i],d);var at=Plot(p);if(OnMap(at)&&IsExplored(p))GUI.DrawTexture(new Rect(at.x-1,at.y-1,3,3),Texture2D.whiteTexture);}
            GUI.color=old;
            if(characterAlly==null)characterAlly=Map01Minimap.Symbol(1);
            if(characterEnemy==null)characterEnemy=Map01Minimap.Symbol(3);
            foreach(var role in new[]{"Hùng","Anh Thành","Anh Lực","Tâm","Ông Sáu","Chị Lành","Bé Tí","Chú Bảy"})
            {
                var actor=Chapter.Actor(role);if(actor==null||actor.visual==null||!actor.visual.gameObject.activeInHierarchy||role=="Hùng"&&Chapter.State.hungDead||!IsExplored(actor.transform.position))continue;
                var at=Plot(actor.transform.position);if(!OnMap(at))continue;GUI.color=new Color(.3f,.67f,.84f);GUI.DrawTexture(new Rect(at.x-5,at.y-5,10,10),characterAlly);
            }
            foreach(var enemy in seenEnemies){var at=Plot(enemy.transform.position);if(!OnMap(at))continue;GUI.color=new Color(.85f,.22f,.13f);GUI.DrawTexture(new Rect(at.x-7,at.y-7,14,14),characterEnemy);}
            GUI.color=old;
            if(TrackedObjective!=null)
            {
                var at=Plot(Point(TrackedObjective));
                if(mini)at=area.center+Vector2.ClampMagnitude(at-area.center,area.width*.43f);
                DrawObjectivePin(at,mini?12:16);
                GUI.Label(new Rect(Mathf.Clamp(at.x-20,area.x,area.xMax-70),Mathf.Clamp(at.y+10,area.y+30,area.yMax-25),75,24),Vector3.Distance(Player.position,Point(TrackedObjective)).ToString("0")+" m",label);
            }
            DrawNamArrow(Plot(Player.position),22);
            if(mini)
            {
                if(characterMapRim==null)characterMapRim=Map01Hud.MakeTexture(192,(u,v)=>{float d=Vector2.Distance(new Vector2(u,v),Vector2.one*.5f)*2;return new Color(.78f,.66f,.38f,Mathf.Clamp01(1-Mathf.Abs(d-.97f)/.025f));});
                GUI.DrawTexture(new Rect(area.x-3,area.y-3,area.width+6,area.height+6),characterMapRim);
            }
            GUI.Label(new Rect(area.center.x-35,area.y+6,80,24),"BẮC",label);GUI.color=old;
        }
        public void DrawCharacterAwareness(float width,float height,GUIStyle label)
        {
            if(Chapter.State.phase==3&&Chapter.State.Done("N2_02")&&!Chapter.State.Done("N2_05"))
                GUI.Label(new Rect(width/2-280,105,560,35),Chapter.State.patrolWarnings>0?"ĐÈN ĐANG TÌM · Đã bị phát hiện 1/2 lần — tìm vật che":"Giữ kín tuyến · Nấp bờ mương, tránh đèn quét",label);
            foreach(var enemy in seenEnemies.Where(e=>e.Alerted||e.Investigating))
            {var p=GameCamera.WorldToViewportPoint(enemy.transform.position+Vector3.up*2.4f);if(p.z>0)CharacterHud.DrawCharacterEye(new Vector2(p.x*width,(1-p.y)*height),enemy.Alerted);}
        }
    }
}
