using UnityEngine;

namespace ShipSalvage.Utils
{
    public static class WeaponSystem
    {
        public static bool Hitscan(
            Vector3 origin,
            Vector3 direction,
            float range,
            float damage,
            LayerMask mask,
            ulong instigatorClientId,
            out Vector3 end,
            out Vector3 normal)
        {
            if (Physics.Raycast(origin, direction, out var hit, range, mask, QueryTriggerInteraction.Ignore))
            {
                Debug.Log($"shooting at {hit.collider.gameObject.name}");
                var isDamageableHit = hit.collider.TryGetComponent(out IDamageable damageableHit);
                if (isDamageableHit)
                {
                    damageableHit.TakeDamage(damage, instigatorClientId);
                    Debug.Log($"damaging enemy {damageableHit} with {damage}");
                }
                end = hit.point;
                normal = hit.normal;
                return true;
            }

            end = origin + direction * range;
            normal = -direction;
            return false;
        }
    }
}
