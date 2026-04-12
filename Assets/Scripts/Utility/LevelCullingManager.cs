using UnityEngine;
using System.Collections;

public class LevelCullingManager : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Drag the ArcadeBoulder here")]
    public Transform player;

    [Tooltip("Drag all your Chunk Parent GameObjects here")]
    public GameObject[] levelChunks;

    [Header("Culling Distances")]
    [Tooltip("How far ahead to load chunks (Make this large enough to hide pop-in)")]
    public float renderDistanceForward = 400f;
    [Tooltip("How far behind before unloading chunks")]
    public float cullDistanceBehind = 150f;

    private void Start()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Boulder");
            if (p != null) player = p.transform;
        }

        // Run the check twice a second, not every frame!
        StartCoroutine(CullRoutine());
    }

    private IEnumerator CullRoutine()
    {
        while (true)
        {
            if (player != null)
            {
                float playerZ = player.position.z;

                for (int i = 0; i < levelChunks.Length; i++)
                {
                    GameObject chunk = levelChunks[i];
                    if (chunk == null) continue;

                    float chunkZ = chunk.transform.position.z;

                    // Is the chunk within our visual range?
                    bool shouldBeActive = (chunkZ > playerZ - cullDistanceBehind) &&
                                          (chunkZ < playerZ + renderDistanceForward);

                    // Only call SetActive if the state actually needs to change
                    if (chunk.activeSelf != shouldBeActive)
                    {
                        chunk.SetActive(shouldBeActive);
                    }
                }
            }

            // Checking 30 chunks twice a second costs 0.0001% of your CPU. 
            // This replaces 5,000 individual checks!
            yield return new WaitForSeconds(0.5f);
        }
    }
}