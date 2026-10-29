using UnityEngine;
namespace ShadowVale.Map01 {
 public sealed partial class Map02ChapterDirector {
  GUIStyle DockText(int size=21,bool bold=false)=>new GUIStyle(GUI.skin.label){fontSize=size,fontStyle=bold?FontStyle.Bold:FontStyle.Normal,wordWrap=true,normal={textColor=new Color(.94f,.89f,.75f)}};
  void DockBox(Rect rect,Color colour){Fill(rect,colour);Frame(rect,new Color(.52f,.46f,.29f),1);}
  void DockBoardImage(Rect rect,int board,bool selected=false){
   Fill(rect,new Color(.58f,.39f,.19f));Frame(rect,new Color(.87f,.69f,.39f),selected?3:1);
   if(rect.width>rect.height){for(int i=1;i<3;i++)Fill(new Rect(rect.x+4,rect.y+rect.height*i/3,rect.width-8,1),new Color(.32f,.22f,.12f));}
   if(board==3)GUI.Label(new Rect(rect.center.x-37,rect.center.y-26,90,54),"╱╲╱",new GUIStyle(DockText(37,true)){normal={textColor=Color.black}});
  }
  void DrawDockBoardPanel(){
   var gold=new Color(.91f,.75f,.4f);var soft=new Color(.1f,.13f,.1f,.98f);var pale=new Color(.79f,.83f,.71f);
   DockBox(new Rect(80,75,1440,750),new Color(.045f,.065f,.05f,.98f));
   GUI.Label(new Rect(110,99,1160,46),"SỬA BẬC BẾN · KÊ VÁN",DockText(32,true));
   if(DayButton(new Rect(1320,101,175,44),"Đóng [Esc]")){RequestCloseDay();return;}
   GUI.Label(new Rect(115,158,1340,38),$"✓ Hai cọc đã chắc     ▶ Kê ván {Day.BoardCount}/3     → Nhờ chú Bảy thử bến",DockText(23,true));
   GUI.Label(new Rect(115,208,350,36),"① CHỌN MỘT TẤM VÁN",DockText(23,true));
   for(int i=0;i<5;i++){
    int board=Day.boardOrder[i];bool used=System.Array.Exists(Day.boards,p=>p.x==board),chosen=selectedBoard==board;var r=new Rect(115,250+i*90,350,80);
    DockBox(r,chosen?new Color(.26f,.28f,.14f):soft);if(chosen)Frame(r,gold,3);
    bool enabled=GUI.enabled;GUI.enabled=enabled&&!used&&!Day.DockReady;if(GUI.Button(r,GUIContent.none,GUIStyle.none))SelectDockBoard(i);GUI.enabled=enabled;
    GUI.Label(new Rect(r.x+15,r.y+8,170,28),$"VÁN {i+1}",DockText(22,true));
    GUI.Label(new Rect(r.x+177,r.y+8,160,28),used?"✓ ĐÃ KÊ":chosen?"ĐANG CHỌN":Day.DockReady?"KHÔNG DÙNG":"BẤM ĐỂ XEM",DockText(17,true));
    DockBoardImage(new Rect(r.x+20,r.y+47,board==4?130:300,21),board);
   }
   GUI.Label(new Rect(500,208,660,36),"② CHỌN BẬC · CĂN HAI ĐẦU VÁN",DockText(23,true));
   DockBox(new Rect(500,250,650,346),soft);
   const float centre=880,scale=300;
   for(int slot=0;slot<3;slot++){
    bool filled=Day.boards[slot].x>=0;var row=new Rect(510,267+slot*100,630,88);
    if(slot==dockSlot&&!Day.DockReady)Fill(row,new Color(.19f,.25f,.18f));
    bool enabled=GUI.enabled;GUI.enabled=enabled&&!filled;if(GUI.Button(row,GUIContent.none,GUIStyle.none))dockSlot=slot;GUI.enabled=enabled;
    GUI.Label(new Rect(520,row.y+8,125,64),$"BẬC {slot+1}\n"+(filled?"✓ Đã kê":slot==dockSlot?"▶ Hiện tại":"Bấm chọn"),DockText(19,true));
   }
   foreach(float side in new[]{-.53f,.53f}){var r=new Rect(centre+side*scale-18,271,36,291);Fill(r,new Color(.35f,.28f,.17f));Frame(r,gold,2);}
   for(int slot=0;slot<3;slot++){
    int board=Day.boards[slot].x;float offset=Day.boards[slot].offset;bool held=slot==dockSlot&&selectedBoard>=0&&board<0;
    if(held){board=selectedBoard;offset=dockOffset;}if(board<0)continue;float y=310+slot*100;
    bool turned=held&&selectionRotated;float length=(board==4?.62f:1.28f)*scale;
    var r=turned?new Rect(centre+offset*scale-13,y-65,26,130):new Rect(centre+offset*scale-length*.5f,y-13,length,26);
    DockBoardImage(r,board,held);if(!held)GUI.Label(new Rect(r.center.x-35,y-13,70,28),"✓",DockText(23,true));
   }
   foreach(float side in new[]{-.53f,.53f})GUI.Label(new Rect(centre+side*scale-100,566,200,27),"THANH ĐỠ",new GUIStyle(DockText(17,true)){alignment=TextAnchor.MiddleCenter});
   GUI.Label(new Rect(500,606,650,28),"DỊCH VÁN · hai đầu phải nằm trên hai thanh đỡ",DockText(19,true));
   bool canAdjust=GUI.enabled;GUI.enabled=canAdjust&&!Day.DockReady&&selectedBoard>=0;
   if(DayButton(new Rect(500,642,145,43),"◀ Dịch trái"))dockOffset=Mathf.Max(-.45f,dockOffset-.03f);
   dockOffset=GUI.HorizontalSlider(new Rect(670,659,310,23),dockOffset,-.45f,.45f);
   if(DayButton(new Rect(1000,642,150,43),"Dịch phải ▶"))dockOffset=Mathf.Min(.45f,dockOffset+.03f);
   GUI.enabled=canAdjust;
   GUI.Label(new Rect(500,691,650,32),"Hoặc giữ A / D để dịch · R xoay · E kiểm tra và kê",DockText(18));
   GUI.Label(new Rect(1190,212,300,38),Day.DockReady?"③ THỬ BẾN":"③ KIỂM TRA RỒI KÊ",DockText(23,true));
   string problem=selectedBoard<0?"Chọn một tấm ván bên trái. Bấm bậc còn trống trên sơ đồ để chọn nơi kê.":Day.BoardProblem(selectedBoard,dockSlot,selectionRotated)??(Mathf.Abs(dockOffset)>.07f?"Một đầu chưa tựa lên thanh đỡ. Dịch ván tới khi cả hai đầu đều được đỡ.":null);
   if(Day.DockReady)problem=null;
   string status=Day.DockReady?"✓ Đủ ba ván. Nhờ chú Bảy thử lực để hoàn tất sửa bến.":problem??"✓ Hai đầu đã được đỡ. Bấm KÊ VÁN để đặt xuống.";
   DockBox(new Rect(1180,265,320,212),soft);GUI.Label(new Rect(1195,280,290,182),status,DockText(23,true));
   GUI.enabled=canAdjust&&!Day.DockReady;
   if(DayButton(new Rect(1190,505,300,58),"XOAY VÁN [R]")){if(selectedBoard>=0)selectionRotated=!selectionRotated;else dayFeedback="Chọn một tấm ván trước.";}
   GUI.enabled=canAdjust;
   if(DayButton(new Rect(1190,585,300,84),Day.DockReady?"NHỜ CHÚ BẢY\nTHỬ BẾN [E]":"KIỂM TRA VÀ\nKÊ VÁN [E]"))ConfirmDockBoard();
   GUI.Label(new Rect(115,742,1370,60),dayInputReady?(Day.DockReady?"✓ Hai cọc và ba ván đã xong. Bấm NHỜ CHÚ BẢY THỬ BẾN để hoàn tất.":string.IsNullOrEmpty(dayFeedback)?"Chọn ván → bấm bậc trống → căn hai đầu → kê. Đóng bảng vẫn giữ phần đã làm xong.":dayFeedback):"Nhả phím/chuột mở bảng để bắt đầu thao tác.",DockText(21));
  }
 }
}
