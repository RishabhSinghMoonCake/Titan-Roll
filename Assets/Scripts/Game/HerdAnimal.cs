using UnityEngine;
using UnityEngine.Pool;

public class HerdAnimal : MonoBehaviour
{
    private IObjectPool<HerdAnimal> _pool;
    private float _speed;
    private float _targetX;
    private bool _movingRight;

    // Called by the Spawner right after the sheep is pulled from the pool
    public void Initialize(IObjectPool<HerdAnimal> pool, float speed, float targetX)
    {
        _pool = pool;
        _speed = speed;
        _targetX = targetX;

        // Determine direction so we know when to despawn
        _movingRight = transform.position.x < targetX;
    }

    void Update()
    {
        // Because the spawner rotates the sheep to face the target, 
        // moving "forward" in local space automatically moves it across the track!
        transform.Translate(Vector3.forward * _speed * Time.deltaTime);

        // Check if we have crossed the finish line
        if (_movingRight && transform.position.x >= _targetX)
        {
            _pool.Release(this); // Go back to sleep in the pool
        }
        else if (!_movingRight && transform.position.x <= _targetX)
        {
            _pool.Release(this); // Go back to sleep in the pool
        }
    }
}