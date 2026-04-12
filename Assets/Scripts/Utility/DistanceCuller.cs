using UnityEngine;
using System.Collections;

public class DistanceCuller : MonoBehaviour
{
    [Tooltip("How far ahead of the player this object should turn on")]
    public float renderDistanceForward = 250f;
    [Tooltip("How far behind the player this object should turn off completely")]
    public float cullDistanceBehind = 50f;

    // We cache the child object that holds the MeshRenderer and Colliders.
    // (Don't disable the root object, or this script will stop running!)
    public GameObject visualAndPhysicsRoot;

    private Transform _playerTransform;

    private void Start()
    {
        // Stagger the start times slightly so 10,000 flowers don't all run their math on the exact same frame
        StartCoroutine(CullRoutine(Random.Range(0f, 0.5f)));
    }

    private IEnumerator CullRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);

        // Find the boulder (Make sure your ArcadeBoulder has this tag!)
        GameObject player = GameObject.FindGameObjectWithTag("Boulder");
        if (player != null) _playerTransform = player.transform;

        while (true)
        {
            if (_playerTransform != null && visualAndPhysicsRoot != null)
            {
                float zDistance = transform.position.z - _playerTransform.position.z;

                // If it's in front of us (within 250m) OR just slightly behind us (within 50m), turn it ON
                if (zDistance < renderDistanceForward && zDistance > -cullDistanceBehind)
                {
                    if (!visualAndPhysicsRoot.activeSelf) visualAndPhysicsRoot.SetActive(true);
                }
                // Otherwise, turn it OFF
                else
                {
                    if (visualAndPhysicsRoot.activeSelf) visualAndPhysicsRoot.SetActive(false);
                }
            }

            // Only check twice a second. Checking distance is cheap, but doing it 60 times a second for 10,000 objects is not!
            yield return new WaitForSeconds(0.5f);
        }
    }
}