using _Project.Scripts.Game;
using UnityEngine;

namespace ShipSalvage.Player
{
    public class Gun : Item
    {
        [SerializeField] private Transform _muzzle;
        [SerializeField] private ParticleSystem _muzzleFlash;
        [SerializeField] private GunData _data;

        public GunData Data => _data;
        public Transform Muzzle => _muzzle;

        public void PlayMuzzleFlash()
        {
            if (_muzzleFlash != null)
                _muzzleFlash.Play();
        }
    }
}
