using UnityEngine;
namespace ShadowVale.Map01
{
    /// <summary>Approved C1-only visuals. Does not replace Nam's gameplay model.</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, sourceAssembly: "ShadowVale.Map01")]
    public sealed class Map02ArrivalC1Data:ScriptableObject
    {
        public GameObject nam,hung;
        public AnimationClip namMotion,hungMotion;
        public Mesh backpackMesh;
        public TextAsset pack,travel,reviewPoses;
        public string sourceSha256;
    }
}
