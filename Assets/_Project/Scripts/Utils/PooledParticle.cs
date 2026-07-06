using UnityEngine;

namespace ShipSalvage.Utils
{
    public class PooledParticle : MonoBehaviour
    {
        [SerializeField] private ParticleSystem _particle;
        [SerializeField] private float _lifetime = 2f;

        private float _releaseAt;
        private bool _active;

        public void Play()
        {
            if (_particle == null)
                _particle = GetComponentInChildren<ParticleSystem>(true);

            float duration = _lifetime;

            if (_particle != null)
            {
                _particle.Clear(true);
                _particle.Play(true);

                var main = _particle.main;
                duration = Mathf.Max(duration, main.duration + main.startLifetime.constantMax);
            }

            _releaseAt = Time.time + duration;
            _active = true;
        }

        private void Update()
        {
            if (!_active) return;
            if (Time.time < _releaseAt) return;

            _active = false;

            if (VfxPool.Instance != null)
                VfxPool.Instance.Release(gameObject);
        }
    }
}
