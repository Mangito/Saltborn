//------------------------------------------------------------------------------
// <auto-generated>
//     This code was auto-generated by com.unity.inputsystem:InputActionCodeGenerator
//     version 1.14.0
//     from Assets/Code/Scripts/PlayerControls.inputactions
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

/// <summary>
/// Provides programmatic access to <see cref="InputActionAsset" />, <see cref="InputActionMap" />, <see cref="InputAction" /> and <see cref="InputControlScheme" /> instances defined in asset "Assets/Code/Scripts/PlayerControls.inputactions".
/// </summary>
/// <remarks>
/// This class is source generated and any manual edits will be discarded if the associated asset is reimported or modified.
/// </remarks>
/// <example>
/// <code>
/// using namespace UnityEngine;
/// using UnityEngine.InputSystem;
///
/// // Example of using an InputActionMap named "Player" from a UnityEngine.MonoBehaviour implementing callback interface.
/// public class Example : MonoBehaviour, MyActions.IPlayerActions
/// {
///     private MyActions_Actions m_Actions;                  // Source code representation of asset.
///     private MyActions_Actions.PlayerActions m_Player;     // Source code representation of action map.
///
///     void Awake()
///     {
///         m_Actions = new MyActions_Actions();              // Create asset object.
///         m_Player = m_Actions.Player;                      // Extract action map object.
///         m_Player.AddCallbacks(this);                      // Register callback interface IPlayerActions.
///     }
///
///     void OnDestroy()
///     {
///         m_Actions.Dispose();                              // Destroy asset object.
///     }
///
///     void OnEnable()
///     {
///         m_Player.Enable();                                // Enable all actions within map.
///     }
///
///     void OnDisable()
///     {
///         m_Player.Disable();                               // Disable all actions within map.
///     }
///
///     #region Interface implementation of MyActions.IPlayerActions
///
///     // Invoked when "Move" action is either started, performed or canceled.
///     public void OnMove(InputAction.CallbackContext context)
///     {
///         Debug.Log($"OnMove: {context.ReadValue&lt;Vector2&gt;()}");
///     }
///
///     // Invoked when "Attack" action is either started, performed or canceled.
///     public void OnAttack(InputAction.CallbackContext context)
///     {
///         Debug.Log($"OnAttack: {context.ReadValue&lt;float&gt;()}");
///     }
///
///     #endregion
/// }
/// </code>
/// </example>
public partial class @PlayerControls: IInputActionCollection2, IDisposable
{
    /// <summary>
    /// Provides access to the underlying asset instance.
    /// </summary>
    public InputActionAsset asset { get; }

