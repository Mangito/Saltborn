using UnityEngine;

public class Cannonball : MonoBehaviour
{
    [SerializeField] float lifespan = 10f;
    [SerializeField] float damage = 20f;

     void Start()
    {
        Destroy(gameObject, lifespan);
    }

    void OnTriggerEnter(Collider other)
    {
        ShipHealth ship = other.GetComponentInParent<ShipHealth>();
        if (ship != null)
        {
            ship.TakeDamage(damage);
        }

        Destroy(gameObject);
    }
}
