using UnityEngine;

public class CharacterSkinManager : MonoBehaviour
{
    [Header("Hierarchy")]
    public Transform visualHolder;

    [Header("Prefabs & Scene Objects")]
    public GameObject[] characterPrefabs;

    [Tooltip("Drag the Giant Weapon that is ALREADY IN THE SCENE into this slot!")]
    public Transform giantWeaponInScene;

    [Header("Runtime References (Read Only)")]
    public Animator currentActiveAnimator;
    public Transform currentWeaponSocket; // We store the socket so we can use it later

    private GameObject _currentSkinInstance;

    public void EquipCharacterSkin(int skinIndex)
    {
        if (characterPrefabs == null || characterPrefabs.Length == 0) return;
        skinIndex = Mathf.Clamp(skinIndex, 0, characterPrefabs.Length - 1);

        if (_currentSkinInstance != null) Destroy(_currentSkinInstance);

        _currentSkinInstance = Instantiate(characterPrefabs[skinIndex], visualHolder);
        _currentSkinInstance.transform.localPosition = Vector3.zero;
        _currentSkinInstance.transform.localRotation = Quaternion.identity;

        SkinReferences references = _currentSkinInstance.GetComponent<SkinReferences>();
        if (references != null)
        {
            currentActiveAnimator = references.skinAnimator;
            currentWeaponSocket = references.weaponSocket;

            // We leave the giantWeaponInScene right where it is on the ground!
        }
    }
}