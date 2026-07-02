using UnityEngine;
using DG.Tweening; // Required for the animation!

[RequireComponent(typeof(LineRenderer))]
public class HighScoreVisuals : MonoBehaviour
{
    public static HighScoreVisuals Instance { get; private set; }

    [Header("Line Settings")]
    public float trackWidth = 30f;
    public int lineResolution = 20;
    public float heightOffset = 0.5f;
    public LayerMask terrainLayer = ~0;

    [Header("Flag Setup")]
    public GameObject flagPrefab;
    public bool placeFlagOnEdge = true;

    [Header("Effects")]
    [Tooltip("Particle system prefab to spawn when the flag hits the ground")]
    public GameObject confettiPrefab;

    private LineRenderer _lineRenderer;
    private GameObject _spawnedFlag;

    private void Awake()
    {
        // Set up the Singleton so the Game Manager can call this script easily
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        _lineRenderer = GetComponent<LineRenderer>();
        Invoke(nameof(GenerateVisuals), 0.1f);
    }

    public void GenerateVisuals()
    {
        if (PlayerDataManager.Instance == null) return;

        float bestDistance = PlayerDataManager.Instance.data.bestDistance;

        if (bestDistance < 1f)
        {
            _lineRenderer.positionCount = 0;
            return;
        }

        _lineRenderer.positionCount = lineResolution;
        float startX = -trackWidth / 2f;
        float stepX = trackWidth / (lineResolution - 1);

        Vector3 flagPosition = Vector3.zero;
        bool foundFlagSpot = false;

        for (int i = 0; i < lineResolution; i++)
        {
            float currentX = startX + (stepX * i);
            Vector3 rayOrigin = new Vector3(currentX, 500f, bestDistance);

            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 1000f, terrainLayer))
            {
                Vector3 pointPos = hit.point + (Vector3.up * heightOffset);
                _lineRenderer.SetPosition(i, pointPos);

                if (placeFlagOnEdge && i == lineResolution - 1)
                {
                    flagPosition = hit.point;
                    foundFlagSpot = true;
                }
                else if (!placeFlagOnEdge && i == lineResolution / 2)
                {
                    flagPosition = hit.point;
                    foundFlagSpot = true;
                }
            }
            else
            {
                _lineRenderer.SetPosition(i, new Vector3(currentX, 0f, bestDistance));
            }
        }

        if (foundFlagSpot && flagPrefab != null && _spawnedFlag == null)
        {
            _spawnedFlag = Instantiate(flagPrefab, flagPosition, Quaternion.identity, transform);
        }
    }

    public void PlantNewRecordFlag(Vector3 boulderDeathPosition)
    {
        if (flagPrefab == null) return;

        Vector3 rayOrigin = new Vector3(boulderDeathPosition.x, 500f, boulderDeathPosition.z);

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 1000f, terrainLayer))
        {
            if (_spawnedFlag != null) Destroy(_spawnedFlag);

            _spawnedFlag = Instantiate(flagPrefab, hit.point + (Vector3.up * 5f), Quaternion.identity, transform);
            _spawnedFlag.transform.localScale = Vector3.zero;

            Sequence flagSeq = DOTween.Sequence();

            // 1. Slam down to the ground while scaling up
            flagSeq.Append(_spawnedFlag.transform.DOMoveY(hit.point.y, 0.4f).SetEase(Ease.InExpo));
            flagSeq.Join(_spawnedFlag.transform.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack));

            // --- NEW: Fire the Confetti exactly on impact ---
            if (confettiPrefab != null)
            {
                flagSeq.AppendCallback(() =>
                {
                    // Pointing up (-90 on X) so it blasts into the air like a fountain
                    GameObject confetti = Instantiate(confettiPrefab, hit.point + (Vector3.up * 0.2f), Quaternion.Euler(-90f, 0f, 0f));
                });
            }

            // 2. Violent shake and squash/stretch on impact
            flagSeq.Append(_spawnedFlag.transform.DOPunchRotation(new Vector3(25f, 0f, 25f), 0.4f, 10, 1f));
            flagSeq.Join(_spawnedFlag.transform.DOPunchScale(new Vector3(0.3f, -0.3f, 0.3f), 0.4f, 5, 0.5f));
        }
    }
}