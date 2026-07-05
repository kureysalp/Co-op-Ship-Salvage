using ShipSalvage.Player;

namespace ShipSalvage.Utils
{
    public interface IInteractable
    {
        bool CanInteract(PlayerController player);
        void Highlight(bool active);
        void Interact(PlayerController player);
        string GetInteractionLabel(PlayerController player);
    }
}
