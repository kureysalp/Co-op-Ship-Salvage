using System;
using System.Collections.Generic;
using _Project.Scripts.Game;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace ShipSalvage.Player
{
    public class PlayerInventory : NetworkBehaviour
    {
        private readonly NetworkList<InventoryItem> _inventoryItems = new();

        public event NetworkList<InventoryItem>.OnListChangedDelegate OnInventoryChanged;

        public int Count => _inventoryItems.Count;
        public InventoryItem this[int index] => _inventoryItems[index];

        public override void OnNetworkSpawn()
        {
            _inventoryItems.OnListChanged += HandleListChanged;
        }

        public override void OnNetworkDespawn()
        {
            _inventoryItems.OnListChanged -= HandleListChanged;
        }

        private void HandleListChanged(NetworkListEvent<InventoryItem> changeEvent)
        {
            OnInventoryChanged?.Invoke(changeEvent);
        }

        public bool TryAddItem(InventoryItem itemToAdd)
        {
            if (!IsServer) return false;

            if (TryAddStack(itemToAdd)) return true;

            _inventoryItems.Add(itemToAdd);
            return true;
        }

        private bool TryAddStack(InventoryItem itemToAdd)
        {
            if (!ItemDatabase.Instance.GetItemById(itemToAdd.ItemId.ToString(), out var item)) return false;
            if (!item.IsStackable) return false;

            for (var i = 0; i < _inventoryItems.Count; i++)
            {
                var inventoryItem = _inventoryItems[i];
                if (!itemToAdd.Equals(inventoryItem))
                    continue;

                inventoryItem.Quantity += itemToAdd.Quantity;
                _inventoryItems[i] = inventoryItem;
                return true;
            }

            return false;
        }
    }

    public struct InventoryItem : INetworkSerializable, IEquatable<InventoryItem>
    {
        public FixedString64Bytes ItemId;
        public int Quantity;

        public InventoryItem(FixedString64Bytes itemId, int quantity)
        {
            ItemId = itemId;
            Quantity = quantity;
        }

        public bool Equals(InventoryItem other)
        {
            return ItemId.Equals(other.ItemId);
        }

        public override bool Equals(object obj)
        {
            return obj is InventoryItem other && Equals(other);
        }

        public override int GetHashCode()
        {
            return ItemId.GetHashCode();
        }
        
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ItemId);
            serializer.SerializeValue(ref Quantity);
        }
    }
}