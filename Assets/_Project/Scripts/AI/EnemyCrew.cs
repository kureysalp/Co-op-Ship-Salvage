using ShipSalvage.Boats;
using ShipSalvage.Core;
using ShipSalvage.Player;
using ShipSalvage.Utils;
using Unity.Netcode;
using UnityEngine;

namespace ShipSalvage.AI
{
    [RequireComponent(typeof(Rigidbody))]
    public class EnemyCrew : NetworkBehaviour, IBoatRider
    {
        [Header("Movement")]
        [SerializeField] private float _moveSpeed = 3f;
        [SerializeField] private float _stopDistance = 1.8f;
        [SerializeField] private bool _snapToDeck = true;
        [SerializeField] private LayerMask _deckMask = ~0;
        [SerializeField] private float _deckOffset = 1f;

        [Header("Combat")]
        [SerializeField] private AiGun _gun;
        [SerializeField] private float _fireRange = 28f;
        [SerializeField] private float _aimHeight = 1.2f;
        [SerializeField] private LayerMask _sightMask;

        [Header("Boarding")]
        [SerializeField] private float _boardArriveDistance = 3f;

        private Rigidbody _rb;
        private Collider[] _colliders;
        private Health _health;
        private BTNode _tree;

        private bool _active;
        private BoatController _boardTarget;
        private bool _crossing;
        private PlayerController _player;
        private BoatController _ridingBoat;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _colliders = GetComponentsInChildren<Collider>();
        }

        public void BoardBoat(BoatController boat)
        {
            if (_ridingBoat != null && _ridingBoat != boat)
                _ridingBoat.RemoveRider(this);

            _ridingBoat = boat;
            SetBoatCollision(boat, true);
        }

        public void LeaveBoat(BoatController boat)
        {
            SetBoatCollision(boat, false);
            if (_ridingBoat == boat)
                _ridingBoat = null;
        }

        private void SwitchBoat(BoatController boat)
        {
            if (!IsServer || boat == null || boat == _ridingBoat) return;
            boat.AddRider(this);
        }

        public void RideBoat(Vector3 deltaPosition, float deltaYaw, Vector3 pivot)
        {
            if (!IsServer || _rb == null) return;

            var offset = _rb.position - pivot;
            var rotationMove = (Quaternion.Euler(0f, deltaYaw, 0f) * offset) - offset;
            _rb.MovePosition(_rb.position + deltaPosition + rotationMove);

            if (Mathf.Abs(deltaYaw) > 0.001f)
                _rb.MoveRotation(Quaternion.Euler(0f, deltaYaw, 0f) * _rb.rotation);
        }

        private void SetBoatCollision(BoatController boat, bool ignore)
        {
            if (boat == null || _colliders == null) return;

            var boatColliders = boat.GetComponentsInChildren<Collider>();
            foreach (var mine in _colliders)
            {
                if (mine == null) continue;

                foreach (var boatCollider in boatColliders)
                {
                    if (boatCollider == null || boatCollider.isTrigger) continue;
                    Physics.IgnoreCollision(mine, boatCollider, ignore);
                }
            }
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;

            if (_gun == null)
                _gun = GetComponent<AiGun>();

            _health = GetComponent<Health>();
            if (_health != null)
                _health.OnDeath += HandleDeath;

            BuildTree();
        }

        public override void OnNetworkDespawn()
        {
            if (!IsServer) return;

            if (_health != null)
                _health.OnDeath -= HandleDeath;

            if (_ridingBoat != null)
                _ridingBoat.RemoveRider(this);
        }

        private void HandleDeath(ulong instigatorClientId)
        {
            if (IsServer && NetworkObject.IsSpawned)
                NetworkObject.Despawn();
        }

        public void BeginBoarding(BoatController playerBoat)
        {
            if (!IsServer) return;

            _active = true;
            _boardTarget = playerBoat;
            _crossing = playerBoat != null;
        }

        public void StopBoarding()
        {
            if (!IsServer) return;

            _active = false;
            _boardTarget = null;
            _crossing = false;
            _player = null;
        }

        private void BuildTree()
        {
            _tree = new Selector(
                new Sequence(
                    new ConditionNode(() => !_active),
                    new ActionNode(() => BTStatus.Success)),
                new Sequence(
                    new ConditionNode(() => _crossing),
                    new ActionNode(TickCross)),
                new Sequence(
                    new ConditionNode(HasTargetInFireRange),
                    new ActionNode(TickFight)),
                new Sequence(
                    new ConditionNode(() => _player != null),
                    new ActionNode(TickChase)),
                new ActionNode(() => BTStatus.Success));
        }

        private void FixedUpdate()
        {
            if (!IsServer || _tree == null) return;

            if (_active)
                AcquirePlayer();

            _tree.Tick();
        }

        private void AcquirePlayer()
        {
            float bestSqr = float.MaxValue;
            PlayerController best = null;

            foreach (var kvp in PlayerManager.Instance.Players)
            {
                var p = kvp.Value;
                if (p == null) continue;

                float sqr = (p.transform.position - _rb.position).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = p;
                }
            }

            _player = best;
        }

        private BTStatus TickCross()
        {
            if (_boardTarget == null)
            {
                _crossing = false;
                return BTStatus.Failure;
            }

            Vector3 entry = _boardTarget.transform.position;
            if (Vector3.Distance(_rb.position, entry) <= _boardArriveDistance)
            {
                _crossing = false;
                SwitchBoat(_boardTarget);
                return BTStatus.Success;
            }

            SeekTo(entry);
            return BTStatus.Running;
        }

        private bool HasTargetInFireRange()
        {
            if (_player == null) return false;

            Vector3 aim = _player.transform.position + Vector3.up * _aimHeight;
            if ((aim - _rb.position).sqrMagnitude > _fireRange * _fireRange) return false;

            return HasLineOfSight(aim);
        }

        private BTStatus TickFight()
        {
            Vector3 aim = _player.transform.position + Vector3.up * _aimHeight;
            FaceDirection(aim - _rb.position);
            _gun?.TryFire(aim);
            return BTStatus.Running;
        }

        private BTStatus TickChase()
        {
            SeekTo(_player.transform.position);
            return BTStatus.Running;
        }

        private void SeekTo(Vector3 point)
        {
            Vector3 to = point - _rb.position;
            to.y = 0f;
            if (to.sqrMagnitude <= _stopDistance * _stopDistance) return;

            Vector3 next = _rb.position + to.normalized * (_moveSpeed * Time.fixedDeltaTime);
            if (_snapToDeck)
                next.y = SampleDeckHeight(next);

            _rb.MovePosition(next);
            FaceDirection(to);
        }

        private float SampleDeckHeight(Vector3 position)
        {
            Vector3 origin = position + Vector3.up * 3f;
            if (Physics.Raycast(origin, Vector3.down, out var hit, 6f, _deckMask, QueryTriggerInteraction.Ignore))
                return hit.point.y + _deckOffset;

            return _rb.position.y;
        }

        private void FaceDirection(Vector3 dir)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) return;
            _rb.MoveRotation(Quaternion.LookRotation(dir));
        }

        private bool HasLineOfSight(Vector3 point)
        {
            Vector3 origin = _rb.position + Vector3.up * _aimHeight;
            Vector3 dir = point - origin;
            return !Physics.Raycast(origin, dir.normalized, dir.magnitude, _sightMask, QueryTriggerInteraction.Ignore);
        }
    }
}
