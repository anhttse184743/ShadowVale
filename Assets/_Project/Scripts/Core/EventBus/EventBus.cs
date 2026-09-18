using System;
using System.Collections.Generic;

namespace ShadowVale.Core.Events
{
    /// <summary>
    /// Typed publish/subscribe. Handlers run synchronously on the caller's thread;
    /// a throwing handler does not stop the others.
    /// </summary>
    public static class EventBus
    {
        private static readonly Dictionary<Type, List<Delegate>> Handlers = new();

        public static void Subscribe<T>(Action<T> handler) where T : IEvent
        {
            if (!Handlers.TryGetValue(typeof(T), out var list))
                Handlers[typeof(T)] = list = new List<Delegate>();
            list.Add(handler);
        }

        public static void Unsubscribe<T>(Action<T> handler) where T : IEvent
        {
            if (Handlers.TryGetValue(typeof(T), out var list)) list.Remove(handler);
        }

        public static void Publish<T>(T evt) where T : IEvent
        {
            if (!Handlers.TryGetValue(typeof(T), out var list) || list.Count == 0) return;
            // copy so handlers may unsubscribe during dispatch
            var snapshot = list.ToArray();
            foreach (var d in snapshot)
            {
                try { ((Action<T>)d).Invoke(evt); }
                catch (Exception e) { UnityEngine.Debug.LogException(e); }
            }
        }

        public static void Clear() => Handlers.Clear();
    }
}
