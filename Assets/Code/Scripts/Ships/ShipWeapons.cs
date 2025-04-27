using UnityEngine;

public class ShipWeapons : MonoBehaviour
{
    [Header("Weapon Containers")]
    public Transform leftWeapons;
    public Transform rightWeapons;
    public Transform frontWeapons;
    public Transform backWeapons;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            FireWeapons(leftWeapons);
        }
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            FireWeapons(rightWeapons);
        }
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            FireWeapons(frontWeapons);
        }
        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            FireWeapons(backWeapons);
        }
    }

    void FireWeapons(Transform weaponContainer)
    {
        if (weaponContainer == null)
        {
            Debug.LogWarning("No weapons assigned to this side!");
            return;
        }

        foreach (Transform weapon in weaponContainer)
        {
            CannonFire cannon = weapon.GetComponent<CannonFire>();
            if (cannon != null)
            {
                cannon.FireCannon();
            }
        }
    }
}
