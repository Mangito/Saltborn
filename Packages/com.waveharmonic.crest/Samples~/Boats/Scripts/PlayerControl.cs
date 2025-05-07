// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

#if d_Unity_InputSystem && ENABLE_INPUT_SYSTEM
#define d_InputSystem
#endif

using UnityEngine;
using UnityEngine.InputSystem;

#pragma warning disable CS0414

namespace WaveHarmonic.Crest.Watercraft.Examples
{
    sealed class PlayerControl : Control
    {
        [SerializeField, HideInInspector]
        int _Version = 0;

#if d_InputSystem
        [SerializeField]
        Key _DriveForward = Key.W;

        [SerializeField]
        Key _DriveBackward = Key.S;

        [SerializeField]
        Key _SteerLeftward = Key.A;

        [SerializeField]
        Key _SteerRightward = Key.D;

        [SerializeField]
        Key _FloatUpward = Key.E;

        [SerializeField]
        Key _FloatDownward = Key.Q;
#else
        [SerializeField]
        int _DriveForward;

        [SerializeField]
        int _DriveBackward;

        [SerializeField]
        int _SteerLeftward;

        [SerializeField]
        int _SteerRightward;

        [SerializeField]
        int _FloatUpward;

        [SerializeField]
        int _FloatDownward;
#endif

#if d_InputSystem
        [HideInInspector]
#endif
        [Tooltip("The input axis name for throttle. See Project Settings > Input Manager.")]
        [SerializeField]
        string _DriveInputAxis = "Vertical";

#if d_InputSystem
        [HideInInspector]
#endif
        [Tooltip("The input axis name for steering. See Project Settings > Input Manager.")]
        [SerializeField]
        string _SteerInputAxis = "Horizontal";

#if d_InputSystem
        [HideInInspector]
#endif
        [SerializeField]
        KeyCode _FloatUpwards = KeyCode.E;

#if d_InputSystem
        [HideInInspector]
#endif
        [SerializeField]
        KeyCode _FloatDownwards = KeyCode.Q;

        [Tooltip("Whether to allow submerge control.")]
        [SerializeField]
        bool _Submersible;

#pragma warning disable UNT0001
        // Here to force the checkbox to show.
        void Start() { }
#pragma warning restore UNT0001

        public override Vector3 Input
        {
            get
            {
                if (!isActiveAndEnabled || !Application.isFocused) return Vector3.zero;

                var input = Vector3.zero;
#if d_InputSystem
                input.z += Keyboard.current[_DriveForward].isPressed ? 1f : 0f;
                input.z += Keyboard.current[_DriveBackward].isPressed ? -1f : 0f;
                input.x += Keyboard.current[_SteerLeftward].isPressed ? -1f : 0f;
                input.x += Keyboard.current[_SteerRightward].isPressed ? 1f : 0f;
                input.y += Keyboard.current[_FloatUpward].isPressed ? 1f : 0f;
                input.y += Keyboard.current[_FloatDownward].isPressed ? -1f : 0f;
#else
                input.z = UnityEngine.Input.GetAxis(_DriveInputAxis);
                input.x = UnityEngine.Input.GetAxis(_SteerInputAxis);
                input.y += UnityEngine.Input.GetKey(_FloatUpwards) ? 1f : 0f;
                input.y += UnityEngine.Input.GetKey(_FloatDownwards) ? -1f : 0f;
#endif

                // Steering towards same direction as forward when going backwards.
                if (input.z < 0f) input.x *= -1f;
                if (!_Submersible) input.y = 0f;
                return input;
            }
        }
    }
}
