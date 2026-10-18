using UnityEngine;

namespace ShadowVale.Map01
{
    public interface IMapObjectives
    {
        string ObjectiveText { get; }
        bool IsComplete { get; }
        bool TryGetObjectiveTarget(out Vector3 target, out string label);
    }

    public interface IBinoculars
    {
        bool Binoculars { get; }
        void HoldBinoculars(bool held);
    }
}