    /// <summary>
    /// Constructs a new instance.
    /// </summary>
    public @PlayerControls()
    {
        asset = InputActionAsset.FromJson(@"{
    ""name"": ""PlayerControls"",
    ""maps"": [
        {
            ""name"": ""ShipControls"",
            ""id"": ""ad4ec126-b175-4b67-84e0-61b5c05f4bdd"",
            ""actions"": [
                {
                    ""name"": ""Steer"",
                    ""type"": ""Value"",
                    ""id"": ""535e4ad2-971c-4eed-a9a9-abc6267bb1d6"",
                    ""expectedControlType"": ""Axis"",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": true
                },
                {
                    ""name"": ""Throttle"",
                    ""type"": ""Value"",
                    ""id"": ""68f8ece5-a9a2-467c-831c-c21cdfdcdd93"",
                    ""expectedControlType"": ""Axis"",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": true
                },
                {
                    ""name"": ""FireCannons"",
                    ""type"": ""Button"",
                    ""id"": ""6674962b-ae21-4275-94a1-1c41fcfe165c"",
                    ""expectedControlType"": """",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": false
                },
                {
                    ""name"": ""AimCannons"",
                    ""type"": ""Value"",
                    ""id"": ""8862423a-4a7a-48f8-89f8-42c0107943d1"",
                    ""expectedControlType"": ""Vector2"",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": true
                },
                {
                    ""name"": ""Anchor"",
                    ""type"": ""Button"",
                    ""id"": ""3348989e-d942-42c8-8114-f9eb20c6789e"",
                    ""expectedControlType"": """",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": false
                },
                {
                    ""name"": ""LookAround"",
                    ""type"": ""Value"",
                    ""id"": ""c1f6aed7-d096-4e1d-8cb8-2f829a5abfcd"",
                    ""expectedControlType"": ""Vector2"",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": true
                },
                {
                    ""name"": ""UseSpecial"",
                    ""type"": ""Button"",
                    ""id"": ""818f02cc-83f1-401c-84cd-1d0448bf8ee9"",
                    ""expectedControlType"": """",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": false
                }
            ],
            ""bindings"": [
                {
                    ""name"": ""1D Axis"",
                    ""id"": ""d9ceedda-205e-4bd4-9ea2-e5489c861cf6"",
                    ""path"": ""1DAxis"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Steer"",
                    ""isComposite"": true,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": ""negative"",
                    ""id"": ""66fa781d-5447-408e-99a0-3f05d2c9a4c9"",
                    ""path"": ""<Keyboard>/#(A)"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Steer"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""positive"",
                    ""id"": ""adc70c55-dbfc-429c-8d9b-36906cc4e9c1"",
                    ""path"": ""<Keyboard>/d"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Steer"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""1D Axis"",
                    ""id"": ""6903bd56-4b14-4b20-aa7e-822545ebd091"",
                    ""path"": ""1DAxis"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Steer"",
                    ""isComposite"": true,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": ""negative"",
                    ""id"": ""a8a4e0a7-778e-4f09-93da-23307f8e3f31"",
                    ""path"": ""<Gamepad>/leftStick/left"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Steer"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""positive"",
                    ""id"": ""1941c6b9-4d06-478a-8f56-68dbc6f176df"",
                    ""path"": ""<Gamepad>/leftStick/right"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Steer"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""1D Axis"",
                    ""id"": ""596aa3e8-3027-4d97-82fd-9c508a20fb30"",
                    ""path"": ""1DAxis"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Throttle"",
                    ""isComposite"": true,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": ""negative"",
                    ""id"": ""e0d9ce95-b6f6-4967-b311-dbe769f56ab3"",
                    ""path"": ""<Keyboard>/#(S)"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Throttle"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""positive"",
                    ""id"": ""7cad649d-d713-45e1-9bde-3d193eabfbd1"",
                    ""path"": ""<Keyboard>/w"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Throttle"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""1D Axis"",
                    ""id"": ""eaaa47f9-d9d3-46db-a6ee-a23ed9601ab0"",
                    ""path"": ""1DAxis"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Throttle"",
                    ""isComposite"": true,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": ""negative"",
                    ""id"": ""69d31f55-bd52-4d11-ac7a-f61fbddfcc19"",
                    ""path"": ""<Gamepad>/leftStick/down"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Throttle"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""positive"",
                    ""id"": ""50fcce32-d096-43aa-ae26-2ae721799812"",
                    ""path"": ""<Gamepad>/leftStick/up"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Throttle"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": """",
                    ""id"": ""be6aec5a-a56a-40b6-9beb-c787f2ea6d76"",
                    ""path"": ""<Mouse>/leftButton"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""FireCannons"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""905de951-5795-43c8-9610-19b647573a2e"",
                    ""path"": ""<Gamepad>/rightShoulder"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""FireCannons"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""d19f93dd-72a6-4263-ba50-5fc37d739661"",
                    ""path"": ""<Mouse>/delta"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""AimCannons"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""7a4309c1-4ab7-4202-9549-16c33c24eb64"",
                    ""path"": ""<Gamepad>/rightStick"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""AimCannons"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""1a62fcd2-97d3-42c7-b76e-75a727722d49"",
                    ""path"": ""<Mouse>/delta"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""LookAround"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""95851163-860d-4f9a-95b2-5f00db2b925d"",
                    ""path"": ""<Gamepad>/rightStick"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""LookAround"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""a2234326-8606-4bdc-b49f-10cf8474d2fd"",
                    ""path"": ""<Keyboard>/#(E)"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Anchor"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""b1fe14e8-bffb-438a-add1-99ba0306c384"",
                    ""path"": ""<Gamepad>/buttonNorth"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Anchor"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""59d7d469-e00e-4f68-8289-d824a54ac6f4"",
                    ""path"": ""<Keyboard>/#(Q)"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""UseSpecial"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""11de3672-b7bf-4af1-8215-98145ae07615"",
                    ""path"": ""<Gamepad>/leftTrigger"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""UseSpecial"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                }
            ]
        }
    ],
    ""controlSchemes"": []
}");
        // ShipControls
        m_ShipControls = asset.FindActionMap("ShipControls", throwIfNotFound: true);
        m_ShipControls_Steer = m_ShipControls.FindAction("Steer", throwIfNotFound: true);
        m_ShipControls_Throttle = m_ShipControls.FindAction("Throttle", throwIfNotFound: true);
        m_ShipControls_FireCannons = m_ShipControls.FindAction("FireCannons", throwIfNotFound: true);
        m_ShipControls_AimCannons = m_ShipControls.FindAction("AimCannons", throwIfNotFound: true);
        m_ShipControls_Anchor = m_ShipControls.FindAction("Anchor", throwIfNotFound: true);
        m_ShipControls_LookAround = m_ShipControls.FindAction("LookAround", throwIfNotFound: true);
        m_ShipControls_UseSpecial = m_ShipControls.FindAction("UseSpecial", throwIfNotFound: true);
    }

