using ShipSalvage.Core;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShipSalvage.UI
{
    public class LobbyUI : MonoBehaviour
    {
        [SerializeField] private GameObject _lobbyPanel;
        [SerializeField] private TMP_InputField _roomNameInput; // repurposed: host IP for Join
        [SerializeField] private Button _createButton;
        [SerializeField] private Button _joinButton;
        [SerializeField] private TextMeshProUGUI _statusText;

        private void Start()
        {
            _createButton.onClick.AddListener(OnCreateClicked);
            _joinButton.onClick.AddListener(OnJoinClicked);
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }

        private void OnDestroy()
        {
            if (NetworkManager.Singleton == null) return;
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            _createButton.onClick.RemoveListener(OnCreateClicked);
            _joinButton.onClick.RemoveListener(OnJoinClicked);
        }

        private void OnCreateClicked()
        {
            SetButtonsInteractable(false);
            var localIP = RoomManager.Instance.CreateRoom();
            SetStatus($"Hosting on {localIP}:7777");
        }

        private void OnJoinClicked()
        {
            const string ip = "127.0.0.1";
            SetButtonsInteractable(false);
            SetStatus($"Connecting to {ip}…");
            RoomManager.Instance.JoinRoom(ip);
        }

        private void OnClientConnected(ulong clientId)
        {
            if (clientId != NetworkManager.Singleton.LocalClientId) return;
            HideOverlay();
            if (NetworkManager.Singleton.IsHost)
                NetworkManager.Singleton.SceneManager.LoadScene("IslandMain", LoadSceneMode.Single);
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (clientId != NetworkManager.Singleton.LocalClientId) return;
            SetButtonsInteractable(true);
            SetStatus("Connection failed. Check the IP and try again.");
        }

        private void HideOverlay()
        {
            _lobbyPanel.SetActive(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void SetStatus(string message) => _statusText.text = message;

        private void SetButtonsInteractable(bool enabled)
        {
            _createButton.interactable = enabled;
            _joinButton.interactable = enabled;
        }
    }
}
