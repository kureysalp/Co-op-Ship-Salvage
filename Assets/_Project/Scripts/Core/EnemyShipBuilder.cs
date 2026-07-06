using ShipSalvage.AI;
using ShipSalvage.Boats;
using Unity.Netcode;
using UnityEngine;

namespace ShipSalvage.Core
{
    public class EnemyShipBuilder : MonoBehaviour
    {
        [SerializeField] private NetworkObject _boatPrefab;
        [SerializeField] private NetworkObject _crewPrefab;
        [SerializeField] private ShipSpawnPoint[] _shipSpawnPoints;
        [SerializeField] private int _fallbackCrewCount = 3;

        private void Start()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return;

            if (_shipSpawnPoints.Length > 0)
            {
                foreach (var point in _shipSpawnPoints)
                    if (point != null)
                        BuildShip(point.GetRandomPosition(), point.transform.rotation);
            }
            else
                BuildShip(transform.position, transform.rotation);
        }

        public AiBoatPilot BuildShip(Vector3 position, Quaternion rotation)
        {
            if (_boatPrefab == null) return null;

            var boat = Instantiate(_boatPrefab, position, rotation);
            boat.Spawn();

            var pilot = boat.GetComponent<AiBoatPilot>();
            if (pilot == null) return null;

            pilot.Engage();
            SpawnCrew(boat, pilot);
            return pilot;
        }

        private void SpawnCrew(NetworkObject boat, AiBoatPilot pilot)
        {
            if (_crewPrefab == null) return;

            var boatController = boat.GetComponent<BoatController>();

            var posts = pilot.CrewSpawnPoints;
            bool hasPosts = posts != null && posts.Count > 0;
            int count = hasPosts ? posts.Count : _fallbackCrewCount;

            for (int i = 0; i < count; i++)
            {
                Vector3 pos;
                Quaternion rot;

                if (hasPosts && posts[i] != null)
                {
                    pos = posts[i].position;
                    rot = posts[i].rotation;
                }
                else
                {
                    pos = boat.transform.position;
                    rot = boat.transform.rotation;
                }

                var crew = Instantiate(_crewPrefab, pos, rot);
                crew.Spawn();

                var enemyCrew = crew.GetComponent<EnemyCrew>();
                if (enemyCrew != null)
                {
                    pilot.RegisterCrew(enemyCrew);
                    if (boatController != null)
                        boatController.AddRider(enemyCrew);
                }
            }
        }
    }
}
