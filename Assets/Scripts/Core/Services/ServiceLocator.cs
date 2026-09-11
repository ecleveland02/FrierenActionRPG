using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frieren.Core.Services
{
    /// <summary>
    /// Registry of long-lived game services, populated once by <c>Bootstrapper</c>.
    /// </summary>
    /// <remarks>
    /// Chosen over one singleton per system. By Milestone 5 there are save, scene, input, state,
    /// combat and AI services that need each other; a static <c>Instance</c> on each makes every
    /// one of them impossible to fake in a test and hides the dependency graph. Registration here
    /// is explicit and happens in exactly one place, and a test can register a stub instead.
    ///
    /// Consumers should resolve once in <c>Awake</c>/<c>Start</c> and cache the result rather than
    /// calling <see cref="Get{T}"/> every frame.
    /// </remarks>
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> Services = new Dictionary<Type, object>();

        public static IReadOnlyCollection<Type> RegisteredTypes => Services.Keys;

        /// <summary>
        /// Clears stale registrations when entering play mode with domain reload disabled, where
        /// static state would otherwise survive from the previous session.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            Services.Clear();
        }

        public static void Register<T>(T service) where T : class
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            Services[typeof(T)] = service;
        }

        public static bool TryGet<T>(out T service) where T : class
        {
            if (Services.TryGetValue(typeof(T), out object stored))
            {
                service = stored as T;
                return service != null;
            }

            service = null;
            return false;
        }

        public static T Get<T>() where T : class
        {
            if (TryGet(out T service))
            {
                return service;
            }

            throw new InvalidOperationException(
                $"No service of type {typeof(T).Name} is registered. " +
                "Services are registered by Bootstrapper in the Boot scene - is it loaded?");
        }

        public static bool IsRegistered<T>() where T : class => Services.ContainsKey(typeof(T));

        public static bool Unregister<T>() where T : class => Services.Remove(typeof(T));

        public static void Clear() => Services.Clear();
    }
}
