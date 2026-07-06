using System.Collections.Generic;
using UnityEngine;

namespace ShipSalvage.Core
{
    public class SpawnPoints : MonoBehaviour
    {
        public static SpawnPoints Instance { get; private set; }

        [SerializeField] private List<Transform> _playerSpawns = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public Vector3 GetPlayerSpawn()
        {
            if (_playerSpawns == null || _playerSpawns.Count == 0) return Vector3.zero;

            var t = _playerSpawns[Random.Range(0, _playerSpawns.Count)];
            return t != null ? t.position : Vector3.zero;
        }
    }
}
