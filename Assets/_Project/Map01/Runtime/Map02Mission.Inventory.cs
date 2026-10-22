using System.Linq;
using UnityEngine;
using ShadowVale.Gameplay.Combat;
namespace ShadowVale.Map01
{
    public sealed partial class Map02Mission
    {
        int bagSelection=-1,bagPage;Texture2D bagIcons;
        static string BagName(string id)=>id=="m2_medicine_sau"?"Thuốc cho ông Sáu":id=="m2_medicine_post"?"Thuốc tổ gác":ForestInventory.Name(id);
        void BagIcon(string id,Rect area)
        {
            if(id.StartsWith("m2_medicine_"))id="medkit_small";
            var icon=Resources.Load<Texture2D>("Hud/ItemIcons/"+id);if(icon!=null){GUI.DrawTexture(area,icon,ScaleMode.ScaleToFit);return;}
            bagIcons??=Resources.Load<Texture2D>("Hud/Items");int index=ForestInventory.IconIndex(id);
            if(index>=0&&bagIcons!=null){float x=23+index%7*218.3f,y=index<7?179:614;GUI.DrawTextureWithTexCoords(area,bagIcons,new Rect(x/1536f,1-(y+218)/1024f,190/1536f,218/1024f));}
            else GUI.Label(area,ForestInventory.Name(id),new GUIStyle(small){alignment=TextAnchor.MiddleCenter,fontSize=16});
        }
        void DrawInventoryPanel()
        {
            // Use Map 1's panel, item atlas, 6x4 bag, equipment cards and detail column.
            HudPlate(new Rect(180,178,315,540));HudPlate(new Rect(510,178,600,540));HudPlate(new Rect(1125,178,300,540));
            GUI.Label(new Rect(200,195,280,40),"TRANG BỊ",heading);GUI.Label(new Rect(530,195,560,40),"TÚI ĐỒ · 6 × 4",heading);
            string[] equipment={RifleItemId,"knife","stone"};string[] labels={"1 · SÚNG CHÍNH","2 · DAO","ĐÁ ĐÁNH LẠC HƯỚNG"};
            for(int i=0;i<3;i++)
            {
                var r=new Rect(200,246+i*142,275,130);GUI.Box(r,GUIContent.none);GUI.Label(new Rect(r.x+12,r.y+7,r.width-24,32),labels[i],small);BagIcon(equipment[i],new Rect(r.x+12,r.y+42,85,75));GUI.Label(new Rect(r.x+105,r.y+47,158,72),i==2?"Số lượng: "+Stones:ForestInventory.Name(equipment[i]),small);
            }
            var stacks=ForestInventory.Split(Inventory,id=>id.StartsWith("ammo_")?90:10);if(Chapter!=null&&Chapter.State.phase<=1){if((Chapter.State.dayOne.medicineMask&1)==0)stacks.Add(new ForestInventory.Stack("m2_medicine_sau",1));if((Chapter.State.dayOne.medicineMask&2)==0)stacks.Add(new ForestInventory.Stack("m2_medicine_post",1));}int pages=Mathf.Max(1,Mathf.CeilToInt(stacks.Count/24f));bagPage=Mathf.Clamp(bagPage,0,pages-1);
            for(int i=0;i<24;i++)
            {
                int index=bagPage*24+i;var r=new Rect(530+i%6*92,250+i/6*98,86,90);var tint=GUI.color;GUI.color=index==bagSelection?new Color(1,.81f,.4f):new Color(.53f,.49f,.35f);GUI.Box(r,GUIContent.none);GUI.color=tint;
                if(index>=stacks.Count)continue;var stack=stacks[index];BagIcon(stack.Id,new Rect(r.x+7,r.y+5,72,61));GUI.Label(new Rect(r.x+6,r.y+64,74,23),"× "+stack.Count,small);
                if(GUI.Button(r,GUIContent.none,GUIStyle.none))bagSelection=index;
            }
            if(pages>1){if(GUI.Button(new Rect(530,660,80,35),"◀"))bagPage=Mathf.Max(0,bagPage-1);GUI.Label(new Rect(630,662,250,30),$"Trang {bagPage+1}/{pages}",small);if(GUI.Button(new Rect(990,660,80,35),"▶"))bagPage=Mathf.Min(pages-1,bagPage+1);}
            GUI.Label(new Rect(1145,195,260,40),"CHI TIẾT",heading);
            if(bagSelection>=0&&bagSelection<stacks.Count)
            {
                var item=stacks[bagSelection];BagIcon(item.Id,new Rect(1200,250,140,130));GUI.Label(new Rect(1145,395,260,60),BagName(item.Id),body);GUI.Label(new Rect(1145,465,260,145),item.Id.StartsWith("m2_medicine_")?"Gói thuốc dành riêng cho nhiệm vụ. Trao cho đúng người; không dùng hồi máu hoặc vứt bỏ.":(item.Id=="knife"?"Dao dùng cận chiến. Nhấn 2 để trang bị.":item.Id=="medkit_small"?"Băng cứu thương mang theo.":ForestInventory.Description(item.Id)),small);
                var kind=item.Id=="knife"?WeaponKind.Knife:item.Id==RifleItemId?WeaponKind.Rifle:WeaponKind.Unarmed;
                if(string.IsNullOrEmpty(Chapter?.State.carry)&&Chapter?.Companion==null&&kind!=WeaponKind.Unarmed&&combat.WeaponFor(kind)!=null&&GUI.Button(new Rect(1145,630,260,48),"TRANG BỊ")){combat.Equip(kind);CloseGameplayPanel();}
            }
            else GUI.Label(new Rect(1145,255,260,110),"Chọn vật phẩm để xem thông tin.",body);
            if(Chapter!=null&&Chapter.State.phase<=1)GUI.Label(new Rect(205,765,1180,70),Chapter.ReservedMedicine,small);
        }
    }
}
