using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ShipSalvage.Core
{
    public class ShipSpawnPoint : MonoBehaviour
    {
        [SerializeField] private float _radius = 20f;
        [SerializeField] private Color _gizmoColor = new Color(1f, 0.35f, 0.2f, 1f);

        public float Radius => _radius;

        public Vector3 GetRandomPosition()
        {
            Vector2 c = Random.insideUnitCircle * _radius;
            return transform.position + new Vector3(c.x, 0f, c.y);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Handles.color = _gizmoColor;
            Handles.DrawWireDisc(transform.position, Vector3.up, _radius);

            var faded = _gizmoColor;
            faded.a *= 0.08f;
            Handles.color = faded;
            Handles.DrawSolidDisc(transform.position, Vector3.up, _radius);
        }
#endif
    }
}
