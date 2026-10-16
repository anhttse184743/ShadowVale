using UnityEngine;
namespace ShadowVale.Map01 {
    [DisallowMultipleComponent] public sealed class Map01OverseerGrip:MonoBehaviour {
        public Vector3 localGunEuler;
        [Range(0,1)] public float supportWeight=1;
        Map01EnemyGrip grip;
        public bool Calibrated=>grip!=null&&grip.Calibrated;
        public float PalmError=>grip!=null?grip.PalmError:0;
        public void Bind(Map01EnemyController guard){grip=Map01EnemyGrip.Bind(guard);}
    }
}
