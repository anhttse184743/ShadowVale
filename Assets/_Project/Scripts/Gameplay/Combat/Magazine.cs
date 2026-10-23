using System;
using UnityEngine;

namespace ShadowVale.Gameplay.Combat
{
    /// <summary>
    /// The rounds actually in the weapon, sitting between the trigger and the reserve in the
    /// player's pack.
    /// <para>
    /// Without this layer a rifle simply drains the backpack one round per shot, which removes
    /// every decision worth making: there is no moment where the magazine is nearly out and the
    /// player has to choose between pushing on and stepping behind cover. The reserve is only
    /// touched on a reload, which is also where the cost of being caught empty is paid.
    /// </para>
    /// Plain C# with no Unity dependencies beyond <see cref="Mathf"/>, so the timing can be
    /// tested without entering play mode.
    /// </summary>
    public sealed class Magazine
    {
        private readonly float _reloadSeconds;
        private float _reloadRemaining;
        private float _reloadDuration;
        private readonly float[] _commits;
        private float _reloadElapsed, _fullReloadDuration;
        private int _planned, _inserted;

        public int Capacity { get; }
        public int Rounds { get; private set; }

        public bool IsReloading => _reloadRemaining > 0f;
        public bool IsEmpty => Rounds <= 0;
        public bool IsFull => Rounds >= Capacity;

        /// <summary>0 at the start of a reload, 1 as it completes. 0 when not reloading.</summary>
        public float ReloadProgress =>
            IsReloading && _reloadDuration > 0f
                ? Mathf.Clamp01(1f - _reloadRemaining / _reloadDuration)
                : 0f;

        public float ReloadRemaining => Mathf.Max(0f, _reloadRemaining);
        public int ReloadPlannedRounds => _planned;
        public float FullReloadProgress => _fullReloadDuration>0 ? _reloadElapsed/_fullReloadDuration : 0;

        /// <param name="reloadSeconds">One base duration for every reload, regardless of rounds left.</param>
        public Magazine(int capacity, float reloadSeconds, float[] individualCommits = null)
        {
            Capacity = Mathf.Max(1, capacity);
            _reloadSeconds = Mathf.Max(0.01f, reloadSeconds);
            Rounds = Capacity;
            if(individualCommits!=null&&individualCommits.Length==Capacity)
            {
                float previous=0;
                foreach(float value in individualCommits){if(!float.IsFinite(value)||value<=previous||value>=1)throw new ArgumentException("Reload insertions must be strictly increasing within (0,1).");previous=value;}
                _commits=(float[])individualCommits.Clone();
            }
        }

        /// <summary>
        /// Spends one round. Fails while empty, and while reloading — the magazine is out of the
        /// weapon, so holding the trigger through a reload does nothing.
        /// </summary>
        public bool TryConsume()
        {
            if (IsReloading || IsEmpty) return false;
            Rounds--;
            return true;
        }

        /// <summary>
        /// Starts a reload, if one is worth starting.
        /// </summary>
        /// <param name="reserve">
        /// Rounds available to load. Zero refuses the reload; a negative value means the caller
        /// has no reserve to track and the magazine simply fills.
        /// </param>
        public bool BeginReload(int reserve, float durationMultiplier = 1f)
        {
            if (IsReloading || IsFull || reserve == 0) return false;
            if (float.IsNaN(durationMultiplier) || float.IsInfinity(durationMultiplier) || durationMultiplier <= 0f) return false;
            _fullReloadDuration=Mathf.Max(.01f,_reloadSeconds*durationMultiplier);_reloadElapsed=0;_inserted=0;
            _planned=Mathf.Min(Capacity-Rounds,reserve<0?Capacity:reserve);
            // Keep the opening and closing, skipping only the unused repeated insertion gestures.
            _reloadDuration = _commits==null ? _fullReloadDuration
                : _fullReloadDuration*(_commits[_planned-1]+1-_commits[Capacity-1]);
            _reloadRemaining = _reloadDuration;
            return true;
        }

        /// <summary>
        /// Advances a reload in progress and completes it when the time is up.
        /// </summary>
        /// <param name="drawFromReserve">
        /// Asked for the rounds needed to fill the magazine; returns how many were actually
        /// available. A null supplier fills the magazine outright, which is what a scene with no
        /// inventory wants.
        /// </param>
        public void Tick(float deltaTime, Func<int, int> drawFromReserve = null)
        {
            if (!IsReloading) return;

            deltaTime=Mathf.Max(0,deltaTime);_reloadRemaining -= deltaTime;_reloadElapsed+=deltaTime;
            if(_commits!=null)
            {
                while(_inserted<_planned && _reloadElapsed+1e-6f>=_fullReloadDuration*_commits[_inserted])
                {
                    int insertedRound=drawFromReserve!=null?Mathf.Clamp(drawFromReserve(1),0,1):1;
                    if(insertedRound==0){CancelReload();return;}
                    Rounds=Mathf.Min(Capacity,Rounds+insertedRound);_inserted++;
                }
                if(_reloadRemaining<=0)_reloadRemaining=0;
                return;
            }
            if (_reloadRemaining > 0f) return;

            _reloadRemaining = 0f;
            int wanted = Capacity - Rounds;
            int granted = drawFromReserve != null ? Mathf.Clamp(drawFromReserve(wanted), 0, wanted) : wanted;
            Rounds += granted;
        }

        /// <summary>
        /// Abandons a reload without loading anything. Dying mid-reload has to go through here:
        /// leaving the timer running across a respawn would strand the weapon in a state it can
        /// never leave, because a magazine that believes it is reloading refuses to fire.
        /// </summary>
        public void CancelReload()
        {
            _reloadRemaining = 0f;
            _reloadDuration = 0f;
            _reloadElapsed=0;_fullReloadDuration=0;_planned=0;_inserted=0;
        }

        /// <summary>Sets the loaded count directly. For restoring a checkpoint.</summary>
        public void Refill(int rounds)
        {
            CancelReload();
            Rounds = Mathf.Clamp(rounds, 0, Capacity);
        }
    }
}
