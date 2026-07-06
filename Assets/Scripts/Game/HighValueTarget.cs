using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider))]
public class HighValueTarget : MonoBehaviour
{
    [Header("Rewards")]
    [Tooltip("The massive payout for hitting this rare target.")]
    public float rewardGold = 1000f;

    [Header("Visuals & Effects")]
    public GameObject collectionEffectPrefab;

    [Header("Retro Spin Animation")]
    [Tooltip("Assign the visual child object here so the physical collider doesn't spin.")]
    public Transform visualMesh;
    [Tooltip("How long it takes to complete one full 360-degree rotation.")]
    public float spinDuration = 1.5f;
    [Tooltip("Target frames per second for that choppy, retro arcade look.")]
    public int spinFPS = 15;

    private string _uniqueHashID;
    private bool _isCollected = false;

    private void Awake()
    {
        // 1. GENERATE UNIQUE HASH
        float posX = Mathf.Round(transform.position.x * 10f);
        float posY = Mathf.Round(transform.position.y * 10f);
        float posZ = Mathf.Round(transform.position.z * 10f);

        _uniqueHashID = $"HVT_{gameObject.name}_{posX}_{posY}_{posZ}";

        // 2. CHECK IF ALREADY COLLECTED
        if (PlayerPrefs.GetInt(_uniqueHashID, 0) == 1)
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Kick off the low-framerate spin routine
        StartCoroutine(RetroSpinRoutine());
    }

    private IEnumerator RetroSpinRoutine()
    {
        Transform targetToSpin = visualMesh != null ? visualMesh : transform;

        // Calculate the timing and movement for the stepped frames
        float waitTime = 1f / spinFPS;
        WaitForSeconds wait = new WaitForSeconds(waitTime);

        // How many degrees it needs to turn per frame to complete 360 in 'spinDuration'
        float degreesPerSecond = 360f / spinDuration;
        float degreesPerStep = degreesPerSecond * waitTime;

        while (!_isCollected)
        {
            // Apply the rotation instantly, then wait for the next specific frame tick
            targetToSpin.Rotate(0f, degreesPerStep, 0f, Space.Self);
            yield return wait;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (_isCollected) return;

        if (other.CompareTag("Boulder"))
        {
            ArcadeBoulder boulder = other.GetComponent<ArcadeBoulder>();
            if (boulder == null) return;

            // --- 1. LOCK IT DOWN ---
            _isCollected = true;
            GetComponent<Collider>().enabled = false;

            // --- 2. SAVE TO PLAYER PREFS ---
            PlayerPrefs.SetInt(_uniqueHashID, 1);
            PlayerPrefs.Save();

            // --- 3. PAY THE PLAYER ---
            if (RewardManager.Instance != null)
            {
                RewardManager.Instance.ProcessDestructionReward(rewardGold, transform.position.z);
            }

            if (BoulderComboText.Instance != null)
            {
                BoulderComboText.Instance.AddGold(rewardGold);
            }

            // --- 4. SPAWN EFFECTS & DESTROY ---
            ShatterAndCollect();
        }
    }

    private void ShatterAndCollect()
    {
        if (collectionEffectPrefab != null)
        {
            if (ObjectPooler.Instance != null)
            {
                ObjectPooler.Instance.Spawn(collectionEffectPrefab, transform.position, Quaternion.identity);
            }
            else
            {
                Instantiate(collectionEffectPrefab, transform.position, Quaternion.identity);
            }
        }

        // THE FIX: Removed transform.parent destruction entirely.
        // It now strictly destroys ONLY this target object!
        Destroy(gameObject);
    }

    public void DebugResetTarget()
    {
        PlayerPrefs.SetInt(_uniqueHashID, 0);
        PlayerPrefs.Save();
    }
}