using TMPro;
using UnityEngine;

namespace ShipSalvage.UI
{
    public class InteractionMenu : MonoBehaviour
    {
        public class InteractionOption
        {
            public string Message;
            public System.Action OnSelected;
        }

        public static InteractionMenu Instance { get; private set; }

        [SerializeField] private GameObject _container;
        [SerializeField] private TextMeshProUGUI[] _optionTexts;

        private static readonly string[] k_Prefixes = { "[E]", "[Q]", "[F]", "[G]" };

        private InteractionOption[] _currentOptions;

        public bool IsVisible => _container.activeSelf;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
            _container.SetActive(false);
        }

        public void Show(InteractionOption[] options)
        {
            _currentOptions = options;
            var count = Mathf.Min(options.Length, _optionTexts.Length);
            for (var i = 0; i < _optionTexts.Length; i++)
            {
                if (i < count)
                {
                    _optionTexts[i].text = $"{k_Prefixes[i]} {options[i].Message}";
                    _optionTexts[i].gameObject.SetActive(true);
                }
                else
                {
                    _optionTexts[i].gameObject.SetActive(false);
                }
            }
            _container.SetActive(true);
        }

        public void Hide()
        {
            _currentOptions = null;
            _container.SetActive(false);
        }

        public void SelectOption(int index)
        {
            if (_currentOptions == null || index >= _currentOptions.Length) return;
            _currentOptions[index].OnSelected?.Invoke();
        }
    }
}
