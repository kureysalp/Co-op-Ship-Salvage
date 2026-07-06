using System.Collections;
using UnityEngine;

namespace ShipSalvage.Utils
{
    public class BulletVisual : MonoBehaviour
    {
        [SerializeField] private float _arriveThreshold = 0.05f;
        [SerializeField] private float _lifeTime;

        private TrailRenderer _trail;
        private Vector3 _target;
        private Vector3 _normal;
        private bool _didHit;
        private float _speed;
        private bool _arrived;

        private void Awake()
        {
            _trail = GetComponentInChildren<TrailRenderer>();
        }

        public void Launch(Vector3 start, Vector3 target, Vector3 normal, bool didHit)
        {
            transform.position = start;
            _target = target;
            _normal = normal;
            _didHit = didHit;
            _arrived = false;
            _speed = VfxPool.Instance != null ? VfxPool.Instance.TracerSpeed : 120f;

            if (_trail != null)
                _trail.Clear();
        }

        private void Update()
        {
            if (_arrived) return;

            var position = transform.position;
            var toTarget = _target - position;
            var distance = toTarget.magnitude;
            var step = _speed * Time.deltaTime;

            if (distance <= step + _arriveThreshold)
            {
                Arrive();
                return;
            }

            transform.position = position + toTarget / distance * step;
        }

        private void Arrive()
        {
            _arrived = true;
            transform.position = _target;

            if (VfxPool.Instance == null) return;

            if (_didHit)
                VfxPool.Instance.SpawnHit(_target, _normal);

            StartCoroutine(CO_ReleaseVisual());
        }

        private IEnumerator CO_ReleaseVisual()
        {
            yield return WaitFor.Seconds(_lifeTime);
            VfxPool.Instance.Release(gameObject);
        }
    }
}
