using UnityEngine;

public class FragileDestructible : MonoBehaviour
{
    [Header("Visuals")]
    public GameObject dustEffectPrefab;

    private bool _isBroken = false;

    void OnTriggerEnter(Collider other)
    {
        if (_isBroken) return;

        if (other.CompareTag("Boulder"))
        {
            _isBroken = true;

            // Debug the percentage to the console
            Debug.Log($"[FragileDestructible] {gameObject.name} crushed! Damage Penalty: 0%");

            // Play the dust poof effect
            if (dustEffectPrefab != null)
            {
                Vector3 hitPoint = other.ClosestPoint(transform.position);
                ObjectPooler.Instance.Spawn(dustEffectPrefab, hitPoint, Quaternion.identity);
            }

            // Instantly vanish without slowing the boulder down at all
            Destroy(gameObject);
        }
    }
}