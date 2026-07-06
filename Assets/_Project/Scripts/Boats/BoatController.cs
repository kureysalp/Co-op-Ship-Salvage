using System.Collections;
using System.Collections.Generic;
using ShipSalvage.Player;
using ShipSalvage.Utils;
using Unity.Netcode;
using UnityEngine;

namespace ShipSalvage.Boats
{
    [RequireComponent(typeof(Rigidbody), typeof(NetworkObject))]
    public class BoatController : NetworkBehaviour, IInteractable
    {
        [Header("Pilot")]
        [SerializeField] private Transform _pilotPoint;
        [SerializeField] private Transform _wheelMesh;

        [Header("Steering")]
        [SerializeField] private float _maxWheelAngle = 45f;
        [SerializeField] private float _steeringRate = 60f;
        [SerializeField] private float _wheelReturnSpeed = 90f;
        [SerializeField] private float _maxTurnSpeed = 45f;

        [Header("Movement")]
        [SerializeField] private float _moveSpeed = 5f;
        [SerializeField] private float _acceleration = 4f;
        [SerializeField] private float _drag = 2f;

        private Rigidbody _rb;
        private Vector3 _latestCollisionNormal;
        private Vector3 _lastPos;
        private float _lastYaw;
        private float _steerInput;
        private float _throttleInput;
        private PlayerBoatPilot _playerPilot;
        private Coroutine _wheelReturnCoroutine;

        private readonly List<Rigidbody> _rbPassengers = new();
        private readonly List<IBoatRider> _riders = new();
        private Color _wheelOriginalColor;
        private Renderer _wheelRenderer;

        private readonly NetworkVariable<float> _wheelAngle = new();

        private readonly NetworkVariable<ulong> _pilotId = new(ulong.MaxValue);

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _playerPilot = GetComponent<PlayerBoatPilot>();
            if (_wheelMesh != null)
            {
                _wheelRenderer = _wheelMesh.GetComponent<Renderer>();
                if (_wheelRenderer != null)
                    _wheelOriginalColor = _wheelRenderer.material.color;
            }
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                _lastPos = transform.position;
                _lastYaw = transform.eulerAngles.y;
            }
        }

        private void FixedUpdate()
        {
            if (!IsServer) return;
            
            var forwardSpeed = Vector3.Dot(_rb.linearVelocity, transform.forward);
            var speedFactor = Mathf.Clamp(forwardSpeed / _moveSpeed, -1f, 1f);
            var steeringFactor = _wheelAngle.Value / _maxWheelAngle;
            _rb.angularVelocity = new Vector3(
                0f,
                steeringFactor * _maxTurnSpeed * Mathf.Deg2Rad * speedFactor,
                0f);
            
            var horiz = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
            
            if (Mathf.Abs(_throttleInput) > 0.001f)
                horiz += transform.forward * (_acceleration * _throttleInput * Time.fixedDeltaTime);
            
            horiz = Vector3.MoveTowards(horiz, Vector3.zero, _drag * Time.fixedDeltaTime);
            horiz = Vector3.ClampMagnitude(horiz, _moveSpeed);

            if (_latestCollisionNormal != Vector3.zero)
                horiz = Vector3.ProjectOnPlane(horiz, _latestCollisionNormal);

            _rb.linearVelocity = new Vector3(horiz.x, _rb.linearVelocity.y, horiz.z);
            _latestCollisionNormal = Vector3.zero;

            if (_wheelMesh != null)
                _wheelMesh.localRotation = Quaternion.Euler(0f, 0f, -_wheelAngle.Value);

            if (Mathf.Abs(_steerInput) > 0.001f)
            {
                _wheelAngle.Value = Mathf.Clamp(
                    _wheelAngle.Value + _steerInput * _steeringRate * Time.fixedDeltaTime,
                    -_maxWheelAngle, _maxWheelAngle);
            }

            var deltaPos = transform.position - _lastPos;
            var deltaYaw = Mathf.DeltaAngle(_lastYaw, transform.eulerAngles.y);
            _lastPos = transform.position;
            _lastYaw = transform.eulerAngles.y;

            if (deltaPos.sqrMagnitude > 0.0001f || Mathf.Abs(deltaYaw) > 0.01f)
            {
                foreach (var rb in _rbPassengers)
                {
                    if (rb != null)
                    {
                        var offset = rb.position - transform.position;
                        var rotationMove = (Quaternion.Euler(0f, deltaYaw, 0f) * offset) - offset;
                        rb.MovePosition(rb.position + deltaPos + rotationMove);
                    }
                }

                for (int i = _riders.Count - 1; i >= 0; i--)
                {
                    var rider = _riders[i];
                    if (rider is Component c && c == null)
                    {
                        _riders.RemoveAt(i);
                        continue;
                    }

                    rider.RideBoat(deltaPos, deltaYaw, transform.position);
                }
            }
        }

        private void Update()
        {
            if (!IsServer && !_wheelMesh)
                _wheelMesh.localRotation = Quaternion.Euler(0f, 0f, -_wheelAngle.Value);
        }

        private void OnCollisionStay(Collision collision)
        {
            if (!IsServer) return;
            _latestCollisionNormal = collision.contacts[0].normal;
        }

        private void OnTriggerEnter(Collider other)
        {
            var player = other.GetComponentInParent<PlayerController>();
            if (player != null)
            {
                if (IsServer) player.OnBoardShip(this);
                return;
            }

            if (IsServer)
            {
                if (other.GetComponentInParent<IBoatRider>() != null) return;

                var rb = other.GetComponentInParent<Rigidbody>();
                if (rb != null && !_rbPassengers.Contains(rb))
                    _rbPassengers.Add(rb);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            var player = other.GetComponentInParent<PlayerController>();
            if (player != null)
            {
                if (IsServer) player.OnLeaveShip();
                return;
            }

            if (IsServer)
            {
                if (other.GetComponentInParent<IBoatRider>() != null) return;

                var rb = other.GetComponentInParent<Rigidbody>();
                if (rb != null)
                    _rbPassengers.Remove(rb);
            }
        }

        public void AddRider(IBoatRider rider)
        {
            if (!IsServer || rider == null) return;
            if (_riders.Contains(rider)) return;

            _riders.Add(rider);
            rider.BoardBoat(this);
        }

        public void RemoveRider(IBoatRider rider)
        {
            if (rider == null) return;

            if (_riders.Remove(rider))
                rider.LeaveBoat(this);
        }
        
        public bool CanInteract(PlayerController player) =>
            _pilotId.Value == ulong.MaxValue || _pilotId.Value == player.OwnerClientId;

        public void Highlight(bool active)
        {
            if (_wheelRenderer == null) return;
            
        }

        public void Interact(PlayerController player)
        {
            if (_pilotId.Value == ulong.MaxValue)
                BeginPilotServerRpc();
            else if (_pilotId.Value == player.OwnerClientId)
                ExitPilotServerRpc();
        }

        public string GetInteractionLabel(PlayerController player) =>
            _pilotId.Value == player.OwnerClientId ? "Leave Wheel" : "Pilot Ship";
        
        
        [ServerRpc(RequireOwnership = false)]
        private void BeginPilotServerRpc(ServerRpcParams rpcParams = default)
        {
            if (_pilotId.Value != ulong.MaxValue) return;

            var clientId = rpcParams.Receive.SenderClientId;
            _pilotId.Value = clientId;
            _steerInput = 0f;
            _throttleInput = 0f;
            _playerPilot?.SetHumanInput(0f, 0f);

            if (_wheelReturnCoroutine != null)
            {
                StopCoroutine(_wheelReturnCoroutine);
                _wheelReturnCoroutine = null;
            }

            var pilot = GetPlayer(clientId);
            pilot?.ServerBeginPilot(this, _pilotPoint.position, _pilotPoint.rotation);

            var target = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
            };
            pilot?.EnterPilotClientRpc(_pilotPoint.rotation.eulerAngles.y, target);
        }

        [ServerRpc(RequireOwnership = false)]
        private void ExitPilotServerRpc(ServerRpcParams rpcParams = default)
        {
            var clientId = rpcParams.Receive.SenderClientId;
            if (_pilotId.Value != clientId) return;

            _pilotId.Value = ulong.MaxValue;
            _steerInput = 0f;
            _throttleInput = 0f;

            var pilot = GetPlayer(clientId);
            pilot?.ServerExitPilot();

            var target = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
            };
            pilot?.ExitPilotClientRpc(target);

            if (_wheelReturnCoroutine != null) StopCoroutine(_wheelReturnCoroutine);
            _wheelReturnCoroutine = StartCoroutine(ReturnWheelToNeutral());
        }

        [ServerRpc(RequireOwnership = false)]
        public void SetPilotInputServerRpc(float steer, float throttle, ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != _pilotId.Value) return;
            _playerPilot?.SetHumanInput(steer, throttle);
        }

        public bool HasHumanPilot => _pilotId.Value != ulong.MaxValue;

        public void SetInput(float steer, float throttle)
        {
            if (!IsServer) return;
            _steerInput = steer;
            _throttleInput = throttle;
        }


        private PlayerController GetPlayer(ulong clientId)
        {
            if (NetworkManager.ConnectedClients.TryGetValue(clientId, out var client))
                return client.PlayerObject != null ? client.PlayerObject.GetComponent<PlayerController>() : null;
            return null;
        }

        private IEnumerator ReturnWheelToNeutral()
        {
            while (Mathf.Abs(_wheelAngle.Value) > 0.01f)
            {
                _wheelAngle.Value = Mathf.MoveTowards(_wheelAngle.Value, 0f, _wheelReturnSpeed * Time.deltaTime);
                yield return null;
            }
            _wheelAngle.Value = 0f;
            _wheelReturnCoroutine = null;
        }
    }
}
