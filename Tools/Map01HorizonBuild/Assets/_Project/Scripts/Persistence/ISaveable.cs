namespace ShadowVale.Persistence
{
    /// <summary>
    /// A system that owns part of the save file. <c>SaveService</c> calls <see cref="CaptureState"/>
    /// on every registered saveable and stores the JSON under <see cref="SaveKey"/>.
    /// </summary>
    public interface ISaveable
    {
        /// <summary>Stable snake_case key, e.g. "inventory", "quests", "skills".</summary>
        string SaveKey { get; }

        /// <summary>Return a plain serialisable object (POCO from ShadowVale.Data).</summary>
        object CaptureState();

        /// <summary>Receive the JSON text stored under <see cref="SaveKey"/>; may be null on a fresh game.</summary>
        void RestoreState(string json);
    }
}
