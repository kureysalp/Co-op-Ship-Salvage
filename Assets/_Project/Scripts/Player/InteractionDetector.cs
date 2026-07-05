using ShipSalvage.UI;
using ShipSalvage.Utils;
using UnityEngine;

namespace ShipSalvage.Player
{
    public class InteractionDetector : MonoBehaviour
    {
        [SerializeField] private float _range = 3f;
        [SerializeField] private LayerMask _interactionLayerMask;

        private PlayerController _player;
        private IInteractable _currentInteractable;
        private bool _wasPiloting;

        private void Awake()
        {
            _player = GetComponent<PlayerController>();
        }

        private void Update()
        {
            if (!_player.IsOwner) return;

            var isPiloting = _player.IsPiloting;
            if (isPiloting != _wasPiloting)
            {
                _currentInteractable?.Highlight(false);
                _currentInteractable = null;
                InteractionMenu.Instance?.Hide();
                _wasPiloting = isPiloting;
            }

            if (isPiloting) return;

            var foundValid = false;
            var aim = _player.FirstPersonCamera != null ? _player.FirstPersonCamera.transform : _player.transform;
            var ray = new Ray(aim.position, aim.forward);
            if (Physics.Raycast(ray, out var hit, _range, _interactionLayerMask))
            {
                var interactable = hit.collider.GetComponentInParent<IInteractable>();
                if (interactable != null && interactable.CanInteract(_player))
                {
                    foundValid = true;

                    if (interactable != _currentInteractable)
                    {
                        _currentInteractable?.Highlight(false);
                        _currentInteractable = interactable;
                        _currentInteractable.Highlight(true);
                        ShowMenu(interactable);
                    }

                    if (InteractionMenu.Instance.IsVisible)
                    {
                        if (_player.MenuOption1Action.WasPressedThisFrame()) InteractionMenu.Instance.SelectOption(0);
                        else if (_player.MenuOption2Action.WasPressedThisFrame()) InteractionMenu.Instance.SelectOption(1);
                        else if (_player.MenuOption3Action.WasPressedThisFrame()) InteractionMenu.Instance.SelectOption(2);
                        else if (_player.MenuOption4Action.WasPressedThisFrame()) InteractionMenu.Instance.SelectOption(3);
                    }
                }
            }

            if (!foundValid && _currentInteractable != null)
            {
                _currentInteractable.Highlight(false);
                _currentInteractable = null;
                InteractionMenu.Instance?.Hide();
            }
        }

        private void ShowMenu(IInteractable interactable)
        {
            var player = _player;
            InteractionMenu.Instance?.Show(new[]
            {
                new InteractionMenu.InteractionOption
                {
                    Message = interactable.GetInteractionLabel(player),
                    OnSelected = () => _currentInteractable?.Interact(player)
                }
            });
        }
    }
}
