using UnityEngine;
namespace ShadowVale.Map01 {
 public sealed partial class Map02ChapterDirector {
  bool postHelp;
  string PostPartName(int part)=>new[]{"ĐÈN DẦU","CỬA RÀO NHỎ","LỐI ĐI GÁC"}[part];
  void TurnPostLamp(float degrees){Day.posts[postIndex].lampAngle=Mathf.Repeat(Day.posts[postIndex].lampAngle+degrees,360);SyncDayProps();}
  void ClosePostGate(){Day.posts[postIndex].gateClosed=true;dayFeedback="Cửa đã khép sát khung. Kiểm tra thanh chốt trước khi xác nhận.";SyncDayProps();}
  void LatchPostGate(){var p=Day.posts[postIndex];if(!p.gateClosed){dayFeedback="Chưa cài được: khép cửa sát khung trước.";return;}p.latchSeated=true;dayFeedback="Thanh chốt đã vào ngàm. Bấm XÁC NHẬN để kết thúc kiểm tra cửa.";SyncDayProps();}
  void StowPostBundle(){Day.posts[postIndex].bundleStowed=true;SyncDayProps();dayFeedback="Bó củi đã vào giá. Bấm XÁC NHẬN, rồi kiểm tra các hạng mục còn lại.";}
  void DrawPostPanel(){
   var p=Day.posts[postIndex];int part=dayActivity==DayActivity.Lamp?0:dayActivity==DayActivity.Gate?1:2;
   var paper=new Color(.92f,.88f,.74f);var dark=new Color(.07f,.1f,.08f,.98f);var gold=new Color(.86f,.7f,.36f);
   DockBox(new Rect(80,65,1440,775),new Color(.035f,.05f,.04f,.98f));
   GUI.Label(new Rect(110,90,1180,45),"KIỂM TRA CHỐT · "+(postIndex==0?"BỜ TRE":"ĐẦU LÀNG"),DockText(30,true));
   if(DayButton(new Rect(1320,91,175,44),"Đóng [Esc]")){RequestCloseDay();return;}
   GUI.Label(new Rect(112,140,1330,38),"Quan sát đồ vật → điều chỉnh → xác nhận. Đóng bảng để đi tới hạng mục khác.",DockText(21));
   for(int i=0;i<3;i++){
    var card=new Rect(112+i*455,188,440,78);DockBox(card,i==part?new Color(.22f,.25f,.13f):dark);
    GUI.Label(new Rect(card.x+14,card.y+7,412,30),(i+1)+". "+PostPartName(i),DockText(22,true));
    string status=i==part?"▶ ĐANG XEM":(p.inspected&(1<<i))!=0?"Đã kiểm tra · tới gần để xem lại":"Chưa kiểm tra · tới gần, nhấn E";
    GUI.Label(new Rect(card.x+14,card.y+41,412,27),status,DockText(17));
   }
   DockBox(new Rect(112,286,825,390),dark);GUI.Label(new Rect(135,302,775,34),"SƠ ĐỒ · "+PostPartName(part),DockText(21,true));
   if(part==0){
    GUI.Label(new Rect(300,343,430,30),"▲ NGOÀI LÀNG · tránh lọt sáng",new GUIStyle(DockText(20,true)){alignment=TextAnchor.MiddleCenter});
    GUI.Label(new Rect(300,628,430,28),"▼ TRONG LÀNG · đường tuần",new GUIStyle(DockText(20,true)){alignment=TextAnchor.MiddleCenter});
    Fill(new Rect(170,386,705,2),new Color(.5f,.4f,.27f));Fill(new Rect(170,611,705,2),new Color(.5f,.4f,.27f));
    var centre=new Vector2(520,500);var old=GUI.matrix;GUI.matrix=LampBeamMatrix(old,centre,p.lampAngle);
    for(int i=0;i<12;i++)Fill(new Rect(centre.x+18+i*8,centre.y-8-i*3,9,16+i*6),new Color(1,.74f,.25f,.16f+i*.022f));
    GUI.Label(new Rect(centre.x+112,centre.y-20,48,40),"▶",DockText(30,true));GUI.matrix=old;
    DockBox(new Rect(501,481,38,38),new Color(.5f,.39f,.2f));
    GUI.Label(new Rect(160,565,200,30),"ĐÈN + CHỤP CHE",DockText(18));
    GUI.Label(new Rect(970,300,505,92),"Xoay chụp, quan sát vệt sáng. Giữ ánh sáng về phía trong làng.",DockText(24,true));
    if(DayButton(new Rect(970,417,240,65),"◀ Xoay trái [A]"))TurnPostLamp(-15);
    if(DayButton(new Rect(1230,417,245,65),"Xoay phải [D] ▶"))TurnPostLamp(15);
    GUI.Label(new Rect(970,500,505,60),"Chuột: mỗi bấm xoay một nấc.\nBàn phím: giữ A / D để xoay liên tục.",DockText(19));
   }else if(part==1){
    Fill(new Rect(315,375,22,220),gold);Fill(new Rect(760,375,22,220),gold);
    var door=new Rect(340,398,p.gateClosed?414:230,180);DockBox(door,new Color(.27f,.2f,.11f));
    for(int i=1;i<5;i++)Fill(new Rect(door.x+door.width*i/5,door.y,5,door.height),gold);
    DockBox(new Rect(729,461,48,38),new Color(.45f,.45f,.39f));Fill(new Rect(p.latchSeated?641:540,471,130,17),paper);
    GUI.Label(new Rect(355,607,510,40),p.gateClosed?"CỬA ĐÃ KHÉP SÁT KHUNG":"CỬA ĐANG HÉ · còn khe hở",DockText(20,true));
    GUI.Label(new Rect(672,347,210,32),"NGÀM GIỮ CHỐT",DockText(18));
    GUI.Label(new Rect(970,300,505,95),"Khép cánh cửa trước, rồi trượt thanh chốt vào ngàm. Đây là cửa rào cạnh chốt.",DockText(24,true));
    if(DayButton(new Rect(970,417,505,58),"1. KHÉP CỬA SÁT KHUNG [A]"))ClosePostGate();
    if(DayButton(new Rect(970,488,505,58),"2. CÀI THANH CHỐT [D]"))LatchPostGate();
    GUI.Label(new Rect(970,558,505,52),p.latchSeated?"Thanh chốt: đã vào ngàm.":"Thanh chốt: chưa vào ngàm.",DockText(20));
   }else{
    Fill(new Rect(410,350,190,290),new Color(.28f,.25f,.17f));GUI.Label(new Rect(412,358,185,40),"LỐI ĐI GÁC",DockText(19,true));
    DockBox(new Rect(686,410,175,95),new Color(.27f,.2f,.11f));GUI.Label(new Rect(680,516,200,65),"GIÁ CẤT\ntrong chốt",DockText(20,true));
    DockBox(new Rect(180,410,175,95),new Color(.18f,.23f,.17f));GUI.Label(new Rect(175,516,225,80),"CẠNH CỬA\ncần chừa khoảng mở",DockText(18));
    Vector2 at=p.bundleStowed?new Vector2(711,433):new Vector2(442,466);
    for(int i=0;i<4;i++)DockBox(new Rect(at.x+i*23,at.y,17,55),new Color(.51f,.35f,.17f));
    GUI.Label(new Rect(970,300,505,100),"Chuyển bó củi khỏi lối đi. Chỗ cất phải chừa cả khoảng mở cửa rào.",DockText(24,true));
    if(DayButton(new Rect(970,417,505,58),"ĐẶT CẠNH CỬA [1]"))dayFeedback="Vị trí này chắn khoảng mở cửa. Hãy chọn chỗ khác.";
    if(DayButton(new Rect(970,488,505,58),"CẤT VÀO GIÁ TRONG CHỐT [2]"))StowPostBundle();
   }
   if(DayButton(new Rect(970,609,505,67),"XÁC NHẬN KIỂM TRA [E]")){ConfirmPostPart();if(!DayActivityActive)return;}
   GUI.Label(new Rect(115,694,1360,59),dayInputReady?(string.IsNullOrEmpty(dayFeedback)?"Điều chỉnh xong, bấm XÁC NHẬN. Nếu chưa an toàn, bảng sẽ nêu lý do để bạn sửa tiếp.":dayFeedback):"Nhả phím / chuột mở bảng để bắt đầu.",DockText(21,true));
   if(DayButton(new Rect(112,769,370,43),postHelp?"Ẩn hướng dẫn thêm":"Nhắc lại cần kiểm tra gì?"))postHelp=!postHelp;
   GUI.Label(new Rect(507,766,965,58),postHelp?"Đèn hướng vào làng · cửa khép và cài chốt · lối gác thông. Sau đó trở lại "+(postIndex==0?"tổ gác":"anh Lực")+" tại ghim vàng để báo cáo.":"Sửa xong các điểm chưa an toàn → trở lại "+(postIndex==0?"tổ gác":"anh Lực")+" ở ghim vàng. Không phải đi sang chốt khác ngay.",DockText(18));
  }
 }
}
