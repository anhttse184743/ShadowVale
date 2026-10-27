using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ShadowVale.Map01
{
    /// <summary>Only Day 1/Night 1. Coordinates and puzzle choices are campaign save data.</summary>
    [Serializable] public sealed class Map02DayOneState
    {
        [Serializable] public sealed class Placement { public int x=-1,y=-1; public bool rotated; public float offset; }
        [Serializable] public sealed class Post
        {
            public int faults,inspected; public float lampAngle; public bool gateClosed,latchSeated,bundleStowed;
            public bool LampSafe=>Mathf.Abs(Mathf.DeltaAngle(lampAngle,180))<=25;
            public bool Safe=>LampSafe&&gateClosed&&latchSeated&&bundleStowed;
            public string Problems=>string.Join(" · ",new[]{LampSafe?null:"ánh đèn lọt ra ngoài",gateClosed&&latchSeated?null:"cửa rào chưa cài chắc",bundleStowed?null:"lối đi chưa thông"}.Where(s=>s!=null));
        }
        public int carriedWeapon=-1;
        public int schema=1,seed,wetPattern,stakeHits,medicineMask,postsReached;
        public bool initialized,stockCredited,duckSeen,arrivalSeen;
        public Placement[] crates,boards;
        public int[] boardOrder;
        public Post[] posts;
        public static Vector2Int Size(int i,bool rotated){var s=new[]{new Vector2Int(3,2),new Vector2Int(2,2),new Vector2Int(1,3)}[i];return rotated?new Vector2Int(s.y,s.x):s;}
        public static Vector2Int[] Wet(int pattern)=>pattern switch {0=>new[]{new Vector2Int(4,3),new Vector2Int(5,3)},1=>new[]{new Vector2Int(2,3),new Vector2Int(3,3)},_=>new[]{new Vector2Int(4,0),new Vector2Int(5,0)}};
        public void Initialize(int value)
        {
            if(initialized)return; seed=value;var random=new System.Random(value);wetPattern=random.Next(3);
            crates=Enumerable.Range(0,3).Select(_=>new Placement()).ToArray();boards=Enumerable.Range(0,3).Select(_=>new Placement()).ToArray();
            boardOrder=Enumerable.Range(0,5).OrderBy(_=>random.Next()).ToArray();
            int[] masks={3,5,6};int mask=masks[random.Next(masks.Length)];
            posts=new[]{MakePost(1),MakePost(mask)};initialized=true;
        }
        static Post MakePost(int mask)=>new Post{faults=mask,lampAngle=(mask&1)!=0?0:180,gateClosed=true,latchSeated=(mask&2)==0,bundleStowed=(mask&4)==0};
        public string PlacementProblem(int index,int x,int y,bool rotated)
        {
            if(index<0||index>2)return "Chọn một thùng trước.";
            var size=Size(index,rotated);
            if(x<0||y<0||x+size.x>6||y+size.y>4)return "Thùng vượt ra ngoài nền kho.";
            if(x<2)return "Đang chắn lối.";
            for(int a=x;a<x+size.x;a++)for(int b=y;b<y+size.y;b++)
            {
                if(Wet(wetPattern).Contains(new Vector2Int(a,b)))return "Nền này bị ẩm.";
                for(int i=0;i<3;i++)if(i!=index&&crates[i].x>=0){var p=crates[i];var s=Size(i,p.rotated);if(a>=p.x&&a<p.x+s.x&&b>=p.y&&b<p.y+s.y)return "Hai thùng đang chồng nhau.";}
            }
            return null;
        }
        public bool PlaceCrate(int index,int x,int y,bool rotated,out string reason)
        {
            reason=PlacementProblem(index,x,y,rotated);if(reason!=null)return false;
            crates[index]=new Placement{x=x,y=y,rotated=rotated};return true;
        }
        public bool Packed=>crates!=null&&Enumerable.Range(0,3).All(i=>crates[i].x>=0&&PlacementProblem(i,crates[i].x,crates[i].y,crates[i].rotated)==null);
        public void ResetCrates(){foreach(var p in crates){p.x=p.y=-1;p.rotated=false;}}
        public static float SweepSeconds(int stake)=>stake==0?1.4f:1.25f;
        public static float ZoneWidth(int stake)=>stake==0?.22f:.18f;
        public bool Strike(float needle)
        {
            if(stakeHits>=6||Mathf.Abs(needle-.5f)>ZoneWidth(stakeHits/3)*.5f)return false;
            stakeHits++;return true;
        }
        // A plank spans the two rails; quarter turns are intentionally invalid across the gap.
        public string BoardProblem(int board,int slot,bool rotated)
        {
            if(board==3)return "Tấm này bị nứt, không chịu được tải.";
            if(board==4)return "Tấm này không đủ dài để tựa hai đầu.";
            if(board<0||board>4||slot<0||slot>2)return "Chọn tấm ván và vị trí đặt.";
            if(rotated)return "Một đầu chưa có chỗ đỡ.";
            if(boards[slot].x>=0&&boards[slot].x!=board)return "Bậc này đã có ván. Chọn bậc còn trống.";
            if(boards.Where((p,i)=>i!=slot).Any(p=>p.x==board))return "Tấm ván này đã được kê.";
            return null;
        }
        public bool LayBoard(int board,int slot,bool rotated,out string reason)=>LayBoard(board,slot,rotated,0,out reason);
        public bool LayBoard(int board,int slot,bool rotated,float offset,out string reason)
        {reason=BoardProblem(board,slot,rotated);if(reason!=null)return false;if(!float.IsFinite(offset)||Mathf.Abs(offset)>.07f){reason="Một đầu chưa có chỗ đỡ. Căn lại hai đầu trên thanh đỡ.";return false;}boards[slot]=new Placement{x=board,y=slot,rotated=rotated,offset=offset};return true;}
        public int BoardCount=>boards?.Count(p=>p.x>=0)??0;
        public bool DockReady=>stakeHits==6&&BoardCount==3;
        public bool GiveMedicine(int packet){if(packet<0||packet>1)throw new ArgumentOutOfRangeException(nameof(packet));int bit=1<<packet;if((medicineMask&bit)!=0)return false;medicineMask|=bit;return true;}
        public void Validate()
        {
            if(!initialized)return;
            if(schema!=1||wetPattern<0||wetPattern>2||stakeHits<0||stakeHits>6||medicineMask<0||medicineMask>3||crates==null||crates.Length!=3||boards==null||boards.Length!=3||posts==null||posts.Length!=2||boardOrder==null||!boardOrder.OrderBy(x=>x).SequenceEqual(Enumerable.Range(0,5)))throw new System.IO.InvalidDataException("Dữ liệu hoạt động Ngày 1 bị hỏng.");
            if(crates.Any(p=>p==null)||boards.Any(p=>p==null)||posts.Any(p=>p==null||!float.IsFinite(p.lampAngle)))throw new System.IO.InvalidDataException("Thiếu chi tiết kho, bến hoặc chốt.");
            for(int i=0;i<3;i++){var p=crates[i];if(p.x!=-1&&PlacementProblem(i,p.x,p.y,p.rotated)!=null)throw new System.IO.InvalidDataException("Thùng trong save không hợp lệ.");var b=boards[i];if(b.x!=-1&&(BoardProblem(b.x,i,b.rotated)!=null||!float.IsFinite(b.offset)||Mathf.Abs(b.offset)>.07f))throw new System.IO.InvalidDataException("Ván trong save không hợp lệ.");}
        }
    }
}
