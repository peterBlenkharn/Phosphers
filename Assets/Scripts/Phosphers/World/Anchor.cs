using UnityEngine;

namespace Phosphers.World
{
    public class Anchor : MonoBehaviour
    {
        [Tooltip("World-space radius to the OUTER edge of the anchor ring.")]
        [SerializeField] private float outerRadius = 0.8f;
        public float OuterRadius => outerRadius;

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, outerRadius);
        }
    }
}
