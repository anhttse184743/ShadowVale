using UnityEngine;
using ShadowVale.Gameplay.Combat;
namespace ShadowVale.Map01
{
    public sealed class Map01ActorAssets : ScriptableObject
    {
        public Weapon rifle;
        public float namHeight, hungHeight, hungSole;
        public Vector3 riflePosition, rifleEuler;
        public static Map01ActorAssets Load() => Resources.Load<Map01ActorAssets>("Characters/Map01ActorAssets");
    }
}
