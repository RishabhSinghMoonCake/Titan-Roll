using UnityEngine;
using System.Collections.Generic;

public class ObjectPooler : MonoBehaviour
{
    public static ObjectPooler Instance;

    // Dictionary Key = The unique Instance ID of the PREFAB asset
    // Value = A Queue of waiting objects
    private Dictionary<int, Queue<GameObject>> poolDictionary = new Dictionary<int, Queue<GameObject>>();

    // Dictionary to link an active object back to its origin pool ID
    private Dictionary<int, int> activeObjectOrigin = new Dictionary<int, int>();

    void Awake()
    {
        Instance = this;
    }

    /// <summary>
    /// Spawns an object from the pool. Creates a new pool if one doesn't exist.
    /// </summary>
    public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        int poolKey = prefab.GetInstanceID();

        // 1. Create Pool if missing
        if (!poolDictionary.ContainsKey(poolKey))
        {
            poolDictionary.Add(poolKey, new Queue<GameObject>());
        }

        GameObject objToSpawn;

        // 2. Try to get object from queue
        if (poolDictionary[poolKey].Count > 0)
        {
            objToSpawn = poolDictionary[poolKey].Dequeue();
        }
        else
        {
            // 3. Queue empty? Create new one (Expand pool)
            objToSpawn = Instantiate(prefab);
        }

        // 4. Setup
        objToSpawn.transform.position = position;
        objToSpawn.transform.rotation = rotation;
        objToSpawn.SetActive(true);

        // 5. Track origin (so we know where to return it later)
        int objId = objToSpawn.GetInstanceID();
        if (!activeObjectOrigin.ContainsKey(objId))
        {
            activeObjectOrigin.Add(objId, poolKey);
        }

        // 6. Call Reset Logic (Interface)
        IPooledObject pooledObj = objToSpawn.GetComponent<IPooledObject>();
        if (pooledObj != null)
        {
            pooledObj.OnObjectSpawn();
        }

        return objToSpawn;
    }

    /// <summary>
    /// Returns an object to its original pool.
    /// </summary>
    public void ReturnToPool(GameObject obj)
    {
        int objId = obj.GetInstanceID();

        if (activeObjectOrigin.ContainsKey(objId))
        {
            int poolKey = activeObjectOrigin[objId];

            obj.SetActive(false);

            // Add back to queue
            if (poolDictionary.ContainsKey(poolKey))
            {
                poolDictionary[poolKey].Enqueue(obj);
            }
        }
        else
        {
            // Fallback if not pooled
            Destroy(obj);
        }
    }
}

// Interface for objects that need reset logic
public interface IPooledObject
{
    void OnObjectSpawn();
}