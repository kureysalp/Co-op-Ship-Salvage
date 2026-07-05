using System;
using System.Collections.Generic;
using ShipSalvage.Player;
using UnityEngine;

namespace ShipSalvage.Core
{

    public class PlayerManager : MonoBehaviour
    {
        private static PlayerManager _instance;

        public static PlayerManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new GameObject(nameof(PlayerManager)).AddComponent<PlayerManager>();
                    DontDestroyOnLoad(_instance.gameObject);
                }

                return _instance;
            }
        }

        private readonly Dictionary<ulong, PlayerController> _players = new();

        public event Action<ulong, PlayerController> OnPlayerRegistered;

        public event Action<ulong, PlayerController> OnPlayerUnregistered;

        public IReadOnlyDictionary<ulong, PlayerController> Players => _players;
        public int Count => _players.Count;
        public PlayerController LocalPlayer { get; private set; }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void Register(ulong clientId, PlayerController player)
        {
            if (player == null) return;

            _players[clientId] = player;

            if (player.IsOwner)
                LocalPlayer = player;

            OnPlayerRegistered?.Invoke(clientId, player);
        }

        public void Unregister(ulong clientId)
        {
            if (!_players.Remove(clientId, out var player)) return;

            if (LocalPlayer == player)
                LocalPlayer = null;

            OnPlayerUnregistered?.Invoke(clientId, player);
        }

        public bool TryGetPlayer(ulong clientId, out PlayerController player) =>
            _players.TryGetValue(clientId, out player);

        public PlayerController GetPlayer(ulong clientId) =>
            _players.TryGetValue(clientId, out var player) ? player : null;
    }
}
