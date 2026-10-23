using System;
using UnityEngine;
namespace ShadowVale.Map01
{
    [CreateAssetMenu(menuName="ShadowVale/Map 2/Chapter layout")]
    public sealed class Map02ChapterLayout : ScriptableObject
    {
        [Serializable] public sealed class Site {public string id;public Vector3 position;}
        [Serializable] public sealed class RoleArt {public string role;public GameObject prefab;}
        public RoleArt[] actors=Array.Empty<RoleArt>();
        public GameObject enemyArt;
        public Site[] sites=Array.Empty<Site>();
        [Min(0)] public int supplyCrateRounds=30;
        [Header("Day 1 / Night 1 natural light")][Range(.3f,1.2f)] public float dayOneSunIntensity=.82f;
        [Range(.1f,1f)] public float dayOneOilLampIntensity=.65f;
        public float nightSunIntensity=.08f,nightSkyExposure=.08f;
        public float patrolSweepSeconds=15,coverFireSeconds=75;
        public GameObject civilianArt,tankArt,coveredHungArt;
        public AnimationClip namMournClip,hungPlantRetreatClip;
        [Header("Night 3 art slots (empty: placeholder or idle pose)")]
        public GameObject radioPropArt,explosivesArt,soupBowlArt,militiaArt;
        public AnimationClip namProneClip,namLieBackClip,namSitRadioClip,hungFallClip;
        [Header("Carried supplies (empty: a plain box)")]
        public GameObject grenadeCrateArt,ammoCanArt,medkitArt,grenadeArt;
        [Tooltip("The straw cart by the store; Day 3's medicine and ammunition wait on it.")]
        public GameObject strawCartArt;
        [Header("Night 3 sound slots (empty: synthesized or silent)")]
        public AudioClip tankTracksClip,tinnitusClip,artilleryClip;
    }
}
