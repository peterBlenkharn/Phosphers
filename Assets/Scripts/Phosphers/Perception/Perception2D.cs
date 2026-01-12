//Perception2D.cs

using UnityEngine;
using Phosphers.Resources;
using Phosphers.Agents;

namespace Phosphers.Perception
{
    /// Add to a Phospher; updates at a cadence and exposes the nearest available Bit that fits inventory.
    public class Perception2D : MonoBehaviour
    {
        [Header("Sight")]
        public float sightRadius = 4f;
        [Range(0f, 180f)] public float fovHalfAngle = 180f; // 180 = 360° FOV
        public LayerMask bitLayer;           // set to your "Bits" layer
        public float tickInterval = 0.08f;   // 12.5 Hz is plenty
        public int maxColliders = 32;

        [Header("Debug")]
        public bool drawGizmos = true;

        public IBitTarget Nearest { get; private set; }

        private Collider2D[] _hits;
        private float _timer;
        private Transform _self;
        private PhospherInventory _inventory;

        private void Awake()
        {
            _hits = new Collider2D[Mathf.Max(8, maxColliders)];
            _self = transform;
            // inventory is composed in Phospher; we read it via a getter the Phospher will expose
        }

        public void BindInventory(PhospherInventory inv) => _inventory = inv;

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = Mathf.Max(0.02f, tickInterval);

            Nearest = ScanNearest();
        }

        private IBitTarget ScanNearest()
        {
            if (_inventory == null) return null;

            int n = Physics2D.OverlapCircleNonAlloc(_self.position, sightRadius, _hits, bitLayer, -Mathf.Infinity, Mathf.Infinity);
            IBitTarget best = null;
            float bestD2 = float.PositiveInfinity;

            for (int i = 0; i < n; i++)
            {
                var c = _hits[i];
                if (c == null) continue;

                // Try to get an IBitTarget component
                var target = c.GetComponent<IBitTarget>();
                if (target == null) continue;
                if (!target.IsAvailable) continue;
                if (!_inventory.CanAccept(target.Spec)) continue;

                Vector2 to = target.Position - (Vector2)_self.position;
                float d2 = to.sqrMagnitude;

                // Optional FOV
                if (fovHalfAngle < 180f && to.sqrMagnitude > 1e-6f)
                {
                    Vector2 fwd = _self.right; // assume +X is forward
                    float ang = Vector2.Angle(fwd, to);
                    if (ang > fovHalfAngle) continue;
                }

                if (d2 < bestD2)
                {
                    bestD2 = d2;
                    best = target;
                }
            }
            return best;
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, sightRadius);
        }
    }
}
