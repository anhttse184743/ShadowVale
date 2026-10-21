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
                if(t.kind=="carry")return !string.IsNullOrEmpty(State.carry)?$"Đang mang thùng {n+1}/{t.count}. Tới ghim vàng rồi nhấn E để đặt.":$"Đã chuyển {n}/{t.count}. Tới ghim vàng, nhấn E nhận thùng tiếp theo.";
                if(escort!=null)
                {
                    var person=Companion;float distance=Vector3.Distance(person.transform.position,Mission.Player.position);
                    return distance>7?$"{person.role} đang chờ, cách {distance:0} m. Quay lại đón; ghim xanh chỉ người đi cùng.":$"Dẫn {(secondaryEscort!=null?"ông Sáu và chị Lành":escort.role)} tới ghim vàng. Người xa nhất cách {distance:0} m.";
                }
                if(t.kind=="escortHung"||t.kind=="escortScout"||t.kind=="patrolHung"||t.kind.StartsWith("rescue"))return "Tới ghim vàng, nhấn E đón người trước khi dẫn tới nơi an toàn.";
                if(t.kind=="families")return $"Đã dặn {n}/{t.count} hộ. Tới nhà được đánh dấu, nhấn E nói chuyện.";
                if(t.kind=="binocular")return $"Tới cầu · F mở/đóng ống nhòm. Nhìn về ánh đèn phía nam cầu, giữ yên 2 giây. Tiến độ {Mathf.Clamp01(observed/t.seconds)*100:0}%.";
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
                    if(State.activeTask!=t.id)return "Tới khu vực ghim vàng, nhấn E bắt đầu yểm trợ.";
                    if(State.taskProgress>=t.seconds&&Vector3.Distance(roles["Hùng"].transform.position,ChargePoint)>=2)return "Hùng đang vòng qua vật che. Tiếp tục yểm trợ tới khi cậu ấy áp sát xe tăng.";
                    return $"Yểm trợ Hùng: {Mathf.Max(0,t.seconds-State.taskProgress):0} giây còn lại. Có thể xoay camera và dùng súng.";
                }
                if(t.kind=="repair")return $"E bắt đầu sửa · đã xong {n}/{t.count} phần. Nhấn Space đúng nhịp; sai được thử lại.";
                if(t.kind=="antenna")return "E thao tác đài · A/D căn tín hiệu, giữ ổn định 2 giây.";
                if(t.kind=="clear"||t.kind=="demolition")return "E bắt đầu · giữ Space để thao tác, Esc hủy.";
                if(t.kind=="reach")return "Đi tới ghim vàng. Tự xác nhận khi vào đúng khu vực.";
                if(t.id=="N3_07")return "Về bờ đất cạnh bến, chỗ Hùng giữ máy.";
                return "Tới ghim vàng, nhấn E để nói chuyện hoặc tương tác.";
            }
        }
        public Map02ChapterActor Companion=>escort==null&&State.activeTask=="N3_08"?roles["Hùng"]:secondaryEscort!=null&&escort!=null&&Vector3.Distance(secondaryEscort.transform.position,Mission.Player.position)>Vector3.Distance(escort.transform.position,Mission.Player.position)?secondaryEscort:escort;
        public bool ShowInteractPrompt(Map02ObjectiveObject marker)
        {
            var task=Map02ChapterDefinition.Load().Find(marker.chapterId);if(task==null)return true;
            if(task.kind=="reach"||task.kind=="crawl"||task.kind=="binocular")return false;
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
            if(t.kind=="carry")return !string.IsNullOrEmpty(State.carry)&&State.activeTask==t.id?$"Đặt thùng {n+1}/{t.count} tại {(t.id=="D1_04"?"kho kín":"chỗ trú bến nam")}":$"Nhận thùng {n+1}/{t.count} tại {(t.id=="D1_04"?"bến cập":"kho kín")}";
            if(t.kind=="families")return $"Dặn dò hộ dân {n+1}/{t.count}";
            if(escort!=null&&State.activeTask==t.id)return $"Dẫn {escort.role} tới {(t.kind.StartsWith("rescue")?"bến nam":t.kind=="escortScout"?"chỗ anh Thành":t.kind=="escortHung"?"nhà chị Lành":t.site=="gate"?"chốt đầu làng":t.site=="rest"?"chỗ nghỉ":"chốt bờ tre")}";
            return t.title;
        }
    }
}
