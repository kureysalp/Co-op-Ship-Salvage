using ShipSalvage.Core;
using ShipSalvage.Player;
using ShipSalvage.Utils;
using Unity.Netcode;
using UnityEngine;

namespace _Project.Scripts.Game
{
    public class PickableItem : NetworkBehaviour, IInteractable
    {
        [SerializeField] private ItemObject _itemObject;
        [SerializeField] private int _quantity = 1;

        [SerializeField] private string _interactionMessage = "Pick up";

        [SerializeField] private GameObject _highlightTarget;

        public bool CanInteract(PlayerController player) => _itemObject != null;

        public void Highlight(bool active)
        {
            if (_highlightTarget != null)
                _highlightTarget.SetActive(active);
        }

        public void Interact(PlayerController player)
        {
            PickupItemServerRpc();
        }

        public string GetInteractionLabel(PlayerController player) => _interactionMessage;

        [ServerRpc(RequireOwnership = false)]
        private void PickupItemServerRpc(ServerRpcParams rpcParams = default)
        {
            if (_itemObject == null) return;

            var clientId = rpcParams.Receive.SenderClientId;
            var player = PlayerManager.Instance.GetPlayer(clientId);
            if (player == null || player.Inventory == null) return;

            var inventoryItem = new InventoryItem(_itemObject.UId, Mathf.Max(1, _quantity));

            if (player.Inventory.TryAddItem(inventoryItem))
                NetworkObject.Despawn();
        }
    }
}
