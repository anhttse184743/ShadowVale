using System;
using System.Collections.Generic;
using System.Linq;

namespace ShadowVale.Map01
{
    public enum Map01OperationResult { Success, Failure, Cancelled }

    [Serializable] public sealed class Map01LockGuess
    {
        public int code, exact, misplaced;
    }

    /// <summary>Pure rules, independent of rendering, frame rate and input devices.</summary>
    public sealed class Map01SupplyLock
    {
        public const int Attempts = 6;
        public readonly List<Map01LockGuess> History = new();
        public int Secret { get; }
        public int AttemptStart { get; private set; }
        public int Remaining => Math.Max(0, Attempts - (History.Count - AttemptStart));
        public bool Solved => History.Any(g => g.exact == 3);
        public Map01SupplyLock(int secret, IEnumerable<Map01LockGuess> history = null, int attemptStart = 0)
        {
            Secret = secret;
            if (history != null) History.AddRange(history.Where(g => g != null));
            AttemptStart = Math.Clamp(attemptStart, 0, History.Count);
        }
        public static int Code(int a, int b, int c) => a * 100 + b * 10 + c;
        public static int[] Digits(int code) => new[] { code / 100 % 10, code / 10 % 10, code % 10 };
        public static Map01LockGuess Score(int secret, int guess)
        {
            var a=Digits(secret); var b=Digits(guess); int exact=0, shared=0;
            for(int i=0;i<3;i++) { if(a[i]>5||b[i]>5||a[i]<0||b[i]<0) throw new ArgumentOutOfRangeException(nameof(guess)); if(a[i]==b[i])exact++; }
            for(int value=0;value<6;value++)shared+=Math.Min(a.Count(v=>v==value),b.Count(v=>v==value));
            return new Map01LockGuess { code=guess,exact=exact,misplaced=shared-exact };
        }
        public bool Submit(int code)
        {
            if(Solved||Remaining==0)return false;
            var row=Score(Secret,code); History.Add(row); return row.exact==3;
        }
        public void Retry() { if(!Solved)AttemptStart=History.Count; }
        public static string Feedback(Map01LockGuess guess)
        {
            if(guess==null)return "Chưa kiểm tra mã. Chọn ba số rồi nhấn KIỂM TRA MÃ.";
            if(guess.exact==3)return "Đúng cả 3 số và vị trí. Khóa đã mở.";
            if(guess.exact+guess.misplaced==0)return "Không có số nào đúng. Cả 3 số đều không thuộc mã.";
            return $"{guess.exact} số đúng vị trí.\n{guess.misplaced} số đúng nhưng sai vị trí.\n{3-guess.exact-guess.misplaced} số không khớp (kể cả số lặp thừa).";
        }
    }

    /// <summary>N/E/S/W are bits 0/1/2/3. Only mutually connected sockets conduct.</summary>
    public sealed class Map01RadioCircuit
    {
        public const int North=1, East=2, South=4, West=8;
        public const float Seconds=90;
        public const int Columns=5, Rows=4, Count=Columns*Rows, Source=0, Destination=19;
        // One continuous cable uses every cell. There are no decoy or alarm branches.
        static readonly int[] CablePath={0,5,10,15,16,11,6,1,2,7,12,17,18,13,8,3,4,9,14,19};
        public static readonly int[] SolvedMasks=BuildCable();
        public readonly int[] Rotations=new int[Count];
        static int[] BuildCable()
        {
            var masks=new int[Count];masks[Source]|=West;masks[Destination]|=East;
            for(int i=1;i<CablePath.Length;i++){
                int a=CablePath[i-1],b=CablePath[i],direction=b==a+1?1:b==a-1?3:b>a?2:0;
                masks[a]|=1<<direction;masks[b]|=1<<((direction+2)%4);
            }return masks;
        }
        public float Remaining=Seconds;
        public Map01RadioCircuit(int seed,int[] rotations=null,float remaining=Seconds)
        {
            var random=new Random(seed);
            // Old 16-cell saves keep mission progress but begin this redesigned puzzle anew.
            for(int i=0;i<Count;i++)Rotations[i]=rotations!=null&&rotations.Length==Count?((rotations[i]%4)+4)%4:random.Next(4);
            Remaining=Math.Clamp(remaining,0,Seconds);
        }
        public static int RotateMask(int mask,int turns)
        {
            for(int i=0;i<((turns%4)+4)%4;i++)mask=((mask<<1)&15)|((mask>>3)&1);
            return mask;
        }
        public int Mask(int index)=>RotateMask(SolvedMasks[index],Rotations[index]);
        public void Rotate(int index,int delta=1) { if(index<0||index>=Count)throw new ArgumentOutOfRangeException(nameof(index));Rotations[index]=(Rotations[index]+delta+4)%4; }
        public bool[] Powered()
        {
            var seen=new bool[Count]; if((Mask(Source)&West)==0)return seen;
            var queue=new Queue<int>();queue.Enqueue(Source);seen[Source]=true;
            while(queue.Count>0){int at=queue.Dequeue();for(int d=0;d<4;d++){
                int next=Neighbour(at,d);if(next<0||seen[next]||(Mask(at)&(1<<d))==0||(Mask(next)&(1<<((d+2)%4)))==0)continue;
                seen[next]=true;queue.Enqueue(next);
            }}return seen;
        }
        static int Neighbour(int at,int d)
        {
            int x=at%Columns,y=at/Columns; if(d==0)y--;else if(d==1)x++;else if(d==2)y++;else x--;
            return x<0||x>=Columns||y<0||y>=Rows?-1:y*Columns+x;
        }
        public bool Solved()
        {
            var seen=Powered();if(seen.Any(on=>!on)||(Mask(Destination)&East)==0)return false;
            for(int at=0;at<Count;at++)if(seen[at])for(int d=0;d<4;d++)if((Mask(at)&(1<<d))!=0){
                if(at==Source&&d==3||at==Destination&&d==1)continue;
                int next=Neighbour(at,d);if(next<0||(Mask(next)&(1<<((d+2)%4)))==0)return false;
            }return true;
        }
        public int ConnectedCount=>Powered().Count(on=>on);
        public string Feedback=>Solved()?"Đã nối đủ 20/20 đoạn. Có thể NGẮT ĐÀI.":
            ConnectedCount==0?"Đầu NGUỒN chưa khớp. Bắt đầu từ ô A1 ở góc trên trái.":
            $"Nguồn đang tới {ConnectedCount}/20 đoạn. Lần theo dây sáng, xoay đoạn kế tiếp để nối tiếp.";
        public void Tick(float seconds){Remaining=Math.Max(0,Remaining-Math.Max(0,seconds));}
    }
}
