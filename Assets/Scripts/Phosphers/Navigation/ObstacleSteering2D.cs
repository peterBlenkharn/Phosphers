using UnityEngine;

namespace Phosphers.Navigation
{
    public struct CircleObstacle
    {
        public Vector2 center;
        public float radius; // rim radius
        public CircleObstacle(Vector2 c, float r) { center = c; radius = r; }
    }

    public static class ObstacleSteering2D
    {
        // Boundary-layer steering near a circle: strong near rim + tangent slide.
        public static Vector2 BoundaryLayerSteering(
            CircleObstacle o, Vector2 pos, Vector2 vel,
            float band, float kNormal, float kTangent)
        {
            // band: thickness of the avoidance layer outside the rim
            // kNormal: outward push; kTangent: slide along rim
            if (band <= 0f) return Vector2.zero;

            Vector2 to = pos - o.center;
            float dist = to.magnitude;
            if (dist <= 1e-6f) return Vector2.zero;

            float rim = o.radius + band;
            if (dist >= rim) return Vector2.zero; // outside layer => no steering

            Vector2 n = to / dist;                      // outward normal
            float depth = rim - dist;                   // how deep inside the layer
            float x = Mathf.Clamp01(depth / band);      // 0..1
            float w = x * x * (3f - 2f * x);           // smoothstep

            // Normal push (strongest at rim)
            Vector2 aN = n * (kNormal * w);

            // Tangent slide (choose sign consistent with current vel)
            Vector2 t = new Vector2(-n.y, n.x);
            float sign = Mathf.Sign(Vector2.Dot(t, vel));
            if (Mathf.Approximately(sign, 0f)) sign = 1f;
            Vector2 aT = t * (kTangent * w * sign);

            return aN + aT;
        }
    }
}
