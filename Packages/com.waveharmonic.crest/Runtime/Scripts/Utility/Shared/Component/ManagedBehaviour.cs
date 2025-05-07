// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using System;
using System.Collections.Generic;

namespace WaveHarmonic.Crest.Internal
{
    /// <summary>
    /// Manages ManagedBehaviours. Replaces Unity's event system.
    /// </summary>
    /// <typeparam name="T">The manager type.</typeparam>
    public abstract class ManagerBehaviour<T> : CustomBehaviour where T : ManagerBehaviour<T>
    {
        internal static readonly List<Action<T>> s_OnUpdate = new();
        internal static readonly List<Action<T>> s_OnLateUpdate = new();
        internal static readonly List<Action<T>> s_OnFixedUpdate = new();
        internal static readonly List<Action<T>> s_OnEnable = new();
        internal static readonly List<Action<T>> s_OnDisable = new();

        /// <summary>
        /// The singleton instance.
        /// </summary>
        public static T Instance { get; private set; }

        void Broadcast(List<Action<T>> listeners, T instance)
        {
            for (var i = listeners.Count - 1; i >= 0; --i)
            {
                listeners[i].Invoke(instance);
            }
        }

        void Broadcast(List<Action<T>> listeners)
        {
            Broadcast(listeners, Instance);
        }

        private protected virtual void Enable()
        {
            // Setting up instance should be last.
            Instance = (T)this;
            Broadcast(s_OnEnable);
        }

        private protected virtual void Disable()
        {
            Broadcast(s_OnDisable);
            Instance = null;
        }

        private protected virtual void FixedUpdate() => Broadcast(s_OnFixedUpdate);
        private protected void BroadcastUpdate() => Broadcast(s_OnUpdate);
        private protected virtual void LateUpdate() => Broadcast(s_OnLateUpdate);

        // OnLoad etc cannot be used on open generic types.
        internal static void AfterRuntimeLoad()
        {
            Instance = null;
        }

        internal static void AfterScriptReload()
        {
            Instance = FindFirstObjectByType<T>();
        }
    }

    /// <summary>
    /// A behaviour which is driven by a ManagerBehaviour instead of Unity's event system.
    /// </summary>
    /// <typeparam name="T">The manager type.</typeparam>
    public abstract class ManagedBehaviour<T> : CustomBehaviour where T : ManagerBehaviour<T>
    {
        readonly Action<T> _OnUpdate;
        readonly Action<T> _OnLateUpdate;
        readonly Action<T> _OnFixedUpdate;
        readonly Action<T> _OnEnable;
        readonly Action<T> _OnDisable;

        /// <summary>
        /// The Update method called by the manager class.
        /// </summary>
        private protected virtual Action<T> OnUpdateMethod => null;

        /// <summary>
        /// The LateUpdate method called by the manager class.
        /// </summary>
        private protected virtual Action<T> OnLateUpdateMethod => null;

        /// <summary>
        /// The FixedUpdated method called by the manager class.
        /// </summary>
        private protected virtual Action<T> OnFixedUpdateMethod => null;

        /// <summary>
        /// The OnEnable method called by the manager class.
        /// </summary>
        private protected virtual Action<T> OnEnableMethod => null;

        /// <summary>
        /// The OnDisable method called by the manager class.
        /// </summary>
        private protected virtual Action<T> OnDisableMethod => null;

        /// <summary>
        /// Constructor which caches Actions to avoid allocations.
        /// </summary>
        public ManagedBehaviour()
        {
            if (OnUpdateMethod != null) _OnUpdate = new(OnUpdateMethod);
            if (OnLateUpdateMethod != null) _OnLateUpdate = new(OnLateUpdateMethod);
            if (OnFixedUpdateMethod != null) _OnFixedUpdate = new(OnFixedUpdateMethod);
            if (OnEnableMethod != null) _OnEnable = new(OnEnableMethod);
            if (OnDisableMethod != null) _OnDisable = new(OnDisableMethod);
        }

        /// <inheritdoc/>
        private protected override void OnEnable()
        {
            base.OnEnable();

            UpdateSubscription(listen: true);

            // Trigger OnEnable as it has already passed.
            if (_OnEnable != null && ManagerBehaviour<T>.Instance != null)
            {
                _OnEnable(ManagerBehaviour<T>.Instance);
            }
        }

        /// <summary>
        /// Unity's OnDisable method. Make sure to call base if overriden.
        /// </summary>
        private protected virtual void OnDisable()
        {
            UpdateSubscription(listen: false);

            if (_OnDisable != null && ManagerBehaviour<T>.Instance != null)
            {
                _OnDisable(ManagerBehaviour<T>.Instance);
            }
        }

        void UpdateSubscription(bool listen)
        {
            if (_OnUpdate != null)
            {
                ManagerBehaviour<T>.s_OnUpdate.Remove(_OnUpdate);
                if (listen) ManagerBehaviour<T>.s_OnUpdate.Add(_OnUpdate);
            }

            if (_OnLateUpdate != null)
            {
                ManagerBehaviour<T>.s_OnLateUpdate.Remove(_OnLateUpdate);
                if (listen) ManagerBehaviour<T>.s_OnLateUpdate.Add(_OnLateUpdate);
            }

            if (_OnFixedUpdate != null)
            {
                ManagerBehaviour<T>.s_OnFixedUpdate.Remove(_OnFixedUpdate);
                if (listen) ManagerBehaviour<T>.s_OnFixedUpdate.Add(_OnFixedUpdate);
            }

            if (_OnEnable != null)
            {
                ManagerBehaviour<T>.s_OnEnable.Remove(_OnEnable);
                if (listen) ManagerBehaviour<T>.s_OnEnable.Add(_OnEnable);
            }

            if (_OnDisable != null)
            {
                ManagerBehaviour<T>.s_OnDisable.Remove(_OnDisable);
                if (listen) ManagerBehaviour<T>.s_OnDisable.Add(_OnDisable);
            }
        }
    }
}
