using UnityEngine;
using DG.Tweening; // Required for the animation!

[RequireComponent(typeof(LineRenderer))]
public class HighScoreVisuals : MonoBehaviour
{
    public static HighScoreVisuals Instance { get; private set; }

    [Header("Dynamic Chunk/Terrain Loading")]
    [Tooltip("How many meters before reaching the best record should the line and flag generate? Guarantees terrain is loaded!")]
    public float generationDistanceThreshold = 200f; // <-- NEW: Configurable threshold!
    private bool _hasGeneratedForCurrentRun = false;
    private float _lastBoulderZ = 0f;

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
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        _lineRenderer = GetComponent<LineRenderer>();
        _lineRenderer.positionCount = 0; // Keep empty at boot until we get close!

        // REMOVED: Invoke(nameof(GenerateVisuals), 0.1f);
        // We now let Update() trigger this dynamically when within 200m!
    }

    private void Update()
    {
        if (PlayerDataManager.Instance == null || ArcadeBoulder.Instance == null) return;

        float currentBoulderZ = ArcadeBoulder.Instance.transform.position.z;

        // --- 1. AUTO-RESET SHIELD ---
        // If the player restarted/respawned back at the launch pad (< 10m) after a deep run, reset our flag!
        if (_hasGeneratedForCurrentRun && currentBoulderZ < 10f && _lastBoulderZ > 50f)
        {
            _hasGeneratedForCurrentRun = false;
            _lineRenderer.positionCount = 0;
            if (_spawnedFlag != null) Destroy(_spawnedFlag);
        }

        _lastBoulderZ = currentBoulderZ;

        // If we already generated for this attempt, stop checking
        if (_hasGeneratedForCurrentRun) return;

        float bestDistance = PlayerDataManager.Instance.data.bestDistance;
        if (bestDistance < 1f) return;

        // --- 2. JUST-IN-TIME (JIT) DISTANCE CHECK ---
        float distanceToRecord = bestDistance - currentBoulderZ;

        // When the boulder rolls within 200m of the record, Terrain 2 is loaded -> GENERATE!
        if (distanceToRecord <= generationDistanceThreshold)
        {
            _hasGeneratedForCurrentRun = true;
            GenerateVisuals();
        }
    }

    public void GenerateVisuals()
    {
        if (PlayerDataManager.Instance == null) return;

        float bestDistance = PlayerDataManager.Instance.data.bestDistance;
        if (bestDistance < 1f) return;

        int actualResolution = Mathf.Max(lineResolution, 60);
        _lineRenderer.positionCount = actualResolution;

        float startX = -trackWidth / 2f;
        float stepX = trackWidth / (actualResolution - 1);

        Vector3 flagPosition = Vector3.zero;
        bool foundFlagSpot = false;

        for (int i = 0; i < actualResolution; i++)
        {
            float currentX = startX + (stepX * i);
            Vector3 rayOrigin = new Vector3(currentX, 500f, bestDistance);

            // Ignore trigger boxes (coins, checkpoints) so line hugs the actual dirt
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 1000f, terrainLayer, QueryTriggerInteraction.Ignore))
            {
                Vector3 pointPos = hit.point + (hit.normal * heightOffset);
                _lineRenderer.SetPosition(i, pointPos);

                if (placeFlagOnEdge && i == actualResolution - 1)
                {
                    flagPosition = hit.point;
                    foundFlagSpot = true;
                }
                else if (!placeFlagOnEdge && i == actualResolution / 2)
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

        // --- NEW: POP-IN ANIMATION WHEN GENERATING ON THE FLY ---
        if (foundFlagSpot && flagPrefab != null && _spawnedFlag == null)
        {
            _spawnedFlag = Instantiate(flagPrefab, flagPosition, Quaternion.identity, transform);

            // Give it a slick arcade scale-up so generating within camera view looks intentional!
            _spawnedFlag.transform.DOKill();
            _spawnedFlag.transform.localScale = Vector3.zero;
            _spawnedFlag.transform.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack);
        }
    }

    public void PlantNewRecordFlag(Vector3 boulderDeathPosition)
    {
        if (flagPrefab == null) return;

        Vector3 rayOrigin = new Vector3(boulderDeathPosition.x, 500f, boulderDeathPosition.z);

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 1000f, terrainLayer, QueryTriggerInteraction.Ignore))
        {
            if (_spawnedFlag != null) Destroy(_spawnedFlag);

            _spawnedFlag = Instantiate(flagPrefab, hit.point + (Vector3.up * 5f), Quaternion.identity, transform);
            _spawnedFlag.transform.localScale = Vector3.zero;

            Sequence flagSeq = DOTween.Sequence();

            flagSeq.Append(_spawnedFlag.transform.DOMoveY(hit.point.y, 0.4f).SetEase(Ease.InExpo));
            flagSeq.Join(_spawnedFlag.transform.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack));

            if (confettiPrefab != null)
            {
                flagSeq.AppendCallback(() =>
                {
                    GameObject confetti = Instantiate(confettiPrefab, hit.point + (Vector3.up * 0.2f), Quaternion.Euler(-90f, 0f, 0f));
                });
            }

            flagSeq.Append(_spawnedFlag.transform.DOPunchRotation(new Vector3(25f, 0f, 25f), 0.4f, 10, 1f));
            flagSeq.Join(_spawnedFlag.transform.DOPunchScale(new Vector3(0.3f, -0.3f, 0.3f), 0.4f, 5, 0.5f));
        }
    }
}