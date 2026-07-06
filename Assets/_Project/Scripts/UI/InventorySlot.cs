using UnityEngine;
using UnityEngine.EventSystems;

namespace ShipSalvage.UI
{
    public enum SlotKind
    {
        Bag,
        Hotbar
    }

    public class InventorySlot : MonoBehaviour, IDropHandler
    {
        [SerializeField] private SlotKind _kind;
        [SerializeField] private ItemHolder _holder;
        [SerializeField] private GameObject _activeHighlight;

        public SlotKind Kind => _kind;
        public int Index { get; private set; }
        public ItemHolder Holder => _holder;

        public void Configure(int index, SlotKind kind)
        {
            Index = index;
            _kind = kind;
            SetActiveHighlight(false);
        }

        public void SetActiveHighlight(bool active)
        {
            if (_activeHighlight != null)
                _activeHighlight.SetActive(active);
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (InventoryUI.Instance != null)
                InventoryUI.Instance.DropOnSlot(this);
        }
    }
}
