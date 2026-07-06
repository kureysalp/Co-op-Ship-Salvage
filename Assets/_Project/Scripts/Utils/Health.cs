using System;
using Unity.Netcode;
using UnityEngine;

namespace ShipSalvage.Utils
{
    public class Health : NetworkBehaviour, IDamageable
    {
        [SerializeField] private float _max = 100f;

        private readonly NetworkVariable<float> _current = new();

        public float Max => _max;
        public float Current => _current.Value;
        public bool IsDead => _current.Value <= 0f;

        public event Action<ulong> OnDeath;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
                _current.Value = _max;
        }

        public void TakeDamage(float amount, ulong instigatorClientId)
        {
            if (!IsServer) return;
            if (_current.Value <= 0f) return;

            _current.Value = Mathf.Max(0f, _current.Value - amount);

            if (_current.Value <= 0f)
                OnDeath?.Invoke(instigatorClientId);
        }

        public void ResetHealth()
        {
            if (!IsServer) return;
            _current.Value = _max;
        }
    }
}
