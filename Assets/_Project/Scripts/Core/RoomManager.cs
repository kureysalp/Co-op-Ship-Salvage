using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace ShipSalvage.Core
{
    public class RoomManager : MonoBehaviour
    {
        public static RoomManager Instance { get; private set; }

        private const ushort DefaultPort = 7777;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        
        public string CreateRoom()
        {
            GetTransport().SetConnectionData("0.0.0.0", DefaultPort);
            NetworkManager.Singleton.StartHost();
            return GetLocalIP();
        }

        public bool JoinRoom(string hostIP)
        {
            if (string.IsNullOrWhiteSpace(hostIP))
                hostIP = "127.0.0.1";

            GetTransport().SetConnectionData(hostIP.Trim(), DefaultPort);
            return NetworkManager.Singleton.StartClient();
        }

        private UnityTransport GetTransport() =>
            NetworkManager.Singleton.GetComponent<UnityTransport>();

        private string GetLocalIP()
        {
            try
            {
                using var socket = new System.Net.Sockets.Socket(
                    System.Net.Sockets.AddressFamily.InterNetwork,
                    System.Net.Sockets.SocketType.Dgram, 0);
                socket.Connect("8.8.8.8", 65530);
                return ((System.Net.IPEndPoint)socket.LocalEndPoint).Address.ToString();
            }
            catch { return "127.0.0.1"; }
        }
    }
}
