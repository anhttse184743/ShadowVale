using System;
using System.Linq;
using UnityEngine;
namespace ShadowVale.Map01
{
    [CreateAssetMenu(menuName="ShadowVale/Map 2/Day one performance")]
    public sealed class Map02DayOneArt:ScriptableObject
    {
        [Serializable] public sealed class Action {public string id;public AnimationClip clip;}
        public Action[] actions=Array.Empty<Action>();
        public AnimationClip Find(string id)=>actions.FirstOrDefault(a=>a.id==id)?.clip;
        [Min(.5f)] public float assistSpeed=1.25f;
        [Min(1)] public float bandageSeconds=24,shiftSeconds=12;
        public Vector3 carryOffset=new Vector3(0,.87f,.36f);
        public GameObject medicinePacket;
    }
}
