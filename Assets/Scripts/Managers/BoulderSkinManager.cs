using UnityEngine;

public class BoulderSkinManager : MonoBehaviour
{
    [Header("Hierarchy Setup")]
    [Tooltip("The empty child GameObject inside the Boulder Parent")]
    public Transform visualHolder;

    [Header("Available Skins")]
    public GameObject[] skinPrefabs;

    private GameObject _currentVisualInstance;

    // Call this directly from your UI Shop Button OnClick event
    public void EquipSkin(int skinIndex)
    {
        if (skinIndex < 0 || skinIndex >= skinPrefabs.Length) return;

        // 1. Remove the old skin instantly
        if (_currentVisualInstance != null)
        {
            Destroy(_currentVisualInstance);
        }

        // 2. Spawn the new skin
        _currentVisualInstance = Instantiate(skinPrefabs[skinIndex], visualHolder);

        // 3. Reset local transforms so it centers perfectly inside the holder
        _currentVisualInstance.transform.localPosition = Vector3.zero;
        _currentVisualInstance.transform.localRotation = Quaternion.identity;

        // We force the local scale to 1. The visual child gets its actual size 
        // by inheriting the scale of the Boulder Parent.
        _currentVisualInstance.transform.localScale = Vector3.one;

        Debug.Log($"Equipped Skin Index: {skinIndex}");
    }
}