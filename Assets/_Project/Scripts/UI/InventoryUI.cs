using System.Collections.Generic;
using _Project.Scripts.Game;
using ShipSalvage.Core;
using ShipSalvage.Player;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ShipSalvage.UI
{
    public class InventoryUI : MonoBehaviour
    {
        public static InventoryUI Instance { get; private set; }

        [SerializeField] private GameObject _bagPanel;
        [SerializeField] private InventorySlot[] _bagSlots;
        [SerializeField] private InventorySlot[] _hotbarSlots;
        [SerializeField] private RectTransform _dragLayer;

        private readonly List<string> _bagItemIds = new();
        private readonly List<string> _hotbarItemIds = new();
        private readonly Dictionary<string, int> _quantities = new();

        private PlayerController _player;
        private PlayerInventory _inventory;
        private PlayerEquipment _equipment;

        private ItemHolder _dragged;
        private Transform _draggedOriginParent;
        private int _activeHotbar;

        private static readonly Key[] HotbarKeys =
        {
            Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5,
            Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9
        };

        public bool IsBagOpen => _bagPanel != null && _bagPanel.activeSelf;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            for (var i = 0; i < _bagSlots.Length; i++)
            {
                _bagSlots[i].Configure(i, SlotKind.Bag);
                _bagItemIds.Add(string.Empty);
            }

            for (var i = 0; i < _hotbarSlots.Length; i++)
            {
                _hotbarSlots[i].Configure(i, SlotKind.Hotbar);
                _hotbarItemIds.Add(string.Empty);
            }

            if (_bagPanel != null)
                _bagPanel.SetActive(false);
        }

        private void OnEnable()
        {
            PlayerManager.Instance.OnPlayerRegistered += HandlePlayerRegistered;
            PlayerManager.Instance.OnPlayerUnregistered += HandlePlayerUnregistered;

            if (PlayerManager.Instance.LocalPlayer != null)
                Bind(PlayerManager.Instance.LocalPlayer);
        }

        private void OnDisable()
        {
            if (PlayerManager.Instance != null)
            {
                PlayerManager.Instance.OnPlayerRegistered -= HandlePlayerRegistered;
                PlayerManager.Instance.OnPlayerUnregistered -= HandlePlayerUnregistered;
            }

            Unbind();
        }

        private void Update()
        {
            if (_player == null) return;

            if (_player.ToggleInventoryAction != null && _player.ToggleInventoryAction.WasPressedThisFrame())
                ToggleBag();

            if (!IsBagOpen)
                HandleHotbarSelection();
        }

        private void HandlePlayerRegistered(ulong clientId, PlayerController player)
        {
            if (player.IsOwner)
                Bind(player);
        }

        private void HandlePlayerUnregistered(ulong clientId, PlayerController player)
        {
            if (player == _player)
                Unbind();
        }

        private void Bind(PlayerController player)
        {
            Unbind();

            _player = player;
            _inventory = player.Inventory;
            _equipment = player.Equipment;

            if (_inventory != null)
            {
                _inventory.OnInventoryChanged += HandleInventoryChanged;
                ReconcileFromInventory();
            }
        }

        private void Unbind()
        {
            if (_inventory != null)
                _inventory.OnInventoryChanged -= HandleInventoryChanged;

            _player = null;
            _inventory = null;
            _equipment = null;
        }

        private void HandleInventoryChanged(NetworkListEvent<InventoryItem> _)
        {
            ReconcileFromInventory();
        }

        private void ReconcileFromInventory()
        {
            _quantities.Clear();

            if (_inventory != null)
            {
                for (var i = 0; i < _inventory.Count; i++)
                {
                    var item = _inventory[i];
                    _quantities[item.ItemId.ToString()] = item.Quantity;
                }
            }

            for (var i = 0; i < _bagItemIds.Count; i++)
            {
                if (_bagItemIds[i].Length > 0 && !_quantities.ContainsKey(_bagItemIds[i]))
                    _bagItemIds[i] = string.Empty;
            }

            for (var i = 0; i < _hotbarItemIds.Count; i++)
            {
                if (_hotbarItemIds[i].Length > 0 && !_quantities.ContainsKey(_hotbarItemIds[i]))
                    _hotbarItemIds[i] = string.Empty;
            }

            foreach (var id in _quantities.Keys)
            {
                if (_bagItemIds.Contains(id)) continue;

                AssignToFreeBagSlot(id);

                if (IsEquipable(id) && !_hotbarItemIds.Contains(id))
                    AssignToFreeHotbarSlot(id);
            }

            Rebuild();
            UpdateHotbarHighlights();
            RefreshEquipForActive();
        }

        private void AssignToFreeBagSlot(string id)
        {
            for (var i = 0; i < _bagItemIds.Count; i++)
            {
                if (_bagItemIds[i].Length == 0)
                {
                    _bagItemIds[i] = id;
                    return;
                }
            }
        }

        private void AssignToFreeHotbarSlot(string id)
        {
            for (var i = 0; i < _hotbarItemIds.Count; i++)
            {
                if (_hotbarItemIds[i].Length == 0)
                {
                    _hotbarItemIds[i] = id;
                    return;
                }
            }
        }

        private void Rebuild()
        {
            for (var i = 0; i < _bagSlots.Length; i++)
                Paint(_bagSlots[i].Holder, _bagItemIds[i]);

            for (var i = 0; i < _hotbarSlots.Length; i++)
                Paint(_hotbarSlots[i].Holder, _hotbarItemIds[i]);
        }

        private void Paint(ItemHolder holder, string id)
        {
            if (holder == null) return;

            if (id.Length == 0 || !TryGetItem(id, out var item))
            {
                holder.Clear();
                return;
            }

            var quantity = _quantities.TryGetValue(id, out var q) ? q : 1;
            holder.Set(item, quantity);
        }

        public void BeginDrag(ItemHolder holder)
        {
            _dragged = holder;
            _draggedOriginParent = holder.transform.parent;

            if (_dragLayer != null)
                holder.transform.SetParent(_dragLayer, true);
        }

        public void Drag(PointerEventData eventData)
        {
            if (_dragged != null)
                _dragged.transform.position = eventData.position;
        }

        public void DropOnSlot(InventorySlot target)
        {
            if (_dragged == null) return;

            var source = _dragged.Slot;
            if (source == null || source == target) return;

            ApplyMove(source, target);
        }

        public void EndDrag()
        {
            if (_dragged != null && _draggedOriginParent != null)
            {
                _dragged.transform.SetParent(_draggedOriginParent, false);

                if (_dragged.transform is RectTransform rect)
                    rect.anchoredPosition = Vector2.zero;
            }

            _dragged = null;
            _draggedOriginParent = null;
            Rebuild();
        }

        private void ApplyMove(InventorySlot source, InventorySlot target)
        {
            var srcList = source.Kind == SlotKind.Bag ? _bagItemIds : _hotbarItemIds;
            var srcId = srcList[source.Index];
            if (srcId.Length == 0) return;

            if (target.Kind == SlotKind.Bag)
            {
                if (source.Kind == SlotKind.Bag)
                {
                    _bagItemIds[source.Index] = _bagItemIds[target.Index];
                    _bagItemIds[target.Index] = srcId;
                }
                else
                {
                    _hotbarItemIds[source.Index] = string.Empty;
                }
            }
            else
            {
                if (!IsEquipable(srcId)) return;

                if (source.Kind == SlotKind.Hotbar)
                {
                    _hotbarItemIds[source.Index] = _hotbarItemIds[target.Index];
                    _hotbarItemIds[target.Index] = srcId;
                }
                else
                {
                    for (var i = 0; i < _hotbarItemIds.Count; i++)
                        if (_hotbarItemIds[i] == srcId)
                            _hotbarItemIds[i] = string.Empty;

                    _hotbarItemIds[target.Index] = srcId;
                }
            }

            Rebuild();
            RefreshEquipForActive();
        }

        private void HandleHotbarSelection()
        {
            if (_hotbarSlots.Length == 0 || Keyboard.current == null) return;

            var max = Mathf.Min(_hotbarSlots.Length, HotbarKeys.Length);
            for (var i = 0; i < max; i++)
            {
                if (!Keyboard.current[HotbarKeys[i]].wasPressedThisFrame) continue;
                SelectHotbar(i);
                return;
            }
        }

        private void SelectHotbar(int index)
        {
            _activeHotbar = index;

            UpdateHotbarHighlights();
            RefreshEquipForActive();
        }

        private void UpdateHotbarHighlights()
        {
            for (var i = 0; i < _hotbarSlots.Length; i++)
                _hotbarSlots[i].SetActiveHighlight(i == _activeHotbar);
        }

        private void RefreshEquipForActive()
        {
            if (_equipment == null) return;
            if (_activeHotbar < 0 || _activeHotbar >= _hotbarItemIds.Count) return;

            var desired = _hotbarItemIds[_activeHotbar];
            if (desired.Length == 0) return;
            if (_equipment.EquippedItemId.ToString() == desired) return;

            _equipment.RequestEquip(new FixedString64Bytes(desired));
        }

        private void ToggleBag()
        {
            if (_bagPanel == null) return;

            var open = !_bagPanel.activeSelf;
            _bagPanel.SetActive(open);

            if (open)
                Rebuild();

            Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = open;
        }

        private bool IsEquipable(string id) => TryGetItem(id, out var item) && item.IsEquipable;

        private bool TryGetItem(string id, out ItemObject item)
        {
            item = null;
            return ItemDatabase.Instance != null && ItemDatabase.Instance.GetItemById(id, out item);
        }
    }
}
