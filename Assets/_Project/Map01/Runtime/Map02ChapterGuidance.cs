using System.Linq;
using ShadowVale.Gameplay.Combat;
using UnityEngine;
namespace ShadowVale.Map01
{
    public sealed partial class Map02ChapterDirector
    {
        public Map02ChapterTask TrackedTask=>State.TrackedTask;
        public Map02ObjectiveObject TrackedMarker=>TrackedTask!=null&&markers.TryGetValue(TrackedTask.id,out var mark)?mark:null;
        public bool Track(string id)=>State.Track(id);
        public string Purpose=>TrackedTask?.purpose??"";
        public string Instruction
        {
            get
            {
                var t=TrackedTask;if(t==null)return State.Objective;
                EnsureDayState();var dayText=DayInstruction(t);if(dayText!=null)return dayText;
                int n=State.Count(t.id);
                if(t.id=="D1_01")return "Tới chốt đầu làng gặp anh Lực, nhấn E đối mật khẩu. Ghim vàng chỉ đúng người cần gặp.";
                if(t.id=="D1_03")return "Đến nhà chị Lành, nhấn E trao thuốc cho ông Sáu. Việc này, cất thùng và sửa bến có thể làm theo thứ tự tùy ý.";
                if(t.id=="D1_06")return "Ba việc đã xong. Gặp anh Thành và nhấn E báo cáo để chuyển sang Đêm 1.";
                if(t.id=="N1_01")return "Tới hiên nhà chị Lành, nhấn E ngồi ăn cùng Hùng. Sau bữa cơm mới bắt đầu ca gác.";
                if(t.id=="N1_04")return "Tới chiếu nghỉ, nhấn E đổi ca. Cảnh nghỉ kết thúc mới sang Ngày 2.";
                if(t.id=="D2_03")return "Theo lối lúa bị giẫm từ cầu tới khoảng trống. Tâm nằm bị thương tại ghim vàng; đến gần, nhấn E để giúp.";
                if(t.kind=="inspectDoor")return "Tới ghim trước nắp hầm kho kín, nhìn vào chốt rồi nhấn E kiểm tra. Đến gần thôi chưa tính hoàn tất.";
                if(t.kind=="inspectRoute")return NightTwoRouteProgress<3?$"Đã kiểm {NightTwoRouteProgress}/4 đoạn. Đi theo ghim kế tiếp: {NightRouteNames[NightTwoRouteProgress]}. Phải kiểm theo thứ tự; không đi tắt tới bến.":"Đã kiểm 3/4 đoạn. Tới bậc bến ở ghim vàng, nhấn E xác nhận bậc chắc chắn.";
                if(t.kind=="wire")return State.wireStage==1?"Đang mang cuộn dây. Tới trạm ở ghim vàng, nhấn E giao cho Hùng.":"Tới kho kín ở ghim vàng, nhấn E lấy cuộn dây ăng-ten.";
                if(t.kind=="carry")return !string.IsNullOrEmpty(State.carry)?$"Đang mang thùng {n+1}/{t.count}. Tới ghim vàng rồi nhấn E để đặt.":$"Đã chuyển {n}/{t.count}. Tới ghim vàng, nhấn E nhận thùng tiếp theo.";
                if(escort!=null)
                {
                    var person=Companion;float distance=Vector3.Distance(person.transform.position,Mission.Player.position);
                    return $"Dẫn {(secondaryEscort!=null?"ông Sáu và chị Lành":escort.role)} tới ghim vàng. Người đi cùng đang theo sau, cách {distance:0} m.";
                }
                if(t.kind=="escortHung"||t.kind=="escortScout"||t.kind=="patrolHung"||t.kind.StartsWith("rescue"))return "Tới ghim vàng, nhấn E đón người trước khi dẫn tới nơi an toàn.";
                if(t.kind=="families")return $"Đã dặn {n}/{t.count} hộ. Tới nhà được đánh dấu, nhấn E nói chuyện.";
                if(t.kind=="binocular")return $"Tới đầu cầu, nhìn về NAM rồi chếch trái ra cánh đồng: tổ 3 lính ở xa cầu khoảng {Vector3.Distance(sites["bridge"].position,NightPatrolPosition):0} m. Giữ F, nhìn người cầm đèn 2 giây. Một vòng quét {Layout.patrolSweepSeconds:0} giây. Tiến độ {Mathf.Clamp01(observed/t.seconds)*100:0}%.";
                if(t.kind=="crawl")return "Giữ C để bò về ghim vàng. Không cần nhấn E lần nữa.";
                if(t.kind=="defend")
                {
                    if(State.activeTask!=t.id)return "Tới khu vực ghim vàng để giữ ngõ cho dân qua.";
                    int pressing=SoldiersPressing(t.id);
                    if(!State.combatGroups.Contains(t.id))return $"Sơ tán {Evacuation}% · Địch đang tiến vào ngõ. Giữ khu vực ghim vàng.";
                    if(Vector3.Distance(Mission.Player.position,markers[t.id].transform.position)>12)return $"Sơ tán {Evacuation}% · Trở lại ngõ để che cho dân.";
                    if(pressing>0)return $"Sơ tán {Evacuation}% · Còn {pressing} lính chặn ngõ. Dân đi chậm khi còn địch.";
                    if(State.taskProgress>=1)return $"Sơ tán {Evacuation}% · Đợt địch tiếp theo đang tới.";
                    return $"Sơ tán {Evacuation}% · Ngõ đang trống. Giữ vị trí cho dân qua.";
                }
                if(t.kind=="coverFire")
                {
                    if(State.activeTask!=t.id)return "Tới bờ đất, nhấn E để yểm trợ Hùng.";
                    return $"Hùng đang bò dọc bờ mương · {Mathf.Clamp01(State.taskProgress)*100:0}% quãng đường. Bắn tốp lính sau xe tăng cho chúng cúi đầu; còn {EscortStanding} tên đang ngẩng lên.";
                }
                if(IsPatch(t))return $"E bắt đầu vá · đã xong {n}/{t.count} thuyền. Bấm chuột vào chỗ rò khi vòng ngoài thu khít; càng chuẩn càng nhanh xong.";
                if(t.kind=="repair")return $"E bắt đầu sửa · đã xong {n}/{t.count} phần. Nhấn Space đúng nhịp; sai được thử lại.";
                if(t.kind=="antenna")return "Tới cây cạnh trạm, E bắt đầu · giữ W leo, S xuống · A/D căn tín hiệu ổn định 2 giây · giữ Space cột dây rồi xuống đất.";
                if(t.kind=="demolition")return CrateInstruction(t);
                if(t.kind=="clear")return "E bắt đầu · giữ Space để thao tác, Esc hủy.";
                if(t.kind=="reach")return "Đi tới ghim vàng. Tự xác nhận khi vào đúng khu vực.";
                if(t.id=="N3_07")return "Về bờ đất cạnh bến, chỗ Hùng giữ máy.";
                return "Tới ghim vàng, nhấn E để nói chuyện hoặc tương tác.";
            }
        }
        public Map02ChapterActor Companion=>escort==null&&State.activeTask=="N3_08"?roles["Hùng"]:secondaryEscort!=null&&escort!=null&&Vector3.Distance(secondaryEscort.transform.position,Mission.Player.position)>Vector3.Distance(escort.transform.position,Mission.Player.position)?secondaryEscort:escort;
        // Bringing Tâm back (Day 2) keeps Nam armed, free to change gear and at full pace (Tài's design, chosen 23/10);
        // leading Hùng before Day 3 slings the rifle and slows Nam to a walk.
        public bool CompanionLocksGear=>Companion!=null&&Companion.role!="Tâm";
        bool LeadingWounded=>escort!=null&&State.phase<4&&escort.role!="Tâm";
        public bool ShowInteractPrompt(Map02ObjectiveObject marker)
        {
            var task=Map02ChapterDefinition.Load().Find(marker.chapterId);if(task==null)return true;
            if(task.kind=="reach"||task.kind=="crawl"||task.kind=="binocular")return false;
            if(task.kind=="inspectRoute"&&NightTwoRouteProgress<3)return false;
            if(task.id=="N3_WOUND"||task.id=="N3_07"||task.id=="N3_09"||task.kind=="defend")return false; // Run on their own.
            return State.activeTask!=task.id||!(escort!=null||task.kind=="defend"||task.kind=="coverFire");
        }
        bool ObservationVisible(Camera cam,Vector3 target)
        {
            foreach(var point in new[]{target,target+Vector3.right*2,target+Vector3.left*2})
            {
                var ray=point-cam.transform.position;
                if(!Physics.RaycastAll(cam.transform.position,ray.normalized,ray.magnitude-.2f,~0,QueryTriggerInteraction.Ignore).Any(h=>!h.transform.IsChildOf(Mission.Player)&&h.transform!=Mission.Player&&h.collider.GetComponentInParent<Health>()==null))return true;
            }
            return false;
        }
        public string Prompt(Map02ChapterTask t)
        {
            int n=State.Count(t.id);
            if(t.id=="D2_03")return "Giúp Tâm — trinh sát bị thương";
            if(t.id=="D1_04"&&n>=3)return "Sắp xếp ba thùng vào kho kín";
            if(t.kind=="inspectDoor")return "Kiểm tra chốt cửa hầm còn kín";
            if(t.kind=="inspectRoute")return NightTwoRouteProgress<3?$"Kiểm tra {NightRouteNames[NightTwoRouteProgress]} ({NightTwoRouteProgress}/4)":"Xác nhận bậc bến chắc chắn (3/4)";
            if(t.kind=="wire")return State.wireStage==1?"Giao cuộn dây ăng-ten cho Hùng":"Lấy cuộn dây ăng-ten trong kho kín";
            if(t.kind=="carry")return !string.IsNullOrEmpty(State.carry)&&State.activeTask==t.id?$"Đặt thùng {n+1}/{t.count} tại {(t.id=="D1_04"?"kho kín":"chỗ trú bến nam")}":$"Nhận thùng {n+1}/{t.count} tại {(t.id=="D1_04"?"bến cập":"kho kín")}";
            if(t.kind=="families")return $"Dặn dò hộ dân {n+1}/{t.count}";
            if(t.kind=="demolition")return CratePrompt(t);
            if(escort!=null&&State.activeTask==t.id)return $"Dẫn {escort.role} tới {(t.kind.StartsWith("rescue")?"bến nam":t.kind=="escortScout"?"chỗ anh Thành":t.kind=="escortHung"?"nhà chị Lành":t.site=="gate"?"chốt đầu làng":t.site=="rest"?"chỗ nghỉ":"chốt bờ tre")}";
            return t.title;
        }
    }
}
