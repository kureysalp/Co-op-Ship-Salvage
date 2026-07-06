using Unity.Netcode;
using UnityEngine;

namespace ShipSalvage.Boats
{
    [RequireComponent(typeof(BoatController))]
    public abstract class BoatPilot : NetworkBehaviour
    {
        protected BoatController Boat;

        protected virtual void Awake()
        {
            Boat = GetComponent<BoatController>();
        }

        public abstract bool IsInCommand { get; }

        protected abstract bool Drive(out float steer, out float throttle);

        protected virtual void FixedUpdate()
        {
            if (!IsServer || Boat == null) return;
            if (!IsInCommand) return;

            if (Drive(out var steer, out var throttle))
                Boat.SetInput(steer, throttle);
        }
    }
}
