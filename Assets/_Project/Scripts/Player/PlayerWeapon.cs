using ShipSalvage.UI;
using ShipSalvage.Utils;
using Unity.Netcode;
using UnityEngine;

namespace ShipSalvage.Player
{
    public class PlayerWeapon : NetworkBehaviour
    {
        [SerializeField] private LayerMask _hitMask = ~0;

        private readonly NetworkVariable<double> _lastFireTime = new();
        private readonly NetworkVariable<int> _currentAmmo = new();
        private readonly NetworkVariable<bool> _isReloading = new();

        private PlayerController _player;
        private PlayerEquipment _equipment;
        private Gun _gun;
        private double _reloadEndTime;

        public int CurrentAmmo => _currentAmmo.Value;
        public bool IsReloading => _isReloading.Value;

        private void Awake()
        {
            _player = GetComponent<PlayerController>();
            _equipment = GetComponent<PlayerEquipment>();
        }

        public override void OnNetworkSpawn()
        {
            _lastFireTime.OnValueChanged += HandleFired;

            if (_equipment != null)
                _equipment.OnEquippedChanged += HandleEquippedChanged;

            ResolveGun();

            if (IsServer)
                ApplyAmmoForEquipped();
        }

        public override void OnNetworkDespawn()
        {
            _lastFireTime.OnValueChanged -= HandleFired;

            if (_equipment != null)
                _equipment.OnEquippedChanged -= HandleEquippedChanged;
        }

        private void HandleEquippedChanged()
        {
            ResolveGun();

            if (IsServer)
                ApplyAmmoForEquipped();
        }

        private void ResolveGun()
        {
            var visual = _equipment != null ? _equipment.CurrentVisual : null;
            _gun = visual != null ? visual.GetComponentInChildren<Gun>() : null;
        }

        private void ApplyAmmoForEquipped()
        {
            _isReloading.Value = false;
            _currentAmmo.Value = _gun != null && _gun.Data != null ? _gun.Data.MagazineSize : 0;
        }

        private void Update()
        {
            if (IsServer)
                UpdateReload();

            if (!IsOwner) return;
            if (_gun == null || _gun.Data == null) return;
            if (InventoryUI.Instance != null && InventoryUI.Instance.IsBagOpen) return;
            if (_player.IsPiloting) return;

            if (_player.ReloadAction != null && _player.ReloadAction.WasPressedThisFrame())
            {
                if (!_isReloading.Value && _currentAmmo.Value < _gun.Data.MagazineSize)
                    ReloadServerRpc();
                return;
            }

            if (_player.FireAction != null && _player.FireAction.WasPressedThisFrame())
            {
                if (_isReloading.Value || _currentAmmo.Value <= 0) return;

                var camera = _player.FirstPersonCamera;
                if (camera == null) return;

                var camTransform = camera.transform;
                FireServerRpc(camTransform.position, camTransform.forward);
            }
        }

        private void UpdateReload()
        {
            if (!_isReloading.Value) return;
            if (NetworkManager.ServerTime.Time < _reloadEndTime) return;

            _currentAmmo.Value = _gun != null && _gun.Data != null ? _gun.Data.MagazineSize : 0;
            _isReloading.Value = false;
        }

        [ServerRpc]
        private void FireServerRpc(Vector3 origin, Vector3 direction)
        {
            if (_gun == null || _gun.Data == null) return;
            if (_isReloading.Value) return;
            if (_currentAmmo.Value <= 0) return;

            var data = _gun.Data;
            if (NetworkManager.ServerTime.Time - _lastFireTime.Value < 1.0 / data.FireRate) return;

            if (Physics.Raycast(origin, direction, out var hit, data.Range, _hitMask, QueryTriggerInteraction.Ignore))
                hit.collider.GetComponentInParent<IDamageable>()?.TakeDamage(data.Damage, OwnerClientId);

            _currentAmmo.Value--;
            _lastFireTime.Value = NetworkManager.ServerTime.Time;
        }

        [ServerRpc]
        private void ReloadServerRpc()
        {
            if (_gun == null || _gun.Data == null) return;
            if (_isReloading.Value) return;
            if (_currentAmmo.Value >= _gun.Data.MagazineSize) return;

            _isReloading.Value = true;
            _reloadEndTime = NetworkManager.ServerTime.Time + _gun.Data.ReloadTime;
        }

        private void HandleFired(double previous, double current)
        {
            if (_gun == null) return;
            if (current <= 0.0) return;
            if (NetworkManager.ServerTime.Time - current >= 0.25) return;

            _gun.PlayMuzzleFlash();
        }
    }
}
