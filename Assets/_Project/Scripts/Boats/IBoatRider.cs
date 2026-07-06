using UnityEngine;

namespace ShipSalvage.Boats
{
    public interface IBoatRider
    {
        void BoardBoat(BoatController boat);
        void LeaveBoat(BoatController boat);
        void RideBoat(Vector3 deltaPosition, float deltaYaw, Vector3 pivot);
    }
}
