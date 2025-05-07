// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

#if d_UnityInputSystem && ENABLE_INPUT_SYSTEM
#define INPUT_SYSTEM_ENABLED
#endif

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace WaveHarmonic.Crest.Examples
{
    /// <summary>
    /// A simple and dumb camera script that can be controlled using WASD and the mouse.
    /// </summary>
#if !CREST_DEBUG
    [AddComponentMenu("")]
#endif
    sealed class CameraController : MonoBehaviour
    {
        [SerializeField, HideInInspector]
#pragma warning disable 414
        int _Version = 0;
#pragma warning restore 414

        [SerializeField]
        float _LinearSpeed = 10f;

        [SerializeField]
        float _AngularSpeed = 70f;

        [SerializeField]
        bool _SimulateForwardInput = false;

        [SerializeField]
        bool _RequireLeftMouseButtonToMove = false;

        [SerializeField]
        float _FixedDeltaTime = 1 / 60f;

        [UnityEngine.Space(10)]

        [SerializeField]
        DebugFields _Debug = new();

        [System.Serializable]
        sealed class DebugFields
        {
            [Tooltip("Allows the camera to roll (rotating on the z axis).")]
            public bool _EnableCameraRoll = false;

            [Tooltip("Disables the XR occlusion mesh for debugging purposes. Only works with legacy XR.")]
            public bool _DisableOcclusionMesh = false;

            [Tooltip("Sets the XR occlusion mesh scale. Useful for debugging refractions. Only works with legacy XR."), UnityEngine.Range(1f, 2f)]
            public float _OcclusionMeshScale = 1f;
        }


        Vector2 _LastMousePosition = -Vector2.one;
        bool _Dragging = false;
        Transform _TargetTransform;
        Camera _Camera;


        void Awake()
        {
            _TargetTransform = transform;

            if (!TryGetComponent(out _Camera))
            {
                enabled = false;
                return;
            }

#if ENABLE_VR && d_UnityModuleVR
            if (XRSettings.enabled)
            {
                // Seems like the best place to put this for now. Most XR debugging happens using this component.
                // @FixMe: useOcclusionMesh doesn't work anymore. Might be a Unity bug.
                XRSettings.useOcclusionMesh = !_Debug._DisableOcclusionMesh;
                XRSettings.occlusionMaskScale = _Debug._OcclusionMeshScale;
            }
#endif
        }

        void Update()
        {
            var dt = Time.deltaTime;
            if (_FixedDeltaTime > 0f)
                dt = _FixedDeltaTime;

            UpdateMovement(dt);

#if ENABLE_VR && d_UnityModuleVR
            // These aren't useful and can break for XR hardware.
            if (!XRSettings.enabled || XRSettings.loadedDeviceName.Contains("MockHMD"))
#endif
            {
                UpdateDragging(dt);
                UpdateKillRoll();
            }

#if ENABLE_VR && d_UnityModuleVR
            if (XRSettings.enabled)
            {
                // Check if property has changed.
                if (XRSettings.useOcclusionMesh == _Debug._DisableOcclusionMesh)
                {
                    // @FixMe: useOcclusionMesh doesn't work anymore. Might be a Unity bug.
                    XRSettings.useOcclusionMesh = !_Debug._DisableOcclusionMesh;
                }

                XRSettings.occlusionMaskScale = _Debug._OcclusionMeshScale;
            }
#endif
        }

        void UpdateMovement(float dt)
        {
            // New input system works even when game view is not focused.
            if (!Application.isFocused)
            {
                return;
            }

#if INPUT_SYSTEM_ENABLED
            if (!Mouse.current.leftButton.isPressed && _RequireLeftMouseButtonToMove) return;
            float forward = (Keyboard.current.wKey.isPressed ? 1 : 0) - (Keyboard.current.sKey.isPressed ? 1 : 0);
#else
            if (!Input.GetMouseButton(0) && _RequireLeftMouseButtonToMove) return;
            float forward = (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0);
#endif
            if (_SimulateForwardInput)
            {
                forward = 1f;
            }

            _TargetTransform.position += dt * forward * _LinearSpeed * _TargetTransform.forward;
            var speed = _LinearSpeed;

#if INPUT_SYSTEM_ENABLED
            if (Keyboard.current.leftShiftKey.isPressed)
#else
            if (Input.GetKey(KeyCode.LeftShift))
#endif
            {
                speed *= 3f;
            }

            _TargetTransform.position += dt * forward * speed * _TargetTransform.forward;
            // _TargetTransform.position += _LinearSpeed * _TargetTransform.right * Input.GetAxis( "Horizontal" ) * dt;
#if INPUT_SYSTEM_ENABLED
            _TargetTransform.position += (Keyboard.current.eKey.isPressed ? 1 : 0) * dt * _LinearSpeed * _TargetTransform.up;
            _TargetTransform.position -= (Keyboard.current.qKey.isPressed ? 1 : 0) * dt * _LinearSpeed * _TargetTransform.up;
            _TargetTransform.position -= (Keyboard.current.aKey.isPressed ? 1 : 0) * dt * _LinearSpeed * _TargetTransform.right;
            _TargetTransform.position += (Keyboard.current.dKey.isPressed ? 1 : 0) * dt * _LinearSpeed * _TargetTransform.right;
            _TargetTransform.position += (Keyboard.current.eKey.isPressed ? 1 : 0) * dt * speed * _TargetTransform.up;
            _TargetTransform.position -= (Keyboard.current.qKey.isPressed ? 1 : 0) * dt * speed * _TargetTransform.up;
            _TargetTransform.position -= (Keyboard.current.aKey.isPressed ? 1 : 0) * dt * speed * _TargetTransform.right;
            _TargetTransform.position += (Keyboard.current.dKey.isPressed ? 1 : 0) * dt * speed * _TargetTransform.right;
#else
            _TargetTransform.position += (Input.GetKey(KeyCode.E) ? 1 : 0) * dt * _LinearSpeed * _TargetTransform.up;
            _TargetTransform.position -= (Input.GetKey(KeyCode.Q) ? 1 : 0) * dt * _LinearSpeed * _TargetTransform.up;
            _TargetTransform.position -= (Input.GetKey(KeyCode.A) ? 1 : 0) * dt * _LinearSpeed * _TargetTransform.right;
            _TargetTransform.position += (Input.GetKey(KeyCode.D) ? 1 : 0) * dt * _LinearSpeed * _TargetTransform.right;
            _TargetTransform.position += (Input.GetKey(KeyCode.E) ? 1 : 0) * dt * speed * _TargetTransform.up;
            _TargetTransform.position -= (Input.GetKey(KeyCode.Q) ? 1 : 0) * dt * speed * _TargetTransform.up;
            _TargetTransform.position -= (Input.GetKey(KeyCode.A) ? 1 : 0) * dt * speed * _TargetTransform.right;
            _TargetTransform.position += (Input.GetKey(KeyCode.D) ? 1 : 0) * dt * speed * _TargetTransform.right;
#endif
            {
                var rotate = 0f;
#if INPUT_SYSTEM_ENABLED
                rotate += Keyboard.current.rightArrowKey.isPressed ? 1 : 0;
                rotate -= Keyboard.current.leftArrowKey.isPressed ? 1 : 0;
#else
                rotate += Input.GetKey(KeyCode.RightArrow) ? 1 : 0;
                rotate -= Input.GetKey(KeyCode.LeftArrow) ? 1 : 0;
#endif

                rotate *= 5f;
                var ea = _TargetTransform.eulerAngles;
                ea.y += 0.1f * _AngularSpeed * rotate * dt;
                _TargetTransform.eulerAngles = ea;
            }
        }

        void UpdateDragging(float dt)
        {
            // New input system works even when game view is not focused.
            if (!Application.isFocused)
            {
                return;
            }

            var mousePos =
#if INPUT_SYSTEM_ENABLED
                Mouse.current.position.ReadValue();
#else
                new Vector2(Input.mousePosition.x, Input.mousePosition.y);
#endif

            var wasLeftMouseButtonPressed =
#if INPUT_SYSTEM_ENABLED
                Mouse.current.leftButton.wasPressedThisFrame;
#else
                Input.GetMouseButtonDown(0);
#endif

            if (!_Dragging && wasLeftMouseButtonPressed && _Camera.rect.Contains(_Camera.ScreenToViewportPoint(mousePos)) &&
                !DebugGUI.OverGUI(mousePos))
            {
                _Dragging = true;
                _LastMousePosition = mousePos;
            }
#if INPUT_SYSTEM_ENABLED
            if (_Dragging && Mouse.current.leftButton.wasReleasedThisFrame)
#else
            if (_Dragging && Input.GetMouseButtonUp(0))
#endif
            {
                _Dragging = false;
                _LastMousePosition = -Vector2.one;
            }

            if (_Dragging)
            {
                var delta = mousePos - _LastMousePosition;

                var ea = _TargetTransform.eulerAngles;
                ea.x += -0.1f * _AngularSpeed * delta.y * dt;
                ea.y += 0.1f * _AngularSpeed * delta.x * dt;
                _TargetTransform.eulerAngles = ea;

                _LastMousePosition = mousePos;
            }
        }

        void UpdateKillRoll()
        {
            if (_Debug._EnableCameraRoll) return;
            var ea = _TargetTransform.eulerAngles;
            ea.z = 0f;
            transform.eulerAngles = ea;
        }
    }
}
