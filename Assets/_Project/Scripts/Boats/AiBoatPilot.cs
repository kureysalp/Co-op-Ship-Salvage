using System.Collections.Generic;
using ShipSalvage.AI;
using Unity.Netcode;
using UnityEngine;

namespace ShipSalvage.Boats
{
    public class AiBoatPilot : BoatPilot
    {
        [Header("Perception")]
        [SerializeField] private LayerMask _islandMask;
        [SerializeField] private float _sightRange = 90f;
        [SerializeField] private float _perceptionInterval = 0.5f;

        [Header("Roaming")]
        [SerializeField] private float _roamRadius = 60f;
        [SerializeField] private float _arriveDistance = 10f;
        [SerializeField] private float _avoidDistance = 18f;
        [SerializeField] private float _steerAngleRange = 45f;

        [Header("Boarding")]
        [SerializeField] private float _boardRange = 14f;
        [SerializeField] private Transform _boardingPoint;
        [SerializeField] private NetworkObject _plankPrefab;

        [Header("Crew")]
        [SerializeField] private Transform[] _crewSpawnPoints;

        private BTNode _tree;
        private readonly List<EnemyCrew> _crew = new();

        private bool _engaged;
        private float _steer;
        private float _throttle;

        private BoatController _target;
        private float _nextPerceptionTime;

        private Vector3 _roamPoint;
        private bool _hasRoamPoint;

        private bool _boarding;
        private NetworkObject _plankInstance;

        public bool Engaged => _engaged;
        public void Engage() => _engaged = true;

        public IReadOnlyList<Transform> CrewSpawnPoints => _crewSpawnPoints;

        public override bool IsInCommand => _engaged && (Boat == null || !Boat.HasHumanPilot);

        public void RegisterCrew(EnemyCrew crew)
        {
            if (crew != null && !_crew.Contains(crew))
                _crew.Add(crew);
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            BuildTree();
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && _plankInstance != null && _plankInstance.IsSpawned)
                _plankInstance.Despawn();
        }

        private void BuildTree()
        {
            _tree = new Selector(
                new Sequence(
                    new ConditionNode(() => _target != null && DistanceToTarget() <= _boardRange),
                    new ActionNode(TickOnBoarding)),
                new Sequence(
                    new ConditionNode(() => _target != null),
                    new ActionNode(TickChase)),
                new ActionNode(TickRoam));
        }

        protected override bool Drive(out float steer, out float throttle)
        {
            _steer = 0f;
            _throttle = 0f;

            UpdatePerception();
            _tree?.Tick();

            steer = _steer;
            throttle = _throttle;
            return true;
        }

        private void Output(float steer, float throttle)
        {
            _steer = steer;
            _throttle = throttle;
        }

        private void UpdatePerception()
        {
            if (Time.time < _nextPerceptionTime) return;
            _nextPerceptionTime = Time.time + _perceptionInterval;
            _target = FindTargetBoat();
        }

        private BoatController FindTargetBoat()
        {
            var boats = FindObjectsByType<BoatController>(FindObjectsSortMode.None);
            BoatController best = null;
            float bestSqr = _sightRange * _sightRange;

            foreach (var b in boats)
            {
                if (b == Boat) continue;

                var ai = b.GetComponent<AiBoatPilot>();
                if (ai != null && ai.Engaged) continue;

                float sqr = (b.transform.position - transform.position).sqrMagnitude;
                if (sqr > bestSqr) continue;
                if (!HasLineOfSight(b.transform.position)) continue;

                best = b;
                bestSqr = sqr;
            }

            return best;
        }

        private bool HasLineOfSight(Vector3 point)
        {
            Vector3 origin = transform.position + Vector3.up * 1.5f;
            Vector3 dir = point - origin;
            return !Physics.Raycast(origin, dir.normalized, dir.magnitude, _islandMask);
        }

        private float DistanceToTarget() =>
            _target != null ? Vector3.Distance(transform.position, _target.transform.position) : float.MaxValue;

        private BTStatus TickRoam()
        {
            if (_boarding) EndBoarding();

            if (!_hasRoamPoint || Vector3.Distance(transform.position, _roamPoint) <= _arriveDistance)
            {
                if (!TryPickRoamPoint(out _roamPoint))
                {
                    Output(0f, 0f);
                    return BTStatus.Running;
                }

                _hasRoamPoint = true;
            }

            SteerToward(_roamPoint);
            return BTStatus.Running;
        }

        private BTStatus TickChase()
        {
            if (_boarding) EndBoarding();
            _hasRoamPoint = false;
            SteerToward(_target.transform.position);
            return BTStatus.Running;
        }

        private BTStatus TickOnBoarding()
        {
            _hasRoamPoint = false;
            Output(SteerValue(_target.transform.position), 0f);

            if (!_boarding)
                BeginBoarding();

            return BTStatus.Running;
        }

        private void BeginBoarding()
        {
            _boarding = true;
            SpawnPlank();

            foreach (var crew in _crew)
                if (crew != null)
                    crew.BeginBoarding(_target);
        }

        private void EndBoarding()
        {
            _boarding = false;

            if (_plankInstance != null && _plankInstance.IsSpawned)
                _plankInstance.Despawn();
            _plankInstance = null;

            foreach (var crew in _crew)
                if (crew != null)
                    crew.StopBoarding();
        }

        private void SpawnPlank()
        {
            if (_plankPrefab == null || _target == null) return;

            Vector3 from = _boardingPoint != null ? _boardingPoint.position : transform.position;
            Vector3 to = _target.transform.position;
            Vector3 mid = (from + to) * 0.5f;

            Vector3 dir = to - from;
            dir.y = 0f;
            Quaternion rot = dir.sqrMagnitude > 0.001f ? Quaternion.LookRotation(dir) : Quaternion.identity;

            _plankInstance = Instantiate(_plankPrefab, mid, rot);
            _plankInstance.Spawn();
        }

        private void SteerToward(Vector3 point)
        {
            float steer = SteerValue(point);

            if (Physics.Raycast(transform.position + Vector3.up * 1.5f, transform.forward, _avoidDistance, _islandMask))
                steer = steer >= 0f ? 1f : -1f;

            Output(steer, 1f);
        }

        private float SteerValue(Vector3 point)
        {
            Vector3 to = point - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.001f) return 0f;

            float angle = Vector3.SignedAngle(transform.forward, to, Vector3.up);
            return Mathf.Clamp(angle / _steerAngleRange, -1f, 1f);
        }

        private bool TryPickRoamPoint(out Vector3 point)
        {
            for (int i = 0; i < 12; i++)
            {
                Vector2 c = Random.insideUnitCircle * _roamRadius;
                Vector3 candidate = transform.position + new Vector3(c.x, 0f, c.y);
                if (IsOnSea(candidate))
                {
                    point = candidate;
                    return true;
                }
            }

            point = transform.position;
            return false;
        }

        private bool IsOnSea(Vector3 point)
        {
            Vector3 origin = point + Vector3.up * 50f;
            return !Physics.Raycast(origin, Vector3.down, 100f, _islandMask);
        }
    }
}