    ~@PlayerControls()
    {
        UnityEngine.Debug.Assert(!m_ShipControls.enabled, "This will cause a leak and performance issues, PlayerControls.ShipControls.Disable() has not been called.");
    }

    /// <summary>
    /// Destroys this asset and all associated <see cref="InputAction"/> instances.
    /// </summary>
    public void Dispose()
    {
        UnityEngine.Object.Destroy(asset);
    }

    /// <inheritdoc cref="UnityEngine.InputSystem.InputActionAsset.bindingMask" />
    public InputBinding? bindingMask
    {
        get => asset.bindingMask;
        set => asset.bindingMask = value;
    }

    /// <inheritdoc cref="UnityEngine.InputSystem.InputActionAsset.devices" />
    public ReadOnlyArray<InputDevice>? devices
    {
        get => asset.devices;
        set => asset.devices = value;
    }

    /// <inheritdoc cref="UnityEngine.InputSystem.InputActionAsset.controlSchemes" />
    public ReadOnlyArray<InputControlScheme> controlSchemes => asset.controlSchemes;

    /// <inheritdoc cref="UnityEngine.InputSystem.InputActionAsset.Contains(InputAction)" />
    public bool Contains(InputAction action)
    {
        return asset.Contains(action);
    }

    /// <inheritdoc cref="UnityEngine.InputSystem.InputActionAsset.GetEnumerator()" />
    public IEnumerator<InputAction> GetEnumerator()
    {
        return asset.GetEnumerator();
    }

    /// <inheritdoc cref="IEnumerable.GetEnumerator()" />
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <inheritdoc cref="UnityEngine.InputSystem.InputActionAsset.Enable()" />
    public void Enable()
    {
        asset.Enable();
    }

    /// <inheritdoc cref="UnityEngine.InputSystem.InputActionAsset.Disable()" />
    public void Disable()
    {
        asset.Disable();
    }

    /// <inheritdoc cref="UnityEngine.InputSystem.InputActionAsset.bindings" />
    public IEnumerable<InputBinding> bindings => asset.bindings;

    /// <inheritdoc cref="UnityEngine.InputSystem.InputActionAsset.FindAction(string, bool)" />
    public InputAction FindAction(string actionNameOrId, bool throwIfNotFound = false)
    {
        return asset.FindAction(actionNameOrId, throwIfNotFound);
    }

    /// <inheritdoc cref="UnityEngine.InputSystem.InputActionAsset.FindBinding(InputBinding, out InputAction)" />
    public int FindBinding(InputBinding bindingMask, out InputAction action)
    {
        return asset.FindBinding(bindingMask, out action);
    }

    // ShipControls
    private readonly InputActionMap m_ShipControls;
    private List<IShipControlsActions> m_ShipControlsActionsCallbackInterfaces = new List<IShipControlsActions>();
    private readonly InputAction m_ShipControls_Steer;
    private readonly InputAction m_ShipControls_Throttle;
    private readonly InputAction m_ShipControls_FireCannons;
    private readonly InputAction m_ShipControls_AimCannons;
    private readonly InputAction m_ShipControls_Anchor;
    private readonly InputAction m_ShipControls_LookAround;
    private readonly InputAction m_ShipControls_UseSpecial;
    /// <summary>
    /// Provides access to input actions defined in input action map "ShipControls".
    /// </summary>
    public struct ShipControlsActions
    {
        private @PlayerControls m_Wrapper;

