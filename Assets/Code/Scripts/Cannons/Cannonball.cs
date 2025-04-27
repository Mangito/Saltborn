using UnityEngine;

public class Cannonball : MonoBehaviour
{
    [SerializeField] private float lifespan = 10f;

    private void Start()
    {
        Destroy(gameObject, lifespan);
    }

    private void OnCollisionEnter(Collision collision)
    {
        Destroy(gameObject);
    }
}
