using UnityEngine;

public class InfiniteTrainTrack : MonoBehaviour
{
    public enum TrackDirection
    {
        Forward,
        Reverse,
        Random
    }

    [Header("Track Reference Points")]
    [Tooltip("Where trains spawn/enter the screen.")]
    public Transform startPoint;
    [Tooltip("Where trains despawn/exit the screen.")]
    public Transform endPoint;

    [Header("Train Setup")]
    [Tooltip("The train car prefabs to use. You can assign different cars (Engine, Cargo, Caboose) and it will cycle through them.")]
    public GameObject[] trainPrefabs;
    [Tooltip("Total number of cars to keep in the loop. If 3 fit on screen, set this to 4 or 5 to prevent visual popping.")]
    public int totalCarsInLoop = 4;
    [Tooltip("Exact distance between the center of one train car and the next.")]
    public float carSpacing = 12f;

    [Header("Movement & Controls")]
    public float speed = 25f;
    public TrackDirection initialDirection = TrackDirection.Forward;
    [Tooltip("If true, the track will randomize its direction every time the scene starts.")]
    public bool randomizeDirectionOnStart = false;

    // Internal State
    private Transform[] _trainCars;
    private Vector3 _moveDirection;
    private float _trackLength;
    private bool _isMovingForward = true;

    private void Start()
    {
        if (startPoint == null || endPoint == null || trainPrefabs == null || trainPrefabs.Length == 0)
        {
            Debug.LogError($"[InfiniteTrainTrack] Missing setup references on {gameObject.name}!");
            enabled = false;
            return;
        }

        // 1. Determine Direction
        if (randomizeDirectionOnStart || initialDirection == TrackDirection.Random)
        {
            _isMovingForward = Random.value > 0.5f;
        }
        else
        {
            _isMovingForward = (initialDirection == TrackDirection.Forward);
        }

        // 2. Calculate Track Math
        Vector3 trackVector = endPoint.position - startPoint.position;
        _trackLength = trackVector.magnitude;
        _moveDirection = trackVector.normalized;

        if (!_isMovingForward)
        {
            _moveDirection = -_moveDirection; // Reverse travel direction
        }

        // 3. Initialize & Position the Train Ring Buffer
        SpawnAndAlignTrains();
    }

    private void SpawnAndAlignTrains()
    {
        _trainCars = new Transform[totalCarsInLoop];

        // Determine orientation: Trains should face the direction they are traveling
        Quaternion lookRotation = _moveDirection != Vector3.zero ? Quaternion.LookRotation(_moveDirection) : Quaternion.identity;

        for (int i = 0; i < totalCarsInLoop; i++)
        {
            // Pick a prefab (cycles through the array if you have multiple car designs)
            GameObject prefabToSpawn = trainPrefabs[i % trainPrefabs.Length];

            // Calculate starting position along the track line
            // We space them out backwards against the move direction so they line up neatly
            float offsetDistance = i * carSpacing;
            Vector3 spawnPos = _isMovingForward
                ? startPoint.position - (_moveDirection * offsetDistance)
                : endPoint.position - (_moveDirection * offsetDistance);

            GameObject newCar = Instantiate(prefabToSpawn, spawnPos, lookRotation, transform);
            _trainCars[i] = newCar.transform;
        }
    }

    private void Update()
    {
        if (!enabled || _trainCars == null || _trainCars.Length == 0)
            return;

        if (startPoint == null || endPoint == null)
            return;

        float moveStep = speed * Time.deltaTime;

        for (int i = 0; i < _trainCars.Length; i++)
        {
            Transform car = _trainCars[i];

            if (car == null)
                continue;

            car.position += _moveDirection * moveStep;

            if (HasCrossedThreshold(car.position))
            {
                RecycleCarToBackOfLine(car);
            }
        }
    }

    /// <summary>
    /// Checks if a train car has passed the end point (if moving forward) or start point (if moving in reverse).
    /// Uses Dot Product for ultra-fast, foolproof plane-crossing detection.
    /// </summary>
    private bool HasCrossedThreshold(Vector3 carPosition)
    {
        if (startPoint == null || endPoint == null)
            return false;

        Vector3 targetPoint = _isMovingForward ? endPoint.position : startPoint.position;
        Vector3 vectorToCar = carPosition - targetPoint;

        return Vector3.Dot(vectorToCar, _moveDirection) > 0f;
    }

    /// <summary>
    /// Teleports the exited car directly behind the furthest trailing car in the buffer.
    /// </summary>
    private void RecycleCarToBackOfLine(Transform carToRecycle)
    {
        if (carToRecycle == null)
            return;

        Transform furthestTrailingCar = null;
        float maxTrailingDistance = float.NegativeInfinity;

        Vector3 referencePoint = _isMovingForward
            ? startPoint.position
            : endPoint.position;

        for (int i = 0; i < _trainCars.Length; i++)
        {
            Transform otherCar = _trainCars[i];

            if (otherCar == null || otherCar == carToRecycle)
                continue;

            Vector3 vectorFromStart = otherCar.position - referencePoint;
            float distanceAlongTrack = Vector3.Dot(vectorFromStart, _moveDirection);

            if (-distanceAlongTrack > maxTrailingDistance)
            {
                maxTrailingDistance = -distanceAlongTrack;
                furthestTrailingCar = otherCar;
            }
        }

        // No valid cars left.
        if (furthestTrailingCar == null)
            return;

        carToRecycle.position = furthestTrailingCar.position - (_moveDirection * carSpacing);
    }

    /// <summary>
    /// Runtime control: Instantly reverse the train's travel direction!
    /// </summary>
    public void ToggleDirection()
    {
        _isMovingForward = !_isMovingForward;
        _moveDirection = -_moveDirection;

        if (_trainCars == null)
            return;

        for (int i = 0; i < _trainCars.Length; i++)
        {
            if (_trainCars[i] == null)
                continue;

            _trainCars[i].forward = _moveDirection;
        }
    }

    /// <summary>
    /// Runtime control: Change track speed dynamically.
    /// </summary>
    public void SetSpeed(float newSpeed)
    {
        speed = Mathf.Max(0f, newSpeed);
    }

    // Draw visual helper lines in the Unity Scene view so you can easily adjust spacing!
    private void OnDrawGizmosSelected()
    {
        if (startPoint != null && endPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(startPoint.position, endPoint.position);
            Gizmos.DrawWireSphere(startPoint.position, 1f);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(endPoint.position, 1f);
        }
    }
}