using ShipSalvage.Core;
using ShipSalvage.Player;
using Unity.Netcode;
using UnityEngine;

namespace _Project.Scripts.Game
{
    public class Item : NetworkBehaviour
    {
        [SerializeField] private ItemObject _itemObject;

        public ItemObject ItemObject => _itemObject;

        protected PlayerController Holder { get; private set; }

        private bool _waitingForHolder;

        public override void OnNetworkSpawn()
        {
            if (TryResolveHolder()) return;

            if (PlayerManager.Instance != null)
            {
                PlayerManager.Instance.OnPlayerRegistered += HandlePlayerRegistered;
                _waitingForHolder = true;
            }
        }

        public override void OnNetworkDespawn()
        {
            StopWaitingForHolder();
            OnUnequipped();
            Holder = null;
        }

        private void HandlePlayerRegistered(ulong clientId, PlayerController player)
        {
            if (clientId != OwnerClientId) return;
            TryResolveHolder();
        }

        private bool TryResolveHolder()
        {
            if (Holder != null) return true;

            var player = PlayerManager.Instance != null ? PlayerManager.Instance.GetPlayer(OwnerClientId) : null;
            if (player == null) return false;

            StopWaitingForHolder();
            Holder = player;

            AttachToHolder();
            OnEquipped();
            return true;
        }

        private void StopWaitingForHolder()
        {
            if (!_waitingForHolder) return;

            if (PlayerManager.Instance != null)
                PlayerManager.Instance.OnPlayerRegistered -= HandlePlayerRegistered;

            _waitingForHolder = false;
        }

        private void AttachToHolder()
        {
            var socket = Holder.Equipment != null ? Holder.Equipment.HandSocket : null;
            if (socket == null) return;

            transform.SetParent(socket, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        protected virtual void OnEquipped() { }
        protected virtual void OnUnequipped() { }
    }
}
