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
                int n=State.Count(t.id);
                if(t.id=="D2_02")return "Ra cầu theo lối đồng trống phía bắc hoặc vòng bờ tre phía nam. Có thể né tổ tuần tra; không bắt buộc hạ địch.";
                if(t.id=="D2_03")return "Theo dấu chân và vết máu từ cầu tới Tâm. Có thể né hoặc xử lý tổ tuần tra 3 lính.";
                if(t.kind=="patrolReport")return "Ở đầu cầu, nhấn E báo đàm. Sau đó vẫn đi tự do; nghe Hùng chỉ lúc đèn quay tới và lúc đã qua. C/Ctrl hạ thấp người, dùng vật che hoặc vòng bờ mương.";
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
                if(t.kind=="defend"||t.kind=="coverFire")
                {
                    if(State.activeTask!=t.id)return "Tới khu vực ghim vàng, nhấn E bắt đầu giữ tuyến.";
                    if(t.kind=="coverFire"&&State.taskProgress>=t.seconds&&Vector3.Distance(roles["Hùng"].transform.position,ChargePoint)>=2)return "Hùng đang vòng qua vật che. Tiếp tục yểm trợ tới khi cậu ấy áp sát xe tăng.";
                    if(t.kind=="coverFire")return $"Yểm trợ Hùng: {Mathf.Max(0,t.seconds-State.taskProgress):0} giây còn lại. Có thể xoay camera và dùng súng.";
                    int alive=Enemies.Count(e=>e!=null&&e.group==t.id&&!e.Dead);
                    if(!State.combatGroups.Contains(t.id))return "Địch đang tiến vào tuyến. Giữ khu vực ghim vàng.";
                    if(alive>0)return $"Còn {alive} lính trong tốp chặn đường. Đẩy lùi chúng để đoàn dân đi qua.";
                    if(Vector3.Distance(Mission.Player.position,markers[t.id].transform.position)>12)return "Tốp địch đã bị đẩy lùi. Trở lại khu vực ghim vàng để bảo vệ đoàn dân.";
                    return $"Đang cho dân qua tuyến: {Mathf.Clamp01(State.taskProgress/t.seconds)*100:0}% · còn {Mathf.Max(0,t.seconds-State.taskProgress):0} giây.";
                }
                if(t.kind=="repair")return $"E bắt đầu sửa · đã xong {n}/{t.count} phần. Nhấn Space đúng nhịp; sai được thử lại.";
                if(t.kind=="antenna")return "Tới cây cạnh trạm, E bắt đầu · giữ W leo, S xuống · A/D căn tín hiệu ổn định 2 giây · giữ Space cột dây rồi xuống đất.";
                if(t.kind=="clear"||t.kind=="demolition")return "E bắt đầu · giữ Space để thao tác, Esc hủy.";
                if(t.kind=="reach")return "Đi tới ghim vàng. Tự xác nhận khi vào đúng khu vực.";
                return "Tới ghim vàng, nhấn E để nói chuyện hoặc tương tác.";
            }
        }
        public Map02ChapterActor Companion=>escort==null&&State.activeTask=="N3_08"?roles["Hùng"]:secondaryEscort!=null&&escort!=null&&Vector3.Distance(secondaryEscort.transform.position,Mission.Player.position)>Vector3.Distance(escort.transform.position,Mission.Player.position)?secondaryEscort:escort;
        public bool ShowInteractPrompt(Map02ObjectiveObject marker)
        {
            var task=Map02ChapterDefinition.Load().Find(marker.chapterId);if(task==null)return true;
            if(task.kind=="reach"||task.kind=="crawl"||task.kind=="binocular")return false;
            if(task.kind=="inspectRoute"&&NightTwoRouteProgress<3)return false;
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
            if(t.kind=="inspectDoor")return "Kiểm tra chốt cửa hầm còn kín";
            if(t.kind=="inspectRoute")return NightTwoRouteProgress<3?$"Kiểm tra {NightRouteNames[NightTwoRouteProgress]} ({NightTwoRouteProgress}/4)":"Xác nhận bậc bến chắc chắn (3/4)";
            if(t.kind=="wire")return State.wireStage==1?"Giao cuộn dây ăng-ten cho Hùng":"Lấy cuộn dây ăng-ten trong kho kín";
            if(t.kind=="carry")return !string.IsNullOrEmpty(State.carry)&&State.activeTask==t.id?$"Đặt thùng {n+1}/{t.count} tại {(t.id=="D1_04"?"kho kín":"chỗ trú bến nam")}":$"Nhận thùng {n+1}/{t.count} tại {(t.id=="D1_04"?"bến cập":"kho kín")}";
            if(t.kind=="families")return $"Dặn dò hộ dân {n+1}/{t.count}";
            if(escort!=null&&State.activeTask==t.id)return $"Dẫn {escort.role} tới {(t.kind.StartsWith("rescue")?"bến nam":t.kind=="escortScout"?"điểm báo cáo gặp anh Thành":t.kind=="escortHung"?"nhà chị Lành":t.site=="gate"?"chốt đầu làng":t.site=="rest"?"chỗ nghỉ":"chốt bờ tre")}";
            return t.title;
        }
    }
}
