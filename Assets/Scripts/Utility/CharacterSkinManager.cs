using UnityEngine;

public class CharacterSkinManager : MonoBehaviour
{
    [Header("Hierarchy")]
    public Transform visualHolder;

    [Header("Prefabs & Scene Objects")]
    public GameObject[] characterPrefabs;
    public GameObject[] handWeaponPrefabs;

    [Tooltip("Drag the Giant Weapon that is ALREADY IN THE SCENE into this slot!")]
    public Transform giantWeaponInScene;

    [Header("Weapon Placement")]
    [Tooltip("Create an Empty GameObject exactly where the weapon should rest on the ground, and assign it here.")]
    public Transform weaponRestAnchor;

    [Header("Runtime References (Read Only)")]
    public Animator currentActiveAnimator;
    public Transform currentWeaponSocket;

    private GameObject _currentSkinInstance;
    private int _currentWeaponIndex = -1;

    [Header("Dynamic Camera Framers")]
    public CinemachineDynamicScaler weaponZoomFramer; // <-- Changed type

    public void EquipCharacterSkin(int skinIndex)
    {
        if (characterPrefabs == null || characterPrefabs.Length == 0) return;
        skinIndex = Mathf.Clamp(skinIndex, 0, characterPrefabs.Length - 1);

        if (_currentSkinInstance != null && currentActiveAnimator != null) return;

        if (_currentSkinInstance != null) Destroy(_currentSkinInstance);

        _currentSkinInstance = Instantiate(characterPrefabs[skinIndex], visualHolder);
        _currentSkinInstance.transform.localPosition = Vector3.zero;
        _currentSkinInstance.transform.localRotation = Quaternion.identity;

        SkinReferences references = _currentSkinInstance.GetComponent<SkinReferences>();
        if (references != null)
        {
            currentActiveAnimator = references.skinAnimator;
            currentWeaponSocket = references.weaponSocket;
        }
    }

    public void EquipWeaponSkin(int weaponIndex)
    {
        if (handWeaponPrefabs == null || handWeaponPrefabs.Length == 0) return;
        weaponIndex = Mathf.Clamp(weaponIndex, 0, handWeaponPrefabs.Length - 1);

        if (_currentWeaponIndex == weaponIndex && giantWeaponInScene != null) return;

        if (giantWeaponInScene != null)
        {
            Destroy(giantWeaponInScene.gameObject);
        }

        // We now use the dedicated rest anchor as the absolute parent
        Transform targetParent = weaponRestAnchor != null ? weaponRestAnchor : transform;

        GameObject newWeaponInstance = Instantiate(handWeaponPrefabs[weaponIndex], targetParent);
        newWeaponInstance.transform.localPosition = Vector3.zero;
        newWeaponInstance.transform.localRotation = Quaternion.identity;

        // Counteract inherited parent scale
        newWeaponInstance.transform.localScale = Vector3.one;
        Vector3 parentLossyScale = targetParent.lossyScale;
        if (parentLossyScale.x > 0 && parentLossyScale.y > 0 && parentLossyScale.z > 0)
        {
            newWeaponInstance.transform.localScale = new Vector3(
                1f / parentLossyScale.x,
                1f / parentLossyScale.y,
                1f / parentLossyScale.z
            );
        }

        giantWeaponInScene = newWeaponInstance.transform;
        _currentWeaponIndex = weaponIndex;

        if (weaponZoomFramer != null)
        {
            weaponZoomFramer.SetTarget(giantWeaponInScene);
        }
    }

    // Notice we removed the Particle System array entirely. 
    // It now grabs the active particle system directly from the spawned weapon.
    public void PlayActiveWeaponParticle()
    {
        if (giantWeaponInScene != null)
        {
            ParticleSystem activeParticles = giantWeaponInScene.GetComponentInChildren<ParticleSystem>();
            if (activeParticles != null)
            {
                activeParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                activeParticles.Play(true);
            }
        }
    }
}