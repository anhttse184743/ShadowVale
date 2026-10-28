using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>The approved resting pose, shown only while the scout awaits rescue.</summary>
    [DefaultExecutionOrder(80)]
    public sealed class Map02ScoutInjuredVisual : MonoBehaviour
    {
        GameObject resting;
        Renderer[] standing;
        bool[] visible;
        Animator animator;
        bool injured;
        public bool Injured => injured;

        void LateUpdate(){if(injured)GroundRestingPose();}
        void GroundRestingPose()
        {
            if(resting==null)return;
            float floor=transform.position.y;
            var hits=Physics.RaycastAll(transform.position+Vector3.up*.7f,Vector3.down,1.6f,~0,QueryTriggerInteraction.Ignore);
            float closest=float.PositiveInfinity;
            foreach(var hit in hits)
            {
                if(hit.normal.y<.55f||hit.transform.IsChildOf(transform)||hit.collider is CharacterController||hit.collider.GetComponentInParent<ShadowVale.Gameplay.Combat.Health>()!=null)continue;
                if(hit.distance<closest){closest=hit.distance;floor=hit.point.y;}
            }
            float bottom=float.PositiveInfinity;
            foreach(var renderer in resting.GetComponentsInChildren<Renderer>())if(renderer.enabled)bottom=Mathf.Min(bottom,renderer.bounds.min.y);
            if(float.IsFinite(bottom))resting.transform.position+=Vector3.up*(floor+.015f-bottom);
        }
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
            if(value)GroundRestingPose();
        }
    }
}

