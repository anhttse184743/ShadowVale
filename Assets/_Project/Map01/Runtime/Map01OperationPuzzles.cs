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
        public static int GenerateCode(int seed){var random=new Random(seed);return Code(random.Next(6),random.Next(6),random.Next(6));}
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
        public const float Seconds=10;
        public const int Columns=5, Rows=4, Count=Columns*Rows, Source=0, Destination=19;
        static readonly int[] FallbackPath={0,5,10,15,16,11,6,1,2,7,12,17,18,13,8,3,4,9,14,19};
        public readonly int[] Rotations=new int[Count];
        public readonly bool[] InitiallyScrambled=new bool[Count];
        public int[] SolvedMasks {get;private set;}
        public int Round {get;private set;}
        public int TotalRounds {get;}
        public int ScrambledCount=>InitiallyScrambled.Count(on=>on);
        public float Remaining=Seconds;
        readonly int seed;
        public Map01RadioCircuit(int seed,int[] rotations=null,float remaining=Seconds,int round=0)
        {
            this.seed=seed;TotalRounds=2+new Random(seed).Next(2);
            Round=Math.Clamp(round,0,TotalRounds-1);BuildRound(rotations);
            Remaining=Math.Clamp(remaining,0,Seconds);
        }
        void BuildRound(int[] restored=null)
        {
            var random=new Random(unchecked(seed+Round*104729));
            var path=new int[Count];var used=new bool[Count];int budget=12000;
            bool Search(int at,int depth)
            {
                if(--budget<0)return false;
                path[depth]=at;used[at]=true;
                if(depth==Count-1){if(at==Destination)return true;used[at]=false;return false;}
                var directions=new[]{0,1,2,3};
                for(int i=3;i>0;i--){int j=random.Next(i+1);(directions[i],directions[j])=(directions[j],directions[i]);}
                foreach(int d in directions){
                    int next=Neighbour(at,d);
                    if(next<0||used[next]||next==Destination&&depth!=Count-2)continue;
                    if(Search(next,depth+1))return true;
                }
                used[at]=false;return false;
            }
            if(!Search(Source,0))path=(int[])FallbackPath.Clone();
            SolvedMasks=new int[Count];SolvedMasks[Source]=West;SolvedMasks[Destination]=East;
            for(int i=1;i<Count;i++){
                int a=path[i-1],b=path[i],d=b==a+1?1:b==a-1?3:b>a?2:0;
                SolvedMasks[a]|=1<<d;SolvedMasks[b]|=1<<((d+2)%4);
            }
            Array.Clear(Rotations,0,Count);Array.Clear(InitiallyScrambled,0,Count);
            // Scramble a subset without exposing it to the UI. Every cell remains
            // rotatable, including correctly oriented cells the player may disturb.
            var choices=Enumerable.Range(0,Count).ToArray();
            for(int i=Count-1;i>0;i--){int j=random.Next(i+1);(choices[i],choices[j])=(choices[j],choices[i]);}
            for(int i=0;i<4+Round;i++){int at=choices[i];InitiallyScrambled[at]=true;Rotations[at]=random.Next(2)==0?1:3;}
            if(restored!=null&&restored.Length==Count)
                for(int i=0;i<Count;i++)Rotations[i]=((restored[i]%4)+4)%4;
            Remaining=Seconds;
        }
        public bool AdvanceRound()
        {
            if(Remaining<=0||!Solved()||Round+1>=TotalRounds)return false;
            Round++;BuildRound();return true;
        }
        public static int RotateMask(int mask,int turns)
        {
            for(int i=0;i<((turns%4)+4)%4;i++)mask=((mask<<1)&15)|((mask>>3)&1);
            return mask;
        }
        public int Mask(int index)=>RotateMask(SolvedMasks[index],Rotations[index]);
        public void Rotate(int index,int delta=1) {
            if(index<0||index>=Count)throw new ArgumentOutOfRangeException(nameof(index));
            Rotations[index]=((Rotations[index]+delta)%4+4)%4;
        }
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
        public string Feedback=>Solved()?"Dây đã liền mạch. Nhấn XÁC NHẬN VÒNG.":
            "Tự lần mạch từ NGUỒN đến NGẮT ĐÀI. Mọi ô đều xoay được; dây sáng biểu thị phần đã có điện.";
        public void Tick(float seconds){Remaining=Math.Max(0,Remaining-Math.Max(0,seconds));}
    }
    /// <summary>Three fuse pulls. The moving needle is a game timing signal, not an electrical simulation.</summary>
    public sealed class Map01PowerSabotage
    {
        public const int Steps=3;
        public const float Seconds=10,Window=.18f;
        readonly int seed;
        float age;
        public int Step {get;private set;}
        public float Remaining {get;private set;}=Seconds;
        public bool Complete {get;private set;}
        public bool Won=>Complete&&Step==Steps;
        public float Center {get;private set;}
        public float Needle=>.5f+.5f*(float)Math.Sin(age*(2.6f+Step*.3f)-Math.PI/2);
        public Map01PowerSabotage(int seed,int step=0){this.seed=seed;Step=Math.Clamp(step,0,Steps-1);Next();}
        void Next(){age=0;Remaining=Seconds;Center=.25f+.5f*(float)new Random(unchecked(seed+Step*7919)).NextDouble();}
        public void Tick(float dt){if(Complete)return;dt=Math.Max(0,dt);age+=dt;Remaining=Math.Max(0,Remaining-dt);if(Remaining<=0)Complete=true;}
        public bool Pull(){
            if(Complete)return false;
            if(Math.Abs(Needle-Center)>Window*.5f){Complete=true;return false;}
            Step++;if(Step==Steps)Complete=true;else Next();return true;
        }
    }

}