        /// <summary>
        /// Construct a new instance of the input action map wrapper class.
        /// </summary>
        public ShipControlsActions(@PlayerControls wrapper) { m_Wrapper = wrapper; }
        /// <summary>
        /// Provides access to the underlying input action "ShipControls/Steer".
        /// </summary>
        public InputAction @Steer => m_Wrapper.m_ShipControls_Steer;
        /// <summary>
        /// Provides access to the underlying input action "ShipControls/Throttle".
        /// </summary>
        public InputAction @Throttle => m_Wrapper.m_ShipControls_Throttle;
        /// <summary>
        /// Provides access to the underlying input action "ShipControls/FireCannons".
        /// </summary>
        public InputAction @FireCannons => m_Wrapper.m_ShipControls_FireCannons;
        /// <summary>
        /// Provides access to the underlying input action "ShipControls/AimCannons".
        /// </summary>
        public InputAction @AimCannons => m_Wrapper.m_ShipControls_AimCannons;
        /// <summary>
        /// Provides access to the underlying input action "ShipControls/Anchor".
        /// </summary>
        public InputAction @Anchor => m_Wrapper.m_ShipControls_Anchor;
        /// <summary>
        /// Provides access to the underlying input action "ShipControls/LookAround".
        /// </summary>
        public InputAction @LookAround => m_Wrapper.m_ShipControls_LookAround;
        /// <summary>
        /// Provides access to the underlying input action "ShipControls/UseSpecial".
        /// </summary>
        public InputAction @UseSpecial => m_Wrapper.m_ShipControls_UseSpecial;
        /// <summary>
        /// Provides access to the underlying input action map instance.
        /// </summary>
        public InputActionMap Get() { return m_Wrapper.m_ShipControls; }
        /// <inheritdoc cref="UnityEngine.InputSystem.InputActionMap.Enable()" />
        public void Enable() { Get().Enable(); }
        /// <inheritdoc cref="UnityEngine.InputSystem.InputActionMap.Disable()" />
        public void Disable() { Get().Disable(); }
        /// <inheritdoc cref="UnityEngine.InputSystem.InputActionMap.enabled" />
        public bool enabled => Get().enabled;
        /// <summary>
        /// Implicitly converts an <see ref="ShipControlsActions" /> to an <see ref="InputActionMap" /> instance.
        /// </summary>
        public static implicit operator InputActionMap(ShipControlsActions set) { return set.Get(); }
        /// <summary>
        /// Adds <see cref="InputAction.started"/>, <see cref="InputAction.performed"/> and <see cref="InputAction.canceled"/> callbacks provided via <param cref="instance" /> on all input actions contained in this map.
        /// </summary>
        /// <param name="instance">Callback instance.</param>
        /// <remarks>
        /// If <paramref name="instance" /> is <c>null</c> or <paramref name="instance"/> have already been added this method does nothing.
        /// </remarks>
        /// <seealso cref="ShipControlsActions" />
        public void AddCallbacks(IShipControlsActions instance)
        {
            if (instance == null || m_Wrapper.m_ShipControlsActionsCallbackInterfaces.Contains(instance)) return;
            m_Wrapper.m_ShipControlsActionsCallbackInterfaces.Add(instance);
            @Steer.started += instance.OnSteer;
            @Steer.performed += instance.OnSteer;
            @Steer.canceled += instance.OnSteer;
            @Throttle.started += instance.OnThrottle;
            @Throttle.performed += instance.OnThrottle;
            @Throttle.canceled += instance.OnThrottle;
            @FireCannons.started += instance.OnFireCannons;
            @FireCannons.performed += instance.OnFireCannons;
            @FireCannons.canceled += instance.OnFireCannons;
            @AimCannons.started += instance.OnAimCannons;
            @AimCannons.performed += instance.OnAimCannons;
            @AimCannons.canceled += instance.OnAimCannons;
            @Anchor.started += instance.OnAnchor;
            @Anchor.performed += instance.OnAnchor;
            @Anchor.canceled += instance.OnAnchor;
            @LookAround.started += instance.OnLookAround;
            @LookAround.performed += instance.OnLookAround;
            @LookAround.canceled += instance.OnLookAround;
            @UseSpecial.started += instance.OnUseSpecial;
            @UseSpecial.performed += instance.OnUseSpecial;
            @UseSpecial.canceled += instance.OnUseSpecial;
        }

