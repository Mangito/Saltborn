using System.Collections.Generic;
using UnityEngine;

public interface IShipWeapon
{
    void Fire();
}

public class ShipWeapons : MonoBehaviour
{
    [Header("Weapon Containers")]
    [SerializeField] private Transform leftWeapons;
    [SerializeField] private Transform rightWeapons;
    [SerializeField] private Transform frontWeapons;
    [SerializeField] private Transform backWeapons;

    private Dictionary<Transform, List<IShipWeapon>> cachedWeapons = new Dictionary<Transform, List<IShipWeapon>>();

    void Start()
    {
        CacheWeaponReferences(leftWeapons);
        CacheWeaponReferences(rightWeapons);
        CacheWeaponReferences(frontWeapons);
        CacheWeaponReferences(backWeapons);
    }

    void Update()
    {
        // NOTE: For a more flexible system, consider using Unity's new Input System
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

    void CacheWeaponReferences(Transform container)
    {
        if (container == null) return;

        List<IShipWeapon> weapons = new List<IShipWeapon>();
        foreach (Transform weapon in container)
        {
            IShipWeapon shipWeapon = weapon.GetComponent<IShipWeapon>();
            if (shipWeapon != null)
            {
                weapons.Add(shipWeapon);
            }
        }
        cachedWeapons[container] = weapons;
    }

    void FireWeapons(Transform weaponContainer)
    {
        if (weaponContainer == null)
        {
            Debug.LogWarning("No weapons assigned to this side!");
            return;
        }

        if (cachedWeapons.TryGetValue(weaponContainer, out List<IShipWeapon> weapons) && weapons.Count > 0)
        {
            foreach (IShipWeapon weapon in weapons)
            {
                weapon.Fire();
            }
        }
        else
        {
            Debug.LogWarning($"No IShipWeapon components found in {weaponContainer.name}");
        }
    }
}

