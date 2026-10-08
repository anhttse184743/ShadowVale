using System.Linq;
using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>The rescue's layer of the HUD: Hùng's health while he is in the line of fire, and
    /// the panel when he falls.</summary>
    public sealed partial class Map01Hud
    {
        private Map01Rescue rescue;
        private Texture2D takedownBadge,takedownKnife;
        private GUIStyle takedownHint;
        private void DrawTakedownHint(float width,float height)
        {
            if(rescue==null)rescue=GetComponent<Map01Rescue>();
            if(rescue==null||!rescue.GetTakedownHint(out var target,out var ready))return;
            var actor=target.GetComponentInChildren<Animator>();
            var head=actor!=null?actor.GetBoneTransform(HumanBodyBones.Head):null;
            var view=mission.gameCamera.WorldToViewportPoint(head!=null?head.position+Vector3.up*.38f:target.transform.position+Vector3.up*2.1f);
            if(view.z<=0||view.x<0||view.x>1||view.y<0||view.y>1)return;
            if(takedownBadge==null){
                takedownBadge=MakeTexture(64,(u,v)=>{
                    float d=new Vector2(u-.5f,v-.5f).magnitude*2;
                    float ring=Mathf.Clamp01(1-Mathf.Abs(d-.83f)/.055f);
                    return new Color(1,1,1,d>.95f?0:Mathf.Max(ring,.13f));
                });
                takedownKnife=MakeTexture(64,(u,v)=>{
                    var p=new Vector2(u,v);
                    bool blade=InTriangle(p,new Vector2(.31f,.8f),new Vector2(.57f,.51f),new Vector2(.44f,.47f));
                    bool hilt=InTriangle(p,new Vector2(.38f,.51f),new Vector2(.57f,.57f),new Vector2(.59f,.49f))
                        ||InTriangle(p,new Vector2(.38f,.51f),new Vector2(.59f,.49f),new Vector2(.42f,.43f));
                    bool handle=InTriangle(p,new Vector2(.44f,.48f),new Vector2(.54f,.51f),new Vector2(.65f,.26f))
                        ||InTriangle(p,new Vector2(.44f,.48f),new Vector2(.65f,.26f),new Vector2(.55f,.22f));
                    return blade||hilt||handle?Color.white:Color.clear;
                });
                takedownHint=new GUIStyle(hudSmall){fontSize=16,alignment=TextAnchor.MiddleCenter,wordWrap=false};
            }
            var tint=ready?new Color(.65f,1f,.36f):new Color(.58f,.61f,.53f,.8f);
            var centre=new Vector2(Mathf.Clamp(view.x*width,130,width-130),Mathf.Clamp((1-view.y)*height,92,height-150));
            var badge=new Rect(centre.x-29,centre.y-29,58,58);
            var old=GUI.color;GUI.color=tint;GUI.DrawTexture(badge,takedownBadge);GUI.DrawTexture(badge,takedownKnife);GUI.color=old;
            var label=new Rect(centre.x-130,centre.y+31,260,27);
            HudFill(label,new Color(.025f,.04f,.02f,.8f));takedownHint.normal.textColor=tint;
            GUI.Label(label,ready?"[CHUỘT TRÁI] KẾT LIỄU":target.Engaged?"ĐỊCH ĐÃ PHÁT HIỆN":"VÒNG RA SAU · ĐẾN GẦN",takedownHint);
        }

        private void DrawRescue(float width)
        {
            if (rescue == null) rescue = GetComponent<Map01Rescue>(); // Added by Map01Quest.Awake.
            if (rescue == null) return;
            if (mission.InventoryOpen || mission.MapOpen) return;
            if (quest.Stage == Map01Quest.RescueStage && !rescue.Failed) {
                var info = new Rect(28, objectiveBottom + 12, 370, 65);
                HudPanel(info);
                GUI.Label(new Rect(info.x+12,info.y+8,345,24), $"LÍNH CÒN LẠI: {rescue.Remaining}/4", hudKey);
                GUI.Label(new Rect(info.x+12,info.y+34,345,25), !rescue.OverseerAlive?"Đã hạ lính giám sát — có thể giao chiến":rescue.CombatAlarm?"Báo động — địch đang truy kích Nam":rescue.CanExecuteCaptive?"Giám sát đang canh Hùng — giữ im lặng":"Giám sát đã rời Hùng — vòng ra sau", hudSmall);
                return;
            }
            if (rescue.HungDown)
            {
                HudPanel(new Rect(width / 2 - 300, 320, 600, 185));
                GUI.Label(new Rect(width / 2 - 275, 345, 550, 75), "HÙNG ĐÃ HY SINH", hudCenter);
                GUI.Label(new Rect(width / 2 - 270, 415, 540, 30), rescue.FailureReason ?? "Giải cứu thất bại.", hudSmall);
                GUI.Label(new Rect(width / 2 - 270, 453, 540, 50), "Enter Thử lại checkpoint  ·  F9 Tải bản lưu  ·  Esc Menu", hudSmall);
                return;
            }
            // Only once it matters: he has been hit, or the squad is fighting.
            if (!rescue.Exposed || mission.InventoryOpen || mission.MapOpen) return;
            if (quest.Stage != Map01Quest.EscortStage) return;
            var plate = new Rect(28, objectiveBottom + 12, 370, 50);
            HudPanel(plate);
            GUI.Label(new Rect(plate.x + 12, plate.y + 12, 80, 26), "HÙNG", hudKey);
            var bar = new Rect(plate.x + 92, plate.y + 18, plate.width - 110, 14);
            HudFill(bar, new Color(0, 0, 0, .6f));
            float left = rescue.HungHealth / rescue.HungMaxHealth;
            bool justHit = Time.time - rescue.HungHitAt < .25f;
            HudFill(new Rect(bar.x, bar.y, bar.width * left, bar.height), justHit ? Color.white : Color.Lerp(Warning, new Color(.45f, .75f, .35f), left));
        }
    }
}
