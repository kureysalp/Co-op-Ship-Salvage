using System;
using System.Collections.Generic;
using UnityEngine;

namespace _Project.Scripts.Game
{
    public class ItemDatabase : MonoBehaviour
    {
        public static ItemDatabase Instance { get; private set; }
        
        [SerializeField] private List<ItemObject> _allItems = new();
        
        private Dictionary<string, ItemObject> _itemLookupTable = new ();

        private void Awake()
        {
            if(Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            InitializeItemDictionary();
        }

        private void InitializeItemDictionary()
        {
            _itemLookupTable.Clear();
            foreach (var item in _allItems)
            {
                if (_itemLookupTable.TryAdd(item.UId, item)) continue;
                Debug.LogWarning($"[ItemDatabase] Duplicate item '{item.UId}'.");
            }
        }

        public bool GetItemById(string id, out ItemObject item)
        {
            item = null;
            if (string.IsNullOrEmpty(id)) return false;

            if (_itemLookupTable.TryGetValue(id, out item)) return true;

            Debug.LogWarning($"[ItemDatabase] Item with ID '{id}' could not be found.");
            return false;
        }
    }
}