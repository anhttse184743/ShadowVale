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
        [Range(10,55)]public float nightPatrolLightRange=28;
        public GameObject civilianArt,tankArt,coveredHungArt;
        public AnimationClip namMournClip,hungPlantRetreatClip;
        public AnimationClip namCheckRifle,hungHurtReaction,hungLeftRadio,hungNightRest,hungNightSleep,namCoverJacket,namAntennaClimb,namAntennaTie;
        public AudioClip hungTheme,hungThemeNightTwo;
        public Vector3[] dayTwoOpenRoute={new Vector3(-52,1,15),new Vector3(-20,1,14)};
        public Vector3[] dayTwoCoveredRoute={new Vector3(-65,1,-15),new Vector3(-25,1,-24)};
        [Min(2)] public float antennaHeight=4;
        public Vector3[] nightTwoEvacuationRoute={new Vector3(-29.324f,1,-30.592f),new Vector3(-36.655f,1,-44.419f),new Vector3(-36.044f,1,-64.069f)};
        [Header("Night 2 river-bank infiltration (latest marked Map 2 overview)")]
        public Vector3 nightTwoHatch=new Vector3(-3.96654f,.8f,-51.858f);
        // Reference is the first guard; remaining guards continue along the straight river-facing row.
        public Vector3 nightTwoPatrolCenter=new Vector3(36.247412f,.8f,-18.801694f);
        public Vector3 nightTwoPatrolRowDirection=new Vector3(-.4606423f,0,-.8875859f);
        public Vector3 nightTwoRiverEntry=new Vector3(-3.971f,.8f,-23.858f);
        public Vector3 nightTwoRiverExit=new Vector3(-28,1,-26);
        public Vector3[] nightTwoBankRoute={new Vector3(4.582f,.8f,-.753f),new Vector3(.305f,.8f,-5.483f),new Vector3(-1.833f,.8f,-11.669f),new Vector3(-3.971f,.8f,-17.128f),new Vector3(-3.971f,.8f,-21.858f),new Vector3(-3.9695f,.8f,-31.858f),new Vector3(-3.968f,.8f,-41.858f),new Vector3(-3.96654f,.8f,-51.858f)};
        [Min(1)] public float nightTwoBankHalfWidth=3;
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
