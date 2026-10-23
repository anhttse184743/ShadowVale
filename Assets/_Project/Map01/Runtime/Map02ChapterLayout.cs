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
        public Vector3[] nightTwoEvacuationRoute={new Vector3(-56,1,-12),new Vector3(-48,1,-34),new Vector3(-37,1,-59)};
    }
}
