using _Project.Scripts.Game;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace ShipSalvage.Player
{
    public class PlayerEquipment : NetworkBehaviour
    {
        [SerializeField] private Transform _handSocket;

        private readonly NetworkVariable<FixedString64Bytes> _equippedItemId = new();

        private PlayerInventory _inventory;
        private NetworkObject _spawnedWeapon;

        public FixedString64Bytes EquippedItemId => _equippedItemId.Value;
        public bool HasEquipped => _equippedItemId.Value.Length > 0;
        public Transform HandSocket => _handSocket;

        private void Awake()
        {
            _inventory = GetComponent<PlayerInventory>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer && _inventory != null)
                _inventory.OnInventoryChanged += HandleInventoryChanged;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && _inventory != null)
                _inventory.OnInventoryChanged -= HandleInventoryChanged;

            if (IsServer)
                DespawnWeapon();
        }

        public void RequestEquip(FixedString64Bytes itemId)
        {
            if (!IsOwner) return;
            EquipRequestServerRpc(itemId);
        }

        public void RequestUnequip()
        {
            if (!IsOwner) return;
            UnequipRequestServerRpc();
        }

        [ServerRpc]
        private void EquipRequestServerRpc(FixedString64Bytes itemId)
        {
            if (!InventoryContains(itemId)) return;
            if (!ItemDatabase.Instance.GetItemById(itemId.ToString(), out var item)) return;
            if (!item.IsEquipable) return;

            _equippedItemId.Value = itemId;
            SpawnWeapon(item);
        }

        [ServerRpc]
        private void UnequipRequestServerRpc()
        {
            _equippedItemId.Value = default;
            DespawnWeapon();
        }

        private void HandleInventoryChanged(NetworkListEvent<InventoryItem> _)
        {
            if (_equippedItemId.Value.Length == 0) return;
            if (InventoryContains(_equippedItemId.Value)) return;

            _equippedItemId.Value = default;
            DespawnWeapon();
        }

        private void SpawnWeapon(ItemObject item)
        {
            DespawnWeapon();

            if (item.EquipPrefab == null) return;

            var instance = Instantiate(item.EquipPrefab);
            if (!instance.TryGetComponent(out NetworkObject netObj))
            {
                Destroy(instance);
                return;
            }

            netObj.SpawnWithOwnership(OwnerClientId);
            _spawnedWeapon = netObj;
        }

        private void DespawnWeapon()
        {
            if (_spawnedWeapon == null) return;

            if (_spawnedWeapon.IsSpawned)
                _spawnedWeapon.Despawn();

            _spawnedWeapon = null;
        }

        private bool InventoryContains(FixedString64Bytes itemId)
        {
            if (_inventory == null) return false;

            for (var i = 0; i < _inventory.Count; i++)
            {
                if (_inventory[i].ItemId.Equals(itemId))
                    return true;
            }

            return false;
        }
    }
}
