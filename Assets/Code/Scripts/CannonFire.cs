using UnityEngine;

public class CannonFire : MonoBehaviour
{
    public GameObject cannonballPrefab;
    public Transform firingPoint;
    public float firingForce = 5000f;

    void Update()
    {
        if (Input.GetMouseButtonDown(0))  // it's set to fire with the left mouse button
        {
            FireCannon();
        }
    }

    void FireCannon()
    {
        GameObject cannonball = Instantiate(cannonballPrefab, firingPoint.position, firingPoint.rotation);
        Rigidbody rb = cannonball.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.AddForce(firingPoint.forward * firingForce);
        }
    }
}