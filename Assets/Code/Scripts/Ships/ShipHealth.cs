using UnityEngine;

public class ShipHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] float maxHealth = 100f;
    [SerializeField] float currentHealth;

    bool isSinking = false;

    private void Start()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float damage)
    {
        if (isSinking) return;

        currentHealth -= damage;
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        if (isSinking) return;
        isSinking = true;

        // Drastically increase mass to force sinking
        var rb = GetComponent<Rigidbody>();
    if (rb != null)
    {
        rb.mass = 10000f;
    }
        Destroy(gameObject, 10f);
    }
}
