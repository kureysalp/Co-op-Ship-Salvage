using ShipSalvage.Boats;
using ShipSalvage.UI;
using ShipSalvage.Utils;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShipSalvage.Player
{
    public struct PlayerInput : INetworkSerializable
    {
        public Vector2 Move;
        public float Yaw;
        public bool Sprint;
        public bool Jump;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Move);
            serializer.SerializeValue(ref Yaw);
            serializer.SerializeValue(ref Sprint);
            serializer.SerializeValue(ref Jump);
        }
    }

    public class PlayerController : NetworkBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float _moveSpeed = 5f;
        [SerializeField] private float _sprintSpeed = 9f;
        [SerializeField] private float _jumpForce = 5f;
        [SerializeField] private float _gravity = -20f;

        [Header("Look")]
        [SerializeField] private float _mouseSensitivity = 0.15f;
        [SerializeField] private float _pitchClamp = 85f;

        [Header("Prediction")]
        [SerializeField] private float _reconcileSnapDistance = 1.5f;
        [SerializeField] private float _remoteSmoothTime = 0.1f;

        [Header("References")]
        [SerializeField] private Camera _firstPersonCamera;
        [SerializeField] private Transform _cameraHolder;
        [SerializeField] private InputActionAsset _inputActions;

        private CharacterController _cc;
        private AnticipatedNetworkTransform _ant;
        private PlayerInventory _inventory;
        private PlayerEquipment _equipment;
        private Health _health;

        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _jumpAction;
        private InputAction _sprintAction;
        private InputAction _menuOption1Action;
        private InputAction _menuOption2Action;
        private InputAction _menuOption3Action;
        private InputAction _menuOption4Action;
        private InputAction _toggleInventoryAction;
        private InputAction _previousAction;
        private InputAction _nextAction;
        private InputAction _fireAction;
        private InputAction _reloadAction;

        private float _localYaw;
        private float _cameraPitch;
        private Vector2 _moveInput;
        private bool _sprintHeld;
        private bool _jumpLatch;
        private float _ownerShipLastYaw;

        private PlayerInput _serverInput;
        private bool _pendingJump;
        private Vector3 _velocity;
        private Vector3 _shipLastPos;
        private float _shipLastYaw;

        private BoatController _currentShip;
        private bool _pilotMode;

        private float _lastSteerSent;
        private float _lastThrottleSent;

        private readonly NetworkVariable<NetworkObjectReference> _shipRef = new();

        public Camera FirstPersonCamera => _firstPersonCamera;
        public bool IsPiloting => _pilotMode;
        public InputAction MenuOption1Action => _menuOption1Action;
        public InputAction MenuOption2Action => _menuOption2Action;
        public InputAction MenuOption3Action => _menuOption3Action;
        public InputAction MenuOption4Action => _menuOption4Action;

        public PlayerInventory Inventory => _inventory;
        public PlayerEquipment Equipment => _equipment;
        public InputAction ToggleInventoryAction => _toggleInventoryAction;
        public InputAction PreviousAction => _previousAction;
        public InputAction NextAction => _nextAction;
        public InputAction FireAction => _fireAction;
        public InputAction ReloadAction => _reloadAction;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _ant = GetComponent<AnticipatedNetworkTransform>();
            _inventory = GetComponent<PlayerInventory>();
            _equipment = GetComponent<PlayerEquipment>();

            var playerMap = _inputActions.FindActionMap("Player");
            _moveAction = playerMap.FindAction("Move");
            _lookAction = playerMap.FindAction("Look");
            _jumpAction = playerMap.FindAction("Jump");
            _sprintAction = playerMap.FindAction("Sprint");
            _menuOption1Action = playerMap.FindAction("MenuOption1");
            _menuOption2Action = playerMap.FindAction("MenuOption2");
            _menuOption3Action = playerMap.FindAction("MenuOption3");
            _menuOption4Action = playerMap.FindAction("MenuOption4");
            _toggleInventoryAction = playerMap.FindAction("ToggleInventory");
            _previousAction = playerMap.FindAction("Previous");
            _nextAction = playerMap.FindAction("Next");
            _fireAction = playerMap.FindAction("Attack");
            _reloadAction = playerMap.FindAction("Reload");
        }

        public override void OnNetworkSpawn()
        {
            Core.PlayerManager.Instance.Register(OwnerClientId, this);

            _cc.enabled = IsServer || IsOwner;

            if (IsServer)
            {
                _shipLastPos = transform.position;
                _shipLastYaw = transform.eulerAngles.y;
                _serverInput.Yaw = transform.eulerAngles.y;

                _health = GetComponent<Health>();
                if (_health != null)
                    _health.OnDeath += HandleServerDeath;
            }

            _shipRef.OnValueChanged += OnShipRefChanged;

            if (!IsOwner)
            {
                _firstPersonCamera.enabled = false;
                return;
            }

            _localYaw = transform.eulerAngles.y;

            _firstPersonCamera.enabled = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            _moveAction.Enable();
            _lookAction.Enable();
            _jumpAction.Enable();
            _sprintAction.Enable();
            _menuOption1Action.Enable();
            _menuOption2Action.Enable();
            _menuOption3Action.Enable();
            _menuOption4Action.Enable();
            _toggleInventoryAction.Enable();
            _previousAction.Enable();
            _nextAction.Enable();
            _fireAction?.Enable();
            _reloadAction?.Enable();

            ResolveShipRef();
            NetworkManager.NetworkTickSystem.Tick += OnNetworkTick;
        }

        public override void OnNetworkDespawn()
        {
            Core.PlayerManager.Instance.Unregister(OwnerClientId);

            if (IsServer && _health != null)
                _health.OnDeath -= HandleServerDeath;

            _shipRef.OnValueChanged -= OnShipRefChanged;

            if (!IsOwner) return;

            if (NetworkManager != null && NetworkManager.NetworkTickSystem != null)
                NetworkManager.NetworkTickSystem.Tick -= OnNetworkTick;

            _moveAction.Disable();
            _lookAction.Disable();
            _jumpAction.Disable();
            _sprintAction.Disable();
            _menuOption1Action.Disable();
            _menuOption2Action.Disable();
            _menuOption3Action.Disable();
            _menuOption4Action.Disable();
            _toggleInventoryAction.Disable();
            _previousAction.Disable();
            _nextAction.Disable();
            _fireAction?.Disable();
            _reloadAction?.Disable();
        }

        private bool IsBagOpen => InventoryUI.Instance != null && InventoryUI.Instance.IsBagOpen;

        private void Update()
        {
            if (!IsOwner) return;

            if (IsBagOpen)
            {
                _moveInput = Vector2.zero;
                _sprintHeld = false;
                return;
            }

            HandleLook();

            if (_pilotMode)
            {
                HandlePiloting();
                return;
            }

            _moveInput = _moveAction.ReadValue<Vector2>();
            _sprintHeld = _sprintAction.IsPressed();
            var jumpPressed = _jumpAction.WasPressedThisFrame();
            if (jumpPressed)
                _jumpLatch = true;
            
            if (IsOwner && !IsServer)
            {
                if (CanPredict())
                    PredictMove(_moveInput, _sprintHeld, jumpPressed, Time.deltaTime);
                else
                    _velocity = Vector3.zero;
            }
        }

        private void LateUpdate()
        {
            if (!IsOwner || _cameraHolder == null) return;
            
            _cameraHolder.rotation = Quaternion.Euler(_cameraPitch, _localYaw, 0f);
        }

        private void HandleLook()
        {
            var delta = _lookAction.ReadValue<Vector2>() * _mouseSensitivity;
            _localYaw += delta.x;
            
            if (_currentShip != null)
            {
                var shipYaw = _currentShip.transform.eulerAngles.y;
                _localYaw += Mathf.DeltaAngle(_ownerShipLastYaw, shipYaw);
                _ownerShipLastYaw = shipYaw;
            }

            _cameraPitch = Mathf.Clamp(_cameraPitch - delta.y, -_pitchClamp, _pitchClamp);
        }

        private void HandlePiloting()
        {
            if (_currentShip == null) return;

            if (_menuOption1Action.WasPressedThisFrame())
            {
                _currentShip.Interact(this);
                return;
            }

            var mv = _moveAction.ReadValue<Vector2>();
            if (!Mathf.Approximately(mv.x, _lastSteerSent) || !Mathf.Approximately(mv.y, _lastThrottleSent))
            {
                _currentShip.SetPilotInputServerRpc(mv.x, mv.y);
                _lastSteerSent = mv.x;
                _lastThrottleSent = mv.y;
            }
        }

        private void OnNetworkTick()
        {
            SubmitInputServerRpc(new PlayerInput
            {
                Move = _pilotMode ? Vector2.zero : _moveInput,
                Yaw = _localYaw,
                Sprint = _sprintHeld,
                Jump = _jumpLatch
            });
            _jumpLatch = false;
        }

        [ServerRpc]
        private void SubmitInputServerRpc(PlayerInput input)
        {
            _serverInput = input;
            if (input.Jump)
                _pendingJump = true;
        }
        

        private void FixedUpdate()
        {
            if (!IsServer) return;
            
            transform.rotation = Quaternion.Euler(0f, _serverInput.Yaw, 0f);

            if (!_pilotMode)
                ServerMove();

            if (_currentShip != null)
                ServerFollowShip();
        }

        private void ServerMove()
        {
            var speed = _serverInput.Sprint ? _sprintSpeed : _moveSpeed;
            var move = (transform.right * _serverInput.Move.x + transform.forward * _serverInput.Move.y) * speed;

            if (_cc.isGrounded && _velocity.y < 0f)
                _velocity.y = -2f;

            if (_pendingJump)
            {
                if (_cc.isGrounded)
                    _velocity.y = _jumpForce;
                _pendingJump = false;
            }

            _velocity.y += _gravity * Time.fixedDeltaTime;

            _cc.Move((move + Vector3.up * _velocity.y) * Time.fixedDeltaTime);
        }
        
        private bool CanPredict() => !_pilotMode && _currentShip == null;

        private void PredictMove(Vector2 moveInput, bool sprint, bool jump, float dt)
        {
            transform.rotation = Quaternion.Euler(0f, _localYaw, 0f);

            var speed = sprint ? _sprintSpeed : _moveSpeed;
            var move = (transform.right * moveInput.x + transform.forward * moveInput.y) * speed;

            if (_cc.isGrounded && _velocity.y < 0f)
                _velocity.y = -2f;

            if (jump && _cc.isGrounded)
                _velocity.y = _jumpForce;

            _velocity.y += _gravity * dt;

            _cc.Move((move + Vector3.up * _velocity.y) * dt);

            _ant.AnticipateMove(transform.position);
            _ant.AnticipateRotate(Quaternion.Euler(0f, _localYaw, 0f));
        }
        
        public override void OnReanticipate(double lastRoundTripTime)
        {
            if (IsServer || !IsSpawned) return;

            var auth = _ant.AuthoritativeState;

            if (IsOwner && CanPredict())
            {
                var predicted = _ant.PreviousAnticipatedState;
                if (Vector3.Distance(auth.Position, predicted.Position) > _reconcileSnapDistance)
                {
                    _ant.Smooth(predicted, auth, _remoteSmoothTime);
                    _velocity.y = 0f;
                }
                else
                {
                    _ant.AnticipateMove(predicted.Position);
                    _ant.AnticipateRotate(predicted.Rotation);
                }
            }
            else
            {
                _ant.Smooth(_ant.PreviousAnticipatedState, auth, _remoteSmoothTime);
            }
        }

        private void ServerFollowShip()
        {
            var shipPos = _currentShip.transform.position;
            var shipYaw = _currentShip.transform.eulerAngles.y;

            var deltaPos = shipPos - _shipLastPos;
            var deltaYaw = Mathf.DeltaAngle(_shipLastYaw, shipYaw);
            _shipLastPos = shipPos;
            _shipLastYaw = shipYaw;

            if (deltaPos == Vector3.zero && deltaYaw == 0f) return;

            var offset = transform.position - shipPos;
            var rotationMove = Quaternion.Euler(0f, deltaYaw, 0f) * offset - offset;
            _cc.Move(deltaPos + rotationMove);
        }

        public void OnBoardShip(BoatController ship)
        {
            if (!IsServer) return;

            _currentShip = ship;
            _shipLastPos = ship.transform.position;
            _shipLastYaw = ship.transform.eulerAngles.y;
            _shipRef.Value = new NetworkObjectReference(ship.NetworkObject);
        }

        public void OnLeaveShip()
        {
            if (!IsServer) return;
            if (_pilotMode) return;

            _currentShip = null;
            _shipRef.Value = default;
        }

        private void HandleServerDeath(ulong instigatorClientId)
        {
            if (!IsServer) return;

            var spawn = Core.SpawnPoints.Instance != null ? Core.SpawnPoints.Instance.GetPlayerSpawn() : Vector3.zero;

            _pilotMode = false;
            _currentShip = null;
            _shipRef.Value = default;

            _cc.enabled = false;
            transform.position = spawn;
            _cc.enabled = true;

            _velocity = Vector3.zero;
            _pendingJump = false;

            if (_health != null)
                _health.ResetHealth();
        }
        
        public void ServerBeginPilot(BoatController ship, Vector3 pos, Quaternion rot)
        {
            _pilotMode = true;
            _cc.enabled = false;
            transform.SetPositionAndRotation(pos, rot);
            _cc.enabled = true;
            _currentShip = ship;
            _shipLastPos = ship.transform.position;
            _shipLastYaw = ship.transform.eulerAngles.y;
            _velocity = Vector3.zero;
            _serverInput.Yaw = rot.eulerAngles.y;
            _shipRef.Value = new NetworkObjectReference(ship.NetworkObject);
        }

        public void ServerExitPilot()
        {
            _pilotMode = false;
        }

        [ClientRpc]
        public void EnterPilotClientRpc(float seatYaw, ClientRpcParams rpcParams = default)
        {
            _pilotMode = true;
            _localYaw = seatYaw;
            InteractionMenu.Instance?.Hide();
            _lastSteerSent = 0f;
            _lastThrottleSent = 0f;
            ResolveShipRef();
        }

        [ClientRpc]
        public void ExitPilotClientRpc(ClientRpcParams rpcParams = default)
        {
            _pilotMode = false;
        }

        private void OnShipRefChanged(NetworkObjectReference previous, NetworkObjectReference current)
        {
            if (IsServer) return;
            ResolveShipRef();
        }

        private void ResolveShipRef()
        {
            if (!IsOwner || IsServer) return;

            if (_shipRef.Value.TryGet(out var netObj) && netObj.TryGetComponent(out BoatController ship))
            {
                _currentShip = ship;
                _ownerShipLastYaw = ship.transform.eulerAngles.y;
            }
            else
                _currentShip = null;
        }
    }
}
