using UnityEngine;

namespace Phosphers.Agents
{
    [CreateAssetMenu(menuName = "Phosphers/Phospher Settings")]
    public class PhospherSettings : ScriptableObject
    {
        [Header("Kinematics")]
        public float maxSpeed = 3.5f;
        public float maxAccel = 10f;
        [Tooltip("Max turning rate, degrees per second")]
        public float maxTurnRateDeg = 720f;

        [Header("Neighbourhood Radii")]
        public float neighbourRadius = 3.0f;      // Cohesion/Alignment sensing
        public float separationRadius = 1.0f;     // Stronger push when very close

        [Header("Anchor Interaction")]
        public float anchorAvoidMargin = 0.2f;     // extra stand-off beyond ring edge (for FORAGE)
        public float anchorAvoidForce = 8f;        // avoidance strength when within avoid radius
        public float anchorEdgeSnapMargin = 0.02f; // how close counts as 'touching the edge'

        [Header("Anchor Exit Behaviour")]
        [Tooltip("Speed multiplier after depositing (relative to the speed just before contact).")]
        public float anchorExitSpeedFactor = 0.9f;
        [Tooltip("Random angle (deg) added to the reversed direction at exit.")]
        public float anchorExitJitterDeg = 12f;
        [Tooltip("Tiny outward push so we don't immediately collide again.")]
        public float anchorCollisionEpsilon = 0.01f;

        [Header("Anchor Obstacle (FORAGE)")]
        public float anchorAvoidBand = 0.25f;       // thickness of the boundary layer outside the rim
        public float anchorAvoidNormalK = 10f;      // outward push strength
        public float anchorAvoidTangentK = 8f;      // slide strength along rim

        [Header("FORAGE Collision")]
        public float forageSlideSpeedFactor = 0.9f; // keep ~incoming speed when sliding
        public float forageCollisionEpsilon = 0.01f;// tiny push so we don't re-collide immediately

        [Header("Recovery (inside-anchor failsafe)")]
        [Tooltip("How far 'inside' the ring counts as being stuck (prevents jitter at the rim).")]
        public float recoverInsideSlack = 0.02f;
        [Tooltip("How far beyond the rim to place the agent when it escapes.")]
        public float recoverExitBuffer = 0.04f;
        [Tooltip("Outward speed while escaping (world units/sec).")]
        public float recoverStepOutSpeed = 8f;
        [Tooltip("After escaping, ignore flocking for this long so it doesn't get pulled back immediately.")]
        public float recoverHoldSeconds = 0.12f;
        [Tooltip("Small angle jitter (deg) applied to outward direction on escape.")]
        public float recoverJitterDeg = 10f;

        [Header("Render")]
        [Tooltip("If your sprite points +Y, set to +90. Default assumes +X.")]
        public float spriteForwardOffsetDeg = 0f;

        [Header("Seek Behaviour")]
        [Tooltip("How hard we steer toward a seen Bit (higher beats flocking).")]
        public float seekSteerWeight = 3.0f;
        [Range(0f, 1f), Tooltip("Multiply Cohesion/Alignment by this in Seek.")]
        public float seekFlockDampen = 0.25f;


        [Header("Weights")]
        public float weightCohesion = 0.8f;
        public float weightAlignment = 1.0f;
        public float weightSeparation = 1.5f;
        public float weightSignal = 1.0f;      // from vector field (Signals)
        public float weightReturn = 2.0f;      // seek Anchor when returning
        public float weightNoise = 0.25f;     // small wander jitter

        [Header("Noise/Wander")]
        public float noiseJitterPerSec = 1.0f;    // magnitude of random accel added per second

        [Header("Return/Deposit")]
        public float depositRadius = 0.6f;

        [Header("Lifetime (optional)")]
        public bool useLifetime = false;
        public Vector2 lifetimeSecondsRange = new Vector2(30, 45);
        public float PickLifetime() => useLifetime ? Random.Range(lifetimeSecondsRange.x, lifetimeSecondsRange.y) : Mathf.Infinity;
    }
}
