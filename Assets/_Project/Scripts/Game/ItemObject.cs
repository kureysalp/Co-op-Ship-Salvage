using System;
using UnityEngine;

namespace _Project.Scripts.Game
{
    public enum ItemType
    {
        Resource,
        Consumable,
        Equipable
    }

    [CreateAssetMenu(fileName = "SO_Item", menuName = "Item Asset")]
    public class ItemObject : ScriptableObject
    {
        [ReadOnly, SerializeField] private string _uId;
        [SerializeField] private string _itemName;
        [SerializeField] private Sprite _itemIcon;
        [SerializeField] private bool _isStackable;
        [SerializeField] private ItemType _itemType;
        [SerializeField] private GameObject _equipPrefab;

        public string UId => _uId;
        public string ItemName => _itemName;
        public Sprite ItemIcon => _itemIcon;
        public bool IsStackable => _isStackable;
        public ItemType Type => _itemType;
        public GameObject EquipPrefab => _equipPrefab;
        public bool IsEquipable => _itemType == ItemType.Equipable;

#if UNITY_EDITOR
        private void OnValidate()
        {
            var assetPath = UnityEditor.AssetDatabase.GetAssetPath(this);
            if(string.IsNullOrEmpty(assetPath))
            {
                _uId = string.Empty;
                return;
            }
            
            var assetGuid = UnityEditor.AssetDatabase.AssetPathToGUID(assetPath);
            
            if (string.IsNullOrEmpty(UId) || UId != assetGuid)
                _uId = assetGuid;

        }
#endif
    }
    
    public class ReadOnlyAttribute : PropertyAttribute { }
#if UNITY_EDITOR
    [UnityEditor.CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
    public class ReadOnlyDrawer : UnityEditor.PropertyDrawer
    {
        public override void OnGUI(Rect position, UnityEditor.SerializedProperty property, GUIContent label)
        {
            GUI.enabled = false;
            UnityEditor.EditorGUI.PropertyField(position, property, label);
            GUI.enabled = true;
        }
    }
#endif
}