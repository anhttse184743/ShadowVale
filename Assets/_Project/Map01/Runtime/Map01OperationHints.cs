using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>One contextual marker at the actual prop, with a readable reason when locked.</summary>
    public sealed class Map01OperationHints:MonoBehaviour
    {
        Map01Mission mission;Map01Story story;GUIStyle label,icon;
        public static Texture2D KeyIcon=>Resources.Load<Texture2D>("OperationsUI/key");
        public static Texture2D CargoIcon=>Resources.Load<Texture2D>("OperationsUI/cargo");
        void Start(){mission=GetComponent<Map01Mission>();story=GetComponent<Map01Story>();}
        void OnGUI()
        {
            if(mission==null||!mission.IsInitialized||mission.Stopped||mission.InventoryOpen||mission.MapOpen||ForestMenu.Visible||story.Layout==null)return;
            var layout=story.Layout;var p=story.Progress;
            if(mission.Stage==3){
                if(!p.keyCollected&&layout.keyKeeper!=null&&!layout.keyKeeper.Alive)
                    Hint(layout.keyKeeper.transform.position+Vector3.up*.45f,"CHÌA KHO ĐIỂM 1","[E] Lục xác để nhặt chìa",KeyIcon,true);
                else if(!p.doorUnlocked&&layout.doorApproach!=null)
                    Hint(layout.doorApproach.position+Vector3.up*1.15f,"CỬA KHO TIẾP TẾ",p.keyCollected?story.RemainingGuards(1)>0?"Hạ nhóm bảo vệ trước":"[E] Dùng chìa mở cửa":"Cần chìa từ lính gác kho",KeyIcon,p.keyCollected&&story.RemainingGuards(1)==0);
                else if(!p.supplies&&layout.chestApproach!=null)
                    Hint(layout.supply.position+Vector3.up*.85f,"HÒM THUỐC · KHÓA MÃ",p.chestUnlocked?"[E] Mở hòm và lấy hàng":"[E] Giải khóa 3 vòng số",CargoIcon,true);
            }else if(mission.Stage==5)
                Hint(layout.radio.position+Vector3.up*1.1f,"BẢNG MẠCH TRẠM ĐÀI",story.ResponsePending?"Xử lý nhóm phản ứng":story.RemainingGuards(2)>0?"Hạ nhóm bảo vệ trước":"[E] Đấu mạch để ngắt đài",null,!story.ResponsePending&&story.RemainingGuards(2)==0);
        }
        void Hint(Vector3 at,string title,string action,Texture texture,bool ready)
        {
            float distance=Vector3.Distance(at,mission.player.position);if(distance>18)return;
            var screen=mission.gameCamera.WorldToScreenPoint(at);if(screen.z<=0)return;
            if(Physics.Linecast(mission.gameCamera.transform.position,at,out var hit,mission.ObstructionMask,QueryTriggerInteraction.Ignore)&&Vector3.Distance(hit.point,at)>.8f)return;
            label??=new GUIStyle(GUI.skin.label){fontSize=15,alignment=TextAnchor.MiddleCenter,wordWrap=true};icon??=new GUIStyle(label){fontSize=23,fontStyle=FontStyle.Bold};
            float x=screen.x,y=Screen.height-screen.y;var color=GUI.color;
            GUI.color=ready?new Color(.95f,.80f,.35f,.96f):new Color(.70f,.66f,.48f,.9f);
            GUI.DrawTexture(new Rect(x-18,y-18,36,36),Texture2D.whiteTexture);GUI.color=new Color(.05f,.08f,.045f,.98f);
            GUI.DrawTexture(new Rect(x-16,y-16,32,32),Texture2D.whiteTexture);GUI.color=color;
            if(texture!=null)GUI.DrawTexture(new Rect(x-14,y-14,28,28),texture,ScaleMode.ScaleToFit);else GUI.Label(new Rect(x-15,y-16,30,30),"E",icon);
            if(distance<=7){
                GUI.color=new Color(.025f,.04f,.025f,.92f);GUI.DrawTexture(new Rect(x-150,y+22,300,55),Texture2D.whiteTexture);GUI.color=color;
                GUI.Label(new Rect(x-146,y+23,292,24),title,label);GUI.Label(new Rect(x-146,y+47,292,25),action,label);
            }GUI.color=color;
        }
    }
}
