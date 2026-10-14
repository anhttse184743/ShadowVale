using UnityEngine;
namespace ShadowVale.Map01 {
    /// <summary>Pure timing state; no damage, input, clock or scene dependencies.</summary>
    public sealed class Map01BushTiming {
        public const float RoundSeconds=2, WindowWidth=.24f, GapSeconds=.35f;
        static readonly float[] Centers={.62f,.38f,.7f,.48f};
        public int Successes{get;private set;}
        public int Misses{get;private set;}
        public int Round{get;private set;}
        public float Elapsed{get;private set;}
        public float Gap{get;private set;}
        public bool Complete=>Successes>=3||Misses>=2;
        public bool Won=>Successes>=3;
        public float Cursor=>Mathf.Clamp01(Elapsed/RoundSeconds);
        public float Center=>Centers[Round%Centers.Length];
        public bool InWindow=>Mathf.Abs(Cursor-Center)<=WindowWidth*.5f;
        public bool Tick(float dt,bool pressed) {
            if(Complete)return false;
            if(Gap>0){Gap=Mathf.Max(0,Gap-dt);return false;}
            Elapsed+=Mathf.Max(0,dt);
            if(!pressed&&Elapsed<RoundSeconds)return false;
            bool hit=pressed&&Elapsed<=RoundSeconds&&InWindow;
            if(hit)Successes++;else Misses++;
            Round++;Elapsed=0;Gap=GapSeconds;return true;
        }
    }
}
