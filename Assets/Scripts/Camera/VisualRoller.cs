using UnityEngine;

public class VisualRoller : MonoBehaviour
{
    [Header("References")]
    public Rigidbody parentRb;

    [Header("Settings")]
    public float baseRadius = 0.5f;

    [Tooltip("Lower this if the ball looks like it's spinning way too fast! (Try 0.3 to 0.5)")]
    public float rotationSpeedMultiplier = 0.5f;

    [Tooltip("Capping the max degrees per frame stops the violent vibrating illusion.")]
    public float maxDegreesPerFrame = 25f;

    void LateUpdate()
    {
        if (parentRb == null || parentRb.velocity.sqrMagnitude < 0.01f) return;

        float currentRadius = baseRadius * parentRb.transform.localScale.x;

        // Apply our fake visual multiplier to slow down the perceived spin
        float distanceThisFrame = parentRb.velocity.magnitude * Time.deltaTime * rotationSpeedMultiplier;

        float circumference = 2 * Mathf.PI * currentRadius;
        float angleToRotate = (distanceThisFrame / circumference) * 360f;

        // CLAMP IT: Never let it rotate more than X degrees in a single frame
        // This completely eliminates the "shaking" optical illusion at high speeds
        angleToRotate = Mathf.Clamp(angleToRotate, 0f, maxDegreesPerFrame);

        Vector3 rotationAxis = Vector3.Cross(Vector3.up, parentRb.velocity.normalized);

        // Spin in world space
        transform.Rotate(rotationAxis, angleToRotate, Space.World);
    }
}