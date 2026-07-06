using ShipSalvage.Player;
using ShipSalvage.Utils;
using Unity.Netcode;
using UnityEngine;

namespace ShipSalvage.AI
{
    public class AiGun : NetworkBehaviour
    {
        [SerializeField] private Transform _muzzle;
        [SerializeField] private GunData _data;
        [SerializeField] private LayerMask _hitMask = ~0;

        private int _currentAmmo;
        private double _lastFireTime;
        private double _reloadEndTime;
        private bool _isReloading;

        public Transform Muzzle => _muzzle;

        public override void OnNetworkSpawn()
        {
            if (IsServer && _data != null)
                _currentAmmo = _data.MagazineSize;
        }

        public bool TryFire(Vector3 targetPoint)
        {
            if (!IsServer || _data == null) return false;

            double now = NetworkManager.ServerTime.Time;

            if (_isReloading)
            {
                if (now < _reloadEndTime) return false;
                _currentAmmo = _data.MagazineSize;
                _isReloading = false;
            }

            if (_currentAmmo <= 0)
            {
                _isReloading = true;
                _reloadEndTime = now + _data.ReloadTime;
                return false;
            }

            if (now - _lastFireTime < 1.0 / _data.FireRate) return false;

            Vector3 origin = _muzzle != null ? _muzzle.position : transform.position;
            Vector3 direction = (targetPoint - origin).normalized;

            _currentAmmo--;
            _lastFireTime = now;

            bool didHit = WeaponSystem.Hitscan(origin, direction, _data.Range, _data.Damage, _hitMask, NetworkManager.ServerClientId, out var end, out var normal);
            FireVisualClientRpc(origin, end, normal, didHit);
            return true;
        }

        [ClientRpc]
        private void FireVisualClientRpc(Vector3 start, Vector3 end, Vector3 normal, bool didHit)
        {
            if (VfxPool.Instance == null) return;
            VfxPool.Instance.SpawnTracer(start, end, normal, didHit);
        }
    }
}
