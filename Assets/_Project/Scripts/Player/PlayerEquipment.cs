using System;
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
        private GameObject _currentVisual;
        private Item _currentItem;

        public event Action OnEquippedChanged;

        public FixedString64Bytes EquippedItemId => _equippedItemId.Value;
        public bool HasEquipped => _equippedItemId.Value.Length > 0;
        public GameObject CurrentVisual => _currentVisual;

        private void Awake()
        {
            _inventory = GetComponent<PlayerInventory>();
        }

        public override void OnNetworkSpawn()
        {
            _equippedItemId.OnValueChanged += HandleEquippedChanged;
            RefreshVisual();

            if (IsServer && _inventory != null)
                _inventory.OnInventoryChanged += HandleInventoryChanged;
        }

        public override void OnNetworkDespawn()
        {
            _equippedItemId.OnValueChanged -= HandleEquippedChanged;

            if (IsServer && _inventory != null)
                _inventory.OnInventoryChanged -= HandleInventoryChanged;

            DestroyVisual();
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
        }

        [ServerRpc]
        private void UnequipRequestServerRpc()
        {
            _equippedItemId.Value = default;
        }

        private void HandleInventoryChanged(NetworkListEvent<InventoryItem> _)
        {
            if (_equippedItemId.Value.Length == 0) return;
            if (!InventoryContains(_equippedItemId.Value))
                _equippedItemId.Value = default;
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

        private void HandleEquippedChanged(FixedString64Bytes previous, FixedString64Bytes current)
        {
            RefreshVisual();
            OnEquippedChanged?.Invoke();
        }

        private void RefreshVisual()
        {
            DestroyVisual();

            if (_handSocket == null) return;
            if (_equippedItemId.Value.Length == 0) return;
            if (ItemDatabase.Instance == null) return;
            if (!ItemDatabase.Instance.GetItemById(_equippedItemId.Value.ToString(), out var item)) return;
            if (item.EquipPrefab == null) return;

            _currentVisual = Instantiate(item.EquipPrefab, _handSocket);
            _currentVisual.transform.localPosition = Vector3.zero;
            _currentVisual.transform.localRotation = Quaternion.identity;

            _currentItem = _currentVisual.GetComponentInChildren<Item>();
            if (_currentItem != null)
                _currentItem.OnEquipped();
        }

        private void DestroyVisual()
        {
            if (_currentVisual == null) return;

            if (_currentItem != null)
            {
                _currentItem.OnUnequipped();
                _currentItem = null;
            }

            Destroy(_currentVisual);
            _currentVisual = null;
        }
    }
}
