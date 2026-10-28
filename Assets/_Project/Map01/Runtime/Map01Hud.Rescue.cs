using System.Linq;
using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>The rescue's layer of the HUD: Hùng's health while he is in the line of fire, and
    /// the panel when he falls.</summary>
    public sealed partial class Map01Hud
    {
        private Map01Rescue rescue;
        private Texture2D takedownBadge,takedownKnife,takedownLeaf;
        private GUIStyle takedownHint;
        private Map01EnemyController hintGuard;
        private Transform hintHead;
        private static readonly string[] SquadLeft=new string[5];
        private void DrawTakedownHint(float width,float height)
        {
            // Nothing here takes input: the search for a guard runs once a frame, not once per GUI event.
            if(Event.current.type!=EventType.Repaint)return;
            if(rescue==null)rescue=GetComponent<Map01Rescue>();
            Map01EnemyController target; bool ready;
            // Without the rescue (the tutorial) the mission finds the guard itself.
            if(rescue!=null?!rescue.GetTakedownHint(out target,out ready):!mission.GetTakedownHint(3.2f,out target,out ready))return;
            if(target!=hintGuard){
                hintGuard=target;var actor=target.GetComponentInChildren<Animator>();
                hintHead=actor!=null?actor.GetBoneTransform(HumanBodyBones.Head):null;
            }
            var head=hintHead;
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
                takedownLeaf=MakeTexture(64,(u,v)=>{
                    var p=new Vector2((u-.5f)*1.3f,(v-.5f)*.85f);
                    return p.magnitude<.35f&&Mathf.Abs(p.x+p.y)<.2f?Color.white:Color.clear;
                });
            }
            var ambush=GetComponent<Map01BushTakedown>();bool bush=ambush!=null&&(ambush.Qualifies(target,out _)||!ready&&ambush.OffersPullHint(target));
            var tint=ready?new Color(.65f,1f,.36f):new Color(.58f,.61f,.53f,.8f);
            var centre=new Vector2(Mathf.Clamp(view.x*width,130,width-130),Mathf.Clamp((1-view.y)*height,92,height-150));
            var badge=new Rect(centre.x-29,centre.y-29,58,58);
            var old=GUI.color;GUI.color=tint;GUI.DrawTexture(badge,takedownBadge);GUI.DrawTexture(badge,takedownKnife);GUI.color=old;
            if(bush){GUI.color=tint;GUI.DrawTexture(new Rect(centre.x+14,centre.y-24,24,28),takedownLeaf);GUI.color=old;}
            var label=new Rect(centre.x-130,centre.y+31,260,27);
            HudFill(label,new Color(.025f,.04f,.02f,.8f));takedownHint.normal.textColor=tint;
            GUI.Label(label,ready?(bush?"[CHUỘT TRÁI] KÉO VÀO BỤI":"[CHUỘT TRÁI] ĐÂM SAU LƯNG"):target.Engaged?"ĐỊCH ĐÃ PHÁT HIỆN":bush?"KÉO VÀO BỤI · ÁP SÁT BÊN HÔNG":"VÒNG RA SAU · ĐẾN GẦN",takedownHint);
        }

        // Every stage's supporting notes are laid out inside DrawObjective, never as detached plates.
        private string ObjectiveNotes()
        {
            if (quest == null) return null;
            rescue ??= GetComponent<Map01Rescue>();
            var notes = new System.Text.StringBuilder();
            if (rescue != null && quest.Stage == Map01Quest.RescueStage && !rescue.Failed)
            {
                int alive = Mathf.Clamp(rescue.Remaining, 0, SquadLeft.Length - 1);
                notes.AppendLine(SquadLeft[alive] ??= $"LÍNH CÒN LẠI: {alive}/4");
                notes.Append(!rescue.OverseerAlive ? "Đã hạ lính giám sát — có thể giao chiến" : rescue.CombatAlarm
                    ? "Báo động — địch đang truy kích Nam" : rescue.CanExecuteCaptive
                    ? "Giám sát đang canh Hùng — giữ im lặng" : "Giám sát đã rời Hùng — vòng ra sau");
            }
            return notes.Length > 0 ? notes.ToString() : null;
        }

        private bool ShowObjectiveEscortHealth => rescue != null && quest != null &&
            quest.Stage == Map01Quest.EscortStage && rescue.Exposed && !rescue.HungDown;

        private void DrawObjectiveEscortHealth(float y)
        {
            ShadowLabel(new Rect(49, y, 292, 24), "HÙNG", objectiveSmall);
            var bar = new Rect(49, y + 25, 292, 10);
            float alpha = GUI.color.a;
            HudFill(bar, new Color(0, 0, 0, .6f * alpha));
            float left = Mathf.Clamp01(rescue.HungHealth / Mathf.Max(1f, rescue.HungMaxHealth));
            var tint = Time.time - rescue.HungHitAt < .25f ? Color.white : Color.Lerp(Warning, new Color(.45f, .75f, .35f), left);
            tint.a *= alpha;
            HudFill(new Rect(bar.x, bar.y, bar.width * left, bar.height), tint);
        }

        private void DrawRescue(float width)
        {
            rescue ??= GetComponent<Map01Rescue>();
            if (rescue == null || !rescue.HungDown || mission.InventoryOpen || mission.MapOpen) return;
            if (Event.current.type != EventType.Repaint) return;
            // Failure/retry remains immediately visible, independent of the timed objective panel.
            HudPanel(new Rect(width / 2 - 300, 320, 600, 185));
            GUI.Label(new Rect(width / 2 - 275, 345, 550, 75), "HÙNG ĐÃ HY SINH", hudCenter);
            GUI.Label(new Rect(width / 2 - 270, 415, 540, 30), rescue.FailureReason ?? "Giải cứu thất bại.", hudSmall);
            GUI.Label(new Rect(width / 2 - 270, 453, 540, 50), "Enter Thử lại checkpoint · F9 Tải bản lưu · Esc Menu", hudSmall);
        }
    }
}
