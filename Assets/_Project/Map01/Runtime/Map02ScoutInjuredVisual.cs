using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>The approved resting pose, shown only while the scout awaits rescue.</summary>
    public sealed class Map02ScoutInjuredVisual : MonoBehaviour
    {
        GameObject resting;
        Renderer[] standing;
        bool[] visible;
        Animator animator;
        bool injured;
        public bool Injured => injured;

        public void SetInjured(bool value, Animator source)
        {
            if (value == injured || source == null) return;
            if (value && resting == null)
            {
                var art = Resources.Load<GameObject>("Characters/Map02Roles/TamInjured");
                if (art == null) { Debug.LogError("Missing approved Tâm injured pose.", this); return; }
                animator = source;
                standing = source.GetComponentsInChildren<Renderer>(true);
                visible = new bool[standing.Length];
                resting = Instantiate(art, source.transform, false);
                resting.name = "Tâm — nằm bị thương (đã duyệt)";
                var materials = standing.Length > 0 ? standing[0].sharedMaterials : null;
                foreach (var renderer in resting.GetComponentsInChildren<Renderer>())
                    if (materials != null) renderer.sharedMaterials = materials;
            }
            if (value)
            {
                for (int i = 0; i < standing.Length; i++)
                { visible[i] = standing[i].enabled; standing[i].enabled = false; }
            }
            else
            {
                for (int i = 0; i < standing.Length; i++)
                    if (standing[i] != null) standing[i].enabled = visible[i];
            }
            resting.SetActive(value);
            injured = value;
        }
    }
}