        /// <summary>
        /// Removes <see cref="InputAction.started"/>, <see cref="InputAction.performed"/> and <see cref="InputAction.canceled"/> callbacks provided via <param cref="instance" /> on all input actions contained in this map.
        /// </summary>
        /// <remarks>
        /// Calling this method when <paramref name="instance" /> have not previously been registered has no side-effects.
        /// </remarks>
        /// <seealso cref="ShipControlsActions" />
        private void UnregisterCallbacks(IShipControlsActions instance)
        {
            @Steer.started -= instance.OnSteer;
            @Steer.performed -= instance.OnSteer;
            @Steer.canceled -= instance.OnSteer;
            @Throttle.started -= instance.OnThrottle;
            @Throttle.performed -= instance.OnThrottle;
            @Throttle.canceled -= instance.OnThrottle;
            @FireCannons.started -= instance.OnFireCannons;
            @FireCannons.performed -= instance.OnFireCannons;
            @FireCannons.canceled -= instance.OnFireCannons;
            @AimCannons.started -= instance.OnAimCannons;
            @AimCannons.performed -= instance.OnAimCannons;
            @AimCannons.canceled -= instance.OnAimCannons;
            @Anchor.started -= instance.OnAnchor;
            @Anchor.performed -= instance.OnAnchor;
            @Anchor.canceled -= instance.OnAnchor;
            @LookAround.started -= instance.OnLookAround;
            @LookAround.performed -= instance.OnLookAround;
            @LookAround.canceled -= instance.OnLookAround;
            @UseSpecial.started -= instance.OnUseSpecial;
            @UseSpecial.performed -= instance.OnUseSpecial;
            @UseSpecial.canceled -= instance.OnUseSpecial;
        }

        /// <summary>
        /// Unregisters <param cref="instance" /> and unregisters all input action callbacks via <see cref="ShipControlsActions.UnregisterCallbacks(IShipControlsActions)" />.
        /// </summary>
        /// <seealso cref="ShipControlsActions.UnregisterCallbacks(IShipControlsActions)" />
        public void RemoveCallbacks(IShipControlsActions instance)
        {
            if (m_Wrapper.m_ShipControlsActionsCallbackInterfaces.Remove(instance))
                UnregisterCallbacks(instance);
        }

