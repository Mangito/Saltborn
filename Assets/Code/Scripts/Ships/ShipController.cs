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

    private float moveInput;
    private float turnInput;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.linearDamping = waterDrag;
        rb.angularDamping = 2f;

        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    void Update()
    {
        moveInput = Input.GetAxis("Vertical");
        turnInput = Input.GetAxis("Horizontal");
    }

    void FixedUpdate()
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
