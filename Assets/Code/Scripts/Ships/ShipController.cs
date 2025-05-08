using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ShipController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float thrustForce = 15f;
    [SerializeField] private float turnSpeed = 50f;
    [SerializeField] private float maxSpeed = 10f;
    [SerializeField] private float waterDrag = 2f;

    private Rigidbody rb;
    private PlayerControls controls;

    private float moveInput;
    private float turnInput;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        controls = new PlayerControls();

        // Setup input bindings
        controls.ShipControls.Throttle.performed += ctx => moveInput = ctx.ReadValue<float>();
        controls.ShipControls.Throttle.canceled += _ => moveInput = 0f;

        controls.ShipControls.Steer.performed += ctx => turnInput = ctx.ReadValue<float>();
        controls.ShipControls.Steer.canceled += _ => turnInput = 0f;
    }

    private void OnEnable()
    {
        controls.ShipControls.Enable();
    }

    private void OnDisable()
    {
        controls.ShipControls.Disable();
    }

    private void Start()
    {
        // Apply drag to simulate water resistance
        rb.linearDamping = waterDrag;
        rb.angularDamping = 2f;

        // Removed tilt constraints for natural motion
        rb.constraints = RigidbodyConstraints.None;
    }

    private void FixedUpdate()
    {
        Vector3 shipForward = -transform.right;

        if (rb.linearVelocity.magnitude < maxSpeed)
        {
            rb.AddForce(shipForward * moveInput * thrustForce, ForceMode.Acceleration);
        }

        Quaternion deltaRotation = Quaternion.Euler(0f, turnInput * turnSpeed * Time.fixedDeltaTime, 0f);
        rb.MoveRotation(rb.rotation * deltaRotation);
    }
}
