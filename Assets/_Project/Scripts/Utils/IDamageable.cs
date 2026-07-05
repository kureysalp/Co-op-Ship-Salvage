namespace ShipSalvage.Utils
{
    public interface IDamageable
    {
        void TakeDamage(float amount, ulong instigatorClientId);
    }
}
