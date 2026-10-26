using UnityEngine;
namespace ShadowVale.Map01
{
    public sealed partial class Map02ChapterDirector
    {
        // P4a (2026-10-25): the hidden store under the banana clump (doc), drawn as the pit seen from above. Khoa's rules stay
        // (Map02DayOneState: 6×4 floor, the way down x<2, damp cells, no stacking); only the drawing and handling change:
        // crates with their goods and true sizes, a ghost of the held crate (green, or red with the reason) before it is
        // put down, lifting a placed crate, cards for the three crates, and "Cất xong" lit only when all three are good.
        // The goods match Day 3 (Map02ChapterDressing): ammunition and medicine to the south pit, grenades to the bank.
        static readonly string[] CrateGoods={"Đạn 7,62","Thuốc","Lựu đạn"},CrateKind={"hòm lớn","hòm vuông","hòm dài"};
        static readonly Color[] CrateWood={new Color(.46f,.35f,.19f),new Color(.57f,.48f,.31f),new Color(.35f,.33f,.2f)};
        static readonly float[] CrateHeight={.34f,.32f,.24f};
        const float CellMetres=.3f;   // one floor cell of the store, in the village
        const float StoreX=160,StoreY=262,CellW=105,CellH=92;   // the 6×4 floor on the 1600×900 HUD
        static readonly Color StoreInk=new Color(.93f,.86f,.68f),StoreDim=new Color(.78f,.74f,.62f),StoreGood=new Color(.42f,.78f,.38f),StoreBad=new Color(.92f,.32f,.25f);
        Vector2 storeMouse;Texture2D storePanel;
        static Rect StoreCell(int x,int y,int w=1,int h=1)=>new Rect(StoreX+x*CellW,StoreY+(4-y-h)*CellH,w*CellW,h*CellH);
        static bool StoreCellAt(Vector2 point,out Vector2Int cell)
        {
            cell=new Vector2Int(Mathf.FloorToInt((point.x-StoreX)/CellW),3-Mathf.FloorToInt((point.y-StoreY)/CellH));
            return point.x>=StoreX&&point.y>=StoreY&&cell.x>=0&&cell.x<6&&cell.y>=0&&cell.y<4;
        }
        int CrateAt(int x,int y)
        {
            for(int i=0;i<3;i++){var p=Day.crates[i];if(p.x<0)continue;var s=Map02DayOneState.Size(i,p.rotated);if(x>=p.x&&x<p.x+s.x&&y>=p.y&&y<p.y+s.y)return i;}
            return -1;
        }
        int CratesPlaced{get{int n=0;for(int i=0;i<3;i++)if(Day.crates[i].x>=0)n++;return n;}}
        static GUIStyle StoreText(int size,Color color,FontStyle style=FontStyle.Normal,TextAnchor anchor=TextAnchor.UpperLeft)
        {var s=new GUIStyle(GUI.skin.label){fontSize=size,fontStyle=style,alignment=anchor,wordWrap=true};s.normal.textColor=color;return s;}
        static void Fill(Rect r,Color c){var t=GUI.color;GUI.color=c;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=t;}
        static void Frame(Rect r,Color c,float w=2){Fill(new Rect(r.x,r.y,r.width,w),c);Fill(new Rect(r.x,r.yMax-w,r.width,w),c);Fill(new Rect(r.x,r.y,w,r.height),c);Fill(new Rect(r.xMax-w,r.y,w,r.height),c);}
        static Rect Inset(Rect r,float d)=>new Rect(r.x+d,r.y+d,r.width-2*d,r.height-2*d);
        static string ShortReason(string problem)=>problem switch{"Nền này bị ẩm."=>"nền ẩm ✕","Đang chắn lối."=>"chắn lối xuống ✕","Hai thùng đang chồng nhau."=>"chồng thùng ✕","Thùng vượt ra ngoài nền kho."=>"ra ngoài hầm ✕",_=>problem};
        /// <summary>A crate from above: boards along its length, two battens across, the goods stencilled on it.</summary>
        static void DrawCrateTop(int crate,Rect r,float alpha,bool label=true)
        {
            Color A(Color c,float k=1)=>new Color(c.r*k,c.g*k,c.b*k,alpha);var wood=CrateWood[crate];
            Fill(Inset(r,3),A(wood));bool wide=r.width>=r.height;
            for(float s=wide?r.y+24:r.x+24;s<(wide?r.yMax-12:r.xMax-12);s+=22)Fill(wide?new Rect(r.x+5,s,r.width-10,2):new Rect(s,r.y+5,2,r.height-10),A(wood,.72f));
            foreach(float k in new[]{.16f,.84f})Fill(wide?new Rect(r.x+r.width*k-6,r.y+4,12,r.height-8):new Rect(r.x+4,r.y+r.height*k-6,r.width-8,12),A(wood,.62f));
            Frame(Inset(r,3),A(wood,.45f),2);
            if(crate==1)GUI.Label(label?new Rect(r.x,r.y+8,r.width,30):r,"✚",StoreText(label?26:20,new Color(.72f,.14f,.12f,alpha),FontStyle.Bold,label?TextAnchor.UpperCenter:TextAnchor.MiddleCenter));
            if(label)GUI.Label(Inset(r,6),CrateGoods[crate]+"\n"+Map02DayOneState.Size(crate,false).x+"×"+Map02DayOneState.Size(crate,false).y,StoreText(19,new Color(.12f,.09f,.05f,alpha),FontStyle.Bold,TextAnchor.MiddleCenter));
        }
        void DrawPacking()
        {
            GUI.Label(new Rect(115,150,1320,40),$"Đã cất {CratesPlaced}/3 hòm · giữ trống lối xuống, tránh nền ẩm, không chồng hòm lên nhau.",StoreText(22,StoreDim));
            var pit=StoreCell(0,0,6,4);
            // the pit: an earth bank round it, bamboo poles holding the sides
            Fill(new Rect(pit.x-22,pit.y-22,pit.width+44,pit.height+44),new Color(.22f,.16f,.09f));
            var bamboo=new Color(.62f,.55f,.33f);Fill(new Rect(pit.x-12,pit.y-12,pit.width+24,7),bamboo);Fill(new Rect(pit.x-12,pit.yMax+5,pit.width+24,7),bamboo);Fill(new Rect(pit.x-12,pit.y-12,7,pit.height+24),bamboo);Fill(new Rect(pit.xMax+5,pit.y-12,7,pit.height+24),bamboo);
            for(float x=pit.x+40;x<pit.xMax;x+=95){Fill(new Rect(x,pit.y-12,3,7),new Color(.42f,.37f,.2f));Fill(new Rect(x,pit.yMax+5,3,7),new Color(.42f,.37f,.2f));}
            Fill(pit,new Color(.16f,.12f,.075f));
            // the way down: one band, steps up to the hatch, named once
            var way=StoreCell(0,0,2,4);Fill(way,new Color(.3f,.25f,.17f));
            for(int s=0;s<7;s++)Fill(new Rect(way.x+12,way.y+44+s*46,way.width-24,7),new Color(.43f,.35f,.21f));
            GUI.Label(new Rect(way.x,way.y+6,way.width,30),"▲ CỬA HẦM",StoreText(18,StoreInk,FontStyle.Bold,TextAnchor.UpperCenter));
            GUI.Label(new Rect(way.x+4,way.center.y-34,way.width-8,70),"LỐI XUỐNG\nGIỮ TRỐNG",StoreText(19,StoreInk,FontStyle.Bold,TextAnchor.MiddleCenter));
            // the damp floor: a puddle, named once
            var wet=Map02DayOneState.Wet(Day.wetPattern);Rect puddle=StoreCell(wet[0].x,wet[0].y);
            foreach(var v in wet){var r=StoreCell(v.x,v.y);puddle=Rect.MinMaxRect(Mathf.Min(puddle.xMin,r.xMin),Mathf.Min(puddle.yMin,r.yMin),Mathf.Max(puddle.xMax,r.xMax),Mathf.Max(puddle.yMax,r.yMax));}
            Fill(Inset(puddle,7),new Color(.13f,.25f,.3f));Fill(Inset(puddle,20),new Color(.2f,.36f,.42f));
            GUI.Label(puddle,"≈  NỀN ẨM  ≈",StoreText(19,new Color(.75f,.9f,.95f),FontStyle.Bold,TextAnchor.MiddleCenter));
            // thin floor lines where crates may go
            for(int x=2;x<=6;x++)Fill(new Rect(StoreX+x*CellW-1,pit.y,1,pit.height),new Color(1,1,1,.09f));
            for(int y=0;y<=4;y++)Fill(new Rect(way.xMax,StoreY+y*CellH-1,pit.xMax-way.xMax,1),new Color(1,1,1,.09f));
            for(int i=0;i<3;i++){var p=Day.crates[i];if(p.x<0)continue;var s=Map02DayOneState.Size(i,p.rotated);DrawCrateTop(i,StoreCell(p.x,p.y,s.x,s.y),1);}
            // the mouse moves the cursor (the arrow keys still do)
            var e=Event.current;
            if(e.type==EventType.Repaint){if((e.mousePosition-storeMouse).sqrMagnitude>4&&StoreCellAt(e.mousePosition,out var hover))packingCursor=hover;storeMouse=e.mousePosition;}
            // the held crate's ghost where it would go: green if it may, red with the reason beside it if not
            if(selectedCrate>=0)
            {
                var size=Map02DayOneState.Size(selectedCrate,selectionRotated);var problem=Day.PlacementProblem(selectedCrate,packingCursor.x,packingCursor.y,selectionRotated);
                var ghost=StoreCell(packingCursor.x,packingCursor.y,size.x,size.y);var shown=Rect.MinMaxRect(Mathf.Max(ghost.xMin,pit.xMin),Mathf.Max(ghost.yMin,pit.yMin),Mathf.Min(ghost.xMax,pit.xMax),Mathf.Min(ghost.yMax,pit.yMax));
                DrawCrateTop(selectedCrate,shown,.55f);Fill(shown,problem==null?new Color(.3f,.8f,.35f,.24f):new Color(.9f,.25f,.2f,.3f));Frame(shown,problem==null?StoreGood:StoreBad,3);
                if(problem!=null){var tag=new Rect(Mathf.Min(shown.xMax+8,pit.xMax-190),Mathf.Max(shown.y-36,pit.y-58),190,32);Fill(tag,new Color(.1f,.05f,.04f,.92f));GUI.Label(tag,ShortReason(problem),StoreText(19,StoreBad,FontStyle.Bold,TextAnchor.MiddleCenter));}
            }
            else Frame(StoreCell(packingCursor.x,packingCursor.y),StoreInk,3);
            if(e.type==EventType.MouseDown&&StoreCellAt(e.mousePosition,out var cell))
            {
                packingCursor=cell;
                if(e.button==1)selectionRotated=!selectionRotated;
                else if(e.button==0){int under=CrateAt(cell.x,cell.y);if(under>=0&&under!=selectedCrate)LiftCrate(under);else PlacePackingSelection(cell.x,cell.y);}
                e.Use();
            }
            // the three crates
            for(int i=0;i<3;i++)
            {
                var card=new Rect(900,250+i*100,520,88);bool held=selectedCrate==i,placed=Day.crates[i].x>=0;
                Fill(card,held?new Color(.27f,.22f,.12f,.96f):new Color(.1f,.09f,.06f,.92f));Frame(card,held?StoreInk:new Color(.45f,.4f,.28f),held?3:1);
                var size=Map02DayOneState.Size(i,false);DrawCrateTop(i,new Rect(card.x+12,card.y+10,size.x*26+8,size.y*22+6),1,false);
                GUI.Label(new Rect(card.x+110,card.y+8,300,34),$"{i+1}. {CrateGoods[i]}",StoreText(23,StoreInk,FontStyle.Bold));
                GUI.Label(new Rect(card.x+110,card.y+44,300,34),$"{CrateKind[i]} · {size.x}×{size.y} ô",StoreText(19,StoreDim));
                GUI.Label(new Rect(card.x+360,card.y+8,150,72),held?"đang cầm":placed?"✓ đã cất":"chưa cất",StoreText(20,held?StoreInk:placed?StoreGood:StoreDim,FontStyle.Bold,TextAnchor.MiddleRight));
                if(GUI.Button(card,GUIContent.none,GUIStyle.none)){selectedCrate=i;selectionRotated=Day.crates[i].rotated;dayFeedback=placed?"Bấm ô khác để dời hòm này, hoặc bấm lên nó trên sơ đồ để nhấc ra.":"Rê tới chỗ đặt; bóng xanh là đặt được. Chuột phải hoặc R để xoay.";}
            }
            GUI.Label(new Rect(900,552,520,92),"Chuột: bấm ô để đặt · bấm hòm để nhấc · chuột phải xoay\nPhím: 1–3 chọn hòm · mũi tên dời · E đặt · R xoay\nBackspace nhấc hòm ở ô đang chọn",StoreText(18,StoreDim));
            var enabled=GUI.enabled;GUI.enabled=Day.Packed;
            if(DayButton(new Rect(900,652,520,58),$"Cất xong · Enter ({CratesPlaced}/3)"))CompletePacking();
            GUI.enabled=enabled;
        }
        void LiftCrate(int crate)
        {
            var was=Day.crates[crate];selectedCrate=crate;selectionRotated=was.rotated;Day.crates[crate]=new Map02DayOneState.Placement();SyncDayProps();
            dayFeedback=$"Đã nhấc hòm {CrateGoods[crate].ToLowerInvariant()} ra. Bấm ô mới để đặt lại.";
        }
        void PlacePackingSelection(int x,int y)
        {
            if(Day.PlaceCrate(selectedCrate,x,y,selectionRotated,out var reason))
            {
                PlayDaySound("crate_place",.6f);dayFeedback=$"✓ Đã đặt hòm {CrateGoods[selectedCrate].ToLowerInvariant()}.";SyncDayProps();
                int next=System.Array.FindIndex(Day.crates,p=>p.x<0);if(next>=0){selectedCrate=next;selectionRotated=Day.crates[next].rotated;dayFeedback+=$" Tiếp: hòm {CrateGoods[next].ToLowerInvariant()}.";}
                else{selectedCrate=-1;dayFeedback+=" Đủ ba hòm: Enter để cất xong.";}
            }
            else{PlayDaySound("crate_wrong",.5f);dayFeedback=reason;}
        }
        /// <summary>Each village crate is built to its own floor size (one cell ≈ 0.3 m), so the one carried is the one placed.</summary>
        Transform BuildCrate(int crate,Vector3 at,Material wood,Material edge)
        {
            var s=Map02DayOneState.Size(crate,false);float w=s.x*CellMetres-.03f,d=s.y*CellMetres-.03f,h=CrateHeight[crate];
            var root=Group("Supply crate "+(crate+1)+" — "+CrateGoods[crate],at);
            Part(root,"Closed wooden body",new Vector3(0,h*.5f,0),new Vector3(w,h,d),wood);
            bool wide=w>=d;foreach(float k in new[]{-.38f,.38f})Part(root,"Binding batten",new Vector3(wide?w*k:0,h*.5f,wide?0:d*k),wide?new Vector3(.05f,h+.03f,d+.04f):new Vector3(w+.04f,h+.03f,.05f),edge);
            foreach(float side in new[]{-1f,1f})Part(root,"Rope grip",wide?new Vector3(side*(w*.5f+.02f),h*.62f,0):new Vector3(0,h*.62f,side*(d*.5f+.02f)),wide?new Vector3(.04f,.05f,.16f):new Vector3(.16f,.05f,.04f),edge);
            return root;
        }
        /// <summary>Where a crate stands in the store for its placement: the cell grid laid on the raised floor.</summary>
        Vector3 StoredCrate(int crate,Map02DayOneState.Placement p){var s=Map02DayOneState.Size(crate,p.rotated);return sites["store"].position+new Vector3(.4f+(p.x+s.x*.5f)*CellMetres,.05f,.1f+(p.y+s.y*.5f)*CellMetres);}
        /// <summary>Carried with its long side across the body, hands on the top edge.</summary>
        void HoldCrate(Transform crate,int index,Vector3 hands)
        {
            var s=Map02DayOneState.Size(index,false);bool turn=s.y>s.x;float depth=Mathf.Min(s.x,s.y)*CellMetres;var player=Mission.Player;
            crate.rotation=player.rotation*Quaternion.Euler(0,turn?90:0,0);crate.position=hands-Vector3.up*CrateHeight[index]+player.forward*(depth*.5f-.05f);
        }
    }
}
