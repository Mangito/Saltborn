using UnityEngine;

public class CannonFire : MonoBehaviour, IShipWeapon
{
    [SerializeField] private GameObject cannonballPrefab;

    [SerializeField] private Transform firingPoint;

    [SerializeField] private Transform cannonballParent;

    [SerializeField] private float firingForce = 5000f;


    public void FireCannon()
    {
        GameObject cannonball = Instantiate(cannonballPrefab, firingPoint.position, firingPoint.rotation, cannonballParent);
        Rigidbody rb = cannonball.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.AddForce(firingPoint.forward * firingForce);
        }
    }

    public void Fire()
    {
        FireCannon();
    }
}
