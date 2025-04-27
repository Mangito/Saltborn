using UnityEngine;

public class CannonFire : MonoBehaviour
{
    public GameObject cannonballPrefab;
    public Transform firingPoint;

    public Transform cannonballParent;

    public float firingForce = 5000f;

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            FireCannon();
        }
    }

    void FireCannon()
    {
        GameObject cannonball = Instantiate(cannonballPrefab, firingPoint.position, firingPoint.rotation, cannonballParent);
        Rigidbody rb = cannonball.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.AddForce(firingPoint.forward * firingForce);
        }
    }
}