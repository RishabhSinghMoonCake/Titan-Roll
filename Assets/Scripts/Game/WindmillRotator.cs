using System.Collections;
using UnityEngine;

public class WindmillRotator : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("How fast the windmill spins (Total degrees per second)")]
    public float rotationSpeed = 30f;

    [Tooltip("Which axis should it spin around?")]
    public Vector3 rotationAxis = Vector3.forward;

    [Header("Optimization")]
    [Tooltip("How many times per second should the math actually run?")]
    public int updatesPerSecond = 5;

    private void Start()
    {
        // Kick off the endless loop as soon as the object loads
        if (updatesPerSecond > 0)
        {
            StartCoroutine(SteppedRotationRoutine());
        }
    }

    private IEnumerator SteppedRotationRoutine()
    {
        // 1. Calculate the exact math once at the start
        float waitTime = 1f / updatesPerSecond;
        float degreesPerTick = rotationSpeed * waitTime;

        // 2. CACHE THE YIELD INSTRUCTION! 
        // Doing 'new WaitForSeconds' inside a while(true) loop creates memory garbage every tick. 
        // Saving it to a variable first means zero garbage collection!
        WaitForSeconds cachedWait = new WaitForSeconds(waitTime);

        // 3. The infinite loop
        while (true)
        {
            // Apply the chunk of rotation
            transform.Rotate(rotationAxis * degreesPerTick, Space.Self);

            // Go completely to sleep and cost 0 CPU cycles until the next tick
            yield return cachedWait;
        }
    }
}