using _Project.Scripts.Game;
using ShipSalvage.UI;
using ShipSalvage.Utils;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShipSalvage.Player
{
    public class Gun : Item
    {
        [SerializeField] private Transform _muzzle;
        [SerializeField] private ParticleSystem _muzzleFlash;
        [SerializeField] private GunData _data;
        [SerializeField] private LayerMask _hitMask = ~0;

        private readonly NetworkVariable<int> _currentAmmo = new();
        private readonly NetworkVariable<bool> _isReloading = new();

        private double _lastFireTime;
        private double _reloadEndTime;
        private bool _inputBound;

        public GunData Data => _data;
        public Transform Muzzle => _muzzle;
        public int CurrentAmmo => _currentAmmo.Value;
        public bool IsReloading => _isReloading.Value;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
                _currentAmmo.Value = _data != null ? _data.MagazineSize : 0;
        }

        private void Update()
        {
            if (!IsServer) return;
            if (!_isReloading.Value) return;
            if (NetworkManager.ServerTime.Time < _reloadEndTime) return;

            _currentAmmo.Value = _data != null ? _data.MagazineSize : 0;
            _isReloading.Value = false;
        }

        protected override void OnEquipped()
        {
            if (!IsOwner || Holder == null) return;

            if (Holder.FireAction != null)
                Holder.FireAction.performed += HandleFireInput;

            if (Holder.ReloadAction != null)
                Holder.ReloadAction.performed += HandleReloadInput;

            _inputBound = true;
        }

        protected override void OnUnequipped()
        {
            if (!_inputBound || Holder == null) return;

            if (Holder.FireAction != null)
                Holder.FireAction.performed -= HandleFireInput;

            if (Holder.ReloadAction != null)
                Holder.ReloadAction.performed -= HandleReloadInput;

            _inputBound = false;
        }

        private void HandleFireInput(InputAction.CallbackContext context)
        {
            if (_data == null) return;
            if (Holder.IsPiloting) return;
            if (InventoryUI.Instance != null && InventoryUI.Instance.IsBagOpen) return;
            if (_isReloading.Value || _currentAmmo.Value <= 0) return;

            var camera = Holder.FirstPersonCamera;
            if (camera == null) return;

            var camTransform = camera.transform;
            FireServerRpc(camTransform.position, camTransform.forward);
        }

        private void HandleReloadInput(InputAction.CallbackContext context)
        {
            if (_data == null) return;
            if (_isReloading.Value || _currentAmmo.Value >= _data.MagazineSize) return;

            ReloadServerRpc();
        }

        [ServerRpc]
        private void FireServerRpc(Vector3 origin, Vector3 direction)
        {
            if (_data == null) return;
            if (_isReloading.Value) return;
            if (_currentAmmo.Value <= 0) return;
            if (NetworkManager.ServerTime.Time - _lastFireTime < 1.0 / _data.FireRate) return;

            _currentAmmo.Value--;
            _lastFireTime = NetworkManager.ServerTime.Time;

            ServerFire(origin, direction);
        }

        protected virtual void ServerFire(Vector3 origin, Vector3 direction)
        {
            var didHit = WeaponSystem.Hitscan(origin, direction, _data.Range, _data.Damage, _hitMask, OwnerClientId, out var end, out var normal);
            FireVisualClientRpc(end, normal, didHit);
        }

        [ServerRpc]
        private void ReloadServerRpc()
        {
            if (_data == null) return;
            if (_isReloading.Value) return;
            if (_currentAmmo.Value >= _data.MagazineSize) return;

            _isReloading.Value = true;
            _reloadEndTime = NetworkManager.ServerTime.Time + _data.ReloadTime;
        }

        [ClientRpc]
        private void FireVisualClientRpc(Vector3 end, Vector3 normal, bool didHit)
        {
            PlayMuzzleFlash();

            if (VfxPool.Instance == null) return;

            var start = _muzzle != null ? _muzzle.position : end;
            VfxPool.Instance.SpawnTracer(start, end, normal, didHit);
        }

        public void PlayMuzzleFlash()
        {
            if (_muzzleFlash != null)
                _muzzleFlash.Play();
        }
    }
}
