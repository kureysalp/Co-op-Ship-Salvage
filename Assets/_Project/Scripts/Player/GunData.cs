using UnityEngine;

namespace ShipSalvage.Player
{
    [CreateAssetMenu(fileName = "SO_GunData", menuName = "Weapon/Gun Data")]
    public class GunData : ScriptableObject
    {
        [SerializeField] private float _damage = 10f;
        [SerializeField] private float _fireRate = 3f;
        [SerializeField] private int _magazineSize = 12;
        [SerializeField] private float _reloadTime = 1.5f;
        [SerializeField] private float _range = 100f;

        public float Damage => _damage;
        public float FireRate => _fireRate;
        public int MagazineSize => _magazineSize;
        public float ReloadTime => _reloadTime;
        public float Range => _range;
    }
}
