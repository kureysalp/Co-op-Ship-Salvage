using UnityEngine;

namespace _Project.Scripts.Game
{
    public class Item : MonoBehaviour
    {
        [SerializeField] private ItemObject _itemObject;

        public ItemObject ItemObject => _itemObject;

        public virtual void OnEquipped() { }
        public virtual void OnUnequipped() { }
    }
}
