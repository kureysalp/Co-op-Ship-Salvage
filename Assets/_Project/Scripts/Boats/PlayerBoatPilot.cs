namespace ShipSalvage.Boats
{
    public class PlayerBoatPilot : BoatPilot
    {
        private float _steer;
        private float _throttle;

        public override bool IsInCommand => Boat != null && Boat.HasHumanPilot;

        public void SetHumanInput(float steer, float throttle)
        {
            _steer = steer;
            _throttle = throttle;
        }

        protected override bool Drive(out float steer, out float throttle)
        {
            steer = _steer;
            throttle = _throttle;
            return true;
        }
    }
}
