using UnityEngine;
using UnityEditor;
using TheLastEmpire;

public class AddGrenadeToPlayer
{
    [MenuItem("Tools/Setup Grenade Weapon")]
    public static void AddGrenade()
    {
        string path = "Assets/Resources/Prefabs/Character/PlayerPrefabs.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab != null)
        {
            PlayerController pc = prefab.GetComponent<PlayerController>();
            if (pc != null)
            {
                // Check if Grenade already exists
                bool exists = pc.WeaponsList.Exists(w => w.weaponName != null && w.weaponName.ToLower().Contains("grenade"));
                if (exists)
                {
                    Debug.Log("Grenade already exists in PlayerPrefabs.prefab!");
                    return;
                }

                Weapon grenade = new Weapon();
                grenade.weaponName = "Grenade";
                grenade.fireRate = 1.0f;
                grenade.magazineSize = 1;
                grenade.reloadDuration = 1.0f;
                grenade.damage = 100;
                grenade.range = 5.0f;
                grenade.ammoType = "Grenade";
                grenade.vfxPoolKey = "Explosion";
                
                pc.WeaponsList.Add(grenade);
                
                EditorUtility.SetDirty(prefab);
                AssetDatabase.SaveAssets();
                Debug.Log("✅ Added Grenade to PlayerPrefabs successfully! You can now equip it in-game.");
            }
            else
            {
                Debug.LogError("PlayerController script not found on PlayerPrefabs!");
            }
        }
        else
        {
            Debug.LogError("Could not find PlayerPrefabs at " + path);
        }
    }
}
