using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ShipMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float thrustForce = 15f;
    public float turnSpeed = 50f;
    public float maxSpeed = 10f;
    public float waterDrag = 2f;

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.linearDamping = waterDrag;
        rb.angularDamping = 2f;

        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    void FixedUpdate()
    {
        float moveInput = Input.GetAxis("Vertical");
        float turnInput = Input.GetAxis("Horizontal");

        Vector3 shipForward = -transform.right;

        if (rb.linearVelocity.magnitude < maxSpeed)
        {
            rb.AddForce(shipForward * moveInput * thrustForce, ForceMode.Acceleration);
        }

        Quaternion deltaRotation = Quaternion.Euler(0f, turnInput * turnSpeed * Time.fixedDeltaTime, 0f);
        rb.MoveRotation(rb.rotation * deltaRotation);
    }
}
