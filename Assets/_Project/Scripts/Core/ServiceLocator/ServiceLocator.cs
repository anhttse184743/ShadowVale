using System;
using System.Collections.Generic;

namespace ShadowVale.Core.Services
{
    /// <summary>
    /// Minimal service registry. Registered once by <c>GameBootstrap</c>; resolved by
    /// gameplay/UI code instead of singletons so tests can swap implementations.
    /// </summary>
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> Services = new();

        public static void Register<T>(T instance) where T : class
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            Services[typeof(T)] = instance;
        }

        public static T Resolve<T>() where T : class
        {
            if (Services.TryGetValue(typeof(T), out var s)) return (T)s;
            throw new InvalidOperationException($"Service {typeof(T).Name} is not registered. Did 00_Boot run?");
        }

        public static bool TryResolve<T>(out T service) where T : class
        {
            if (Services.TryGetValue(typeof(T), out var s)) { service = (T)s; return true; }
            service = null;
            return false;
        }

        public static bool IsRegistered<T>() where T : class => Services.ContainsKey(typeof(T));

        public static void Unregister<T>() where T : class => Services.Remove(typeof(T));

        /// <summary>Used by tests and by the Boot scene when reloading.</summary>
        public static void Clear() => Services.Clear();
    }
}