        /// <summary>
        /// Replaces all existing callback instances and previously registered input action callbacks associated with them with callbacks provided via <param cref="instance" />.
        /// </summary>
        /// <remarks>
        /// If <paramref name="instance" /> is <c>null</c>, calling this method will only unregister all existing callbacks but not register any new callbacks.
        /// </remarks>
        /// <seealso cref="ShipControlsActions.AddCallbacks(IShipControlsActions)" />
        /// <seealso cref="ShipControlsActions.RemoveCallbacks(IShipControlsActions)" />
        /// <seealso cref="ShipControlsActions.UnregisterCallbacks(IShipControlsActions)" />
        public void SetCallbacks(IShipControlsActions instance)
        {
            foreach (var item in m_Wrapper.m_ShipControlsActionsCallbackInterfaces)
                UnregisterCallbacks(item);
            m_Wrapper.m_ShipControlsActionsCallbackInterfaces.Clear();
            AddCallbacks(instance);
        }
    }
    /// <summary>
    /// Provides a new <see cref="ShipControlsActions" /> instance referencing this action map.
    /// </summary>
    public ShipControlsActions @ShipControls => new ShipControlsActions(this);
    /// <summary>
    /// Interface to implement callback methods for all input action callbacks associated with input actions defined by "ShipControls" which allows adding and removing callbacks.
    /// </summary>
    /// <seealso cref="ShipControlsActions.AddCallbacks(IShipControlsActions)" />
    /// <seealso cref="ShipControlsActions.RemoveCallbacks(IShipControlsActions)" />
    public interface IShipControlsActions
    {
        /// <summary>
        /// Method invoked when associated input action "Steer" is either <see cref="UnityEngine.InputSystem.InputAction.started" />, <see cref="UnityEngine.InputSystem.InputAction.performed" /> or <see cref="UnityEngine.InputSystem.InputAction.canceled" />.
        /// </summary>
        /// <seealso cref="UnityEngine.InputSystem.InputAction.started" />
        /// <seealso cref="UnityEngine.InputSystem.InputAction.performed" />
        /// <seealso cref="UnityEngine.InputSystem.InputAction.canceled" />
        void OnSteer(InputAction.CallbackContext context);
        /// <summary>
        /// Method invoked when associated input action "Throttle" is either <see cref="UnityEngine.InputSystem.InputAction.started" />, <see cref="UnityEngine.InputSystem.InputAction.performed" /> or <see cref="UnityEngine.InputSystem.InputAction.canceled" />.
        /// </summary>
        /// <seealso cref="UnityEngine.InputSystem.InputAction.started" />
        /// <seealso cref="UnityEngine.InputSystem.InputAction.performed" />
        /// <seealso cref="UnityEngine.InputSystem.InputAction.canceled" />
        void OnThrottle(InputAction.CallbackContext context);
        /// <summary>
        /// Method invoked when associated input action "FireCannons" is either <see cref="UnityEngine.InputSystem.InputAction.started" />, <see cref="UnityEngine.InputSystem.InputAction.performed" /> or <see cref="UnityEngine.InputSystem.InputAction.canceled" />.
        /// </summary>
        /// <seealso cref="UnityEngine.InputSystem.InputAction.started" />
        /// <seealso cref="UnityEngine.InputSystem.InputAction.performed" />
        /// <seealso cref="UnityEngine.InputSystem.InputAction.canceled" />
        void OnFireCannons(InputAction.CallbackContext context);
        /// <summary>
        /// Method invoked when associated input action "AimCannons" is either <see cref="UnityEngine.InputSystem.InputAction.started" />, <see cref="UnityEngine.InputSystem.InputAction.performed" /> or <see cref="UnityEngine.InputSystem.InputAction.canceled" />.
        /// </summary>
        /// <seealso cref="UnityEngine.InputSystem.InputAction.started" />
        /// <seealso cref="UnityEngine.InputSystem.InputAction.performed" />
        /// <seealso cref="UnityEngine.InputSystem.InputAction.canceled" />
        void OnAimCannons(InputAction.CallbackContext context);
        /// <summary>
        /// Method invoked when associated input action "Anchor" is either <see cref="UnityEngine.InputSystem.InputAction.started" />, <see cref="UnityEngine.InputSystem.InputAction.performed" /> or <see cref="UnityEngine.InputSystem.InputAction.canceled" />.
        /// </summary>
        /// <seealso cref="UnityEngine.InputSystem.InputAction.started" />
        /// <seealso cref="UnityEngine.InputSystem.InputAction.performed" />
        /// <seealso cref="UnityEngine.InputSystem.InputAction.canceled" />
        void OnAnchor(InputAction.CallbackContext context);
        /// <summary>
        /// Method invoked when associated input action "LookAround" is either <see cref="UnityEngine.InputSystem.InputAction.started" />, <see cref="UnityEngine.InputSystem.InputAction.performed" /> or <see cref="UnityEngine.InputSystem.InputAction.canceled" />.
        /// </summary>
        /// <seealso cref="UnityEngine.InputSystem.InputAction.started" />
        /// <seealso cref="UnityEngine.InputSystem.InputAction.performed" />
        /// <seealso cref="UnityEngine.InputSystem.InputAction.canceled" />
        void OnLookAround(InputAction.CallbackContext context);
        /// <summary>
        /// Method invoked when associated input action "UseSpecial" is either <see cref="UnityEngine.InputSystem.InputAction.started" />, <see cref="UnityEngine.InputSystem.InputAction.performed" /> or <see cref="UnityEngine.InputSystem.InputAction.canceled" />.
        /// </summary>
        /// <seealso cref="UnityEngine.InputSystem.InputAction.started" />
        /// <seealso cref="UnityEngine.InputSystem.InputAction.performed" />
        /// <seealso cref="UnityEngine.InputSystem.InputAction.canceled" />
        void OnUseSpecial(InputAction.CallbackContext context);
    }
}
