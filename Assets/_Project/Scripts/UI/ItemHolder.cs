using _Project.Scripts.Game;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShipSalvage.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public class ItemHolder : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TextMeshProUGUI _countText;

        private CanvasGroup _canvasGroup;
        private InventorySlot _slot;

        public ItemObject Item { get; private set; }
        public int Quantity { get; private set; }
        public bool IsEmpty => Item == null;
        public InventorySlot Slot => _slot;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            _slot = GetComponentInParent<InventorySlot>();
            Clear();
        }

        public void Set(ItemObject item, int quantity)
        {
            Item = item;
            Quantity = quantity;

            if (_icon != null)
            {
                _icon.sprite = item != null ? item.ItemIcon : null;
                _icon.enabled = item != null;
            }

            if (_countText != null)
                _countText.text = quantity > 1 ? quantity.ToString() : string.Empty;
        }

        public void Clear()
        {
            Item = null;
            Quantity = 0;

            if (_icon != null)
            {
                _icon.sprite = null;
                _icon.enabled = false;
            }

            if (_countText != null)
                _countText.text = string.Empty;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (IsEmpty || InventoryUI.Instance == null) return;

            _canvasGroup.blocksRaycasts = false;
            InventoryUI.Instance.BeginDrag(this);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (InventoryUI.Instance == null) return;
            InventoryUI.Instance.Drag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _canvasGroup.blocksRaycasts = true;

            if (InventoryUI.Instance == null) return;
            InventoryUI.Instance.EndDrag();
        }
    }
}
