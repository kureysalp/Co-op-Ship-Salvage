using System.Collections.Generic;
using UnityEngine;

namespace ShipSalvage.Utils
{
    public class VfxPool : MonoBehaviour
    {
        public static VfxPool Instance { get; private set; }

        [SerializeField] private GameObject _tracerPrefab;
        [SerializeField] private GameObject _hitPrefab;
        [SerializeField] private float _tracerSpeed = 120f;
        [SerializeField] private int _tracerPrewarm = 8;
        [SerializeField] private int _hitPrewarm = 8;

        public float TracerSpeed => _tracerSpeed;

        private readonly Dictionary<GameObject, Queue<GameObject>> _pools = new();
        private readonly Dictionary<GameObject, GameObject> _prefabOf = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            Prewarm(_tracerPrefab, _tracerPrewarm);
            Prewarm(_hitPrefab, _hitPrewarm);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null) return null;

            if (!_pools.TryGetValue(prefab, out var queue))
            {
                queue = new Queue<GameObject>();
                _pools[prefab] = queue;
            }

            GameObject instance = queue.Count > 0 ? queue.Dequeue() : Instantiate(prefab, transform);
            _prefabOf[instance] = prefab;

            instance.transform.SetPositionAndRotation(position, rotation);
            instance.SetActive(true);
            return instance;
        }

        public void Release(GameObject instance)
        {
            if (instance == null) return;

            instance.SetActive(false);

            if (_prefabOf.TryGetValue(instance, out var prefab) && _pools.TryGetValue(prefab, out var queue))
                queue.Enqueue(instance);
            else
                Destroy(instance);
        }

        public void SpawnTracer(Vector3 from, Vector3 to, Vector3 normal, bool didHit)
        {
            if (_tracerPrefab == null) return;

            Vector3 direction = to - from;
            Quaternion rotation = direction.sqrMagnitude > 0f ? Quaternion.LookRotation(direction) : Quaternion.identity;

            var instance = Get(_tracerPrefab, from, rotation);
            if (instance == null) return;

            var bullet = instance.GetComponent<BulletVisual>();
            if (bullet != null)
                bullet.Launch(from, to, normal, didHit);
            else
                Release(instance);
        }

        public void SpawnHit(Vector3 point, Vector3 normal)
        {
            if (_hitPrefab == null) return;

            Quaternion rotation = normal.sqrMagnitude > 0f ? Quaternion.LookRotation(normal) : Quaternion.identity;

            var instance = Get(_hitPrefab, point, rotation);
            if (instance == null) return;

            var particle = instance.GetComponent<PooledParticle>();
            if (particle == null)
                particle = instance.AddComponent<PooledParticle>();

            particle.Play();
        }

        private void Prewarm(GameObject prefab, int count)
        {
            if (prefab == null || count <= 0) return;

            var spawned = new List<GameObject>(count);
            for (int i = 0; i < count; i++)
                spawned.Add(Get(prefab, Vector3.zero, Quaternion.identity));

            for (int i = 0; i < spawned.Count; i++)
                Release(spawned[i]);
        }
    }
}
