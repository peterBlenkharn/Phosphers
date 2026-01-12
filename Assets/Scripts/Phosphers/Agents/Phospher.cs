using UnityEngine;
using Phosphers.Core;
using Phosphers.Signals;
using Phosphers.Perception;
using Phosphers.Resources;

namespace Phosphers.Agents
{
    public class Phospher : MonoBehaviour
    {
        [SerializeField] private PhospherSettings settings;
        [SerializeField] private bool enableDevHotkeys = true;

        [Header("Runtime (read-only)")]
        [SerializeField] private PhospherState state = PhospherState.Forage;
        [SerializeField] private string debugStateName = "Forage";
        [SerializeField] private float lifetimeRemaining;
        [SerializeField] private Vector2 velocity;

        // Provided by manager on Init
        private PhospherManager _mgr;
        private Transform _anchor;
        private IVectorField _field;

        private float _anchorOuterRadius;

        // track what we should go back to after Recover
        private PhospherState _resumeState = PhospherState.Forage;
        private float _recoverTimer;                 // time spent in Recover
        private float _suppressSteeringTimer;        // countdown to temporarily ignore flocking/signals

        public event System.Action<IBitTarget> OnBitSeen;      // fires when we acquire a target
        [SerializeField] private Perception2D perception;      // drag on prefab
        [SerializeField] private PhospherInventory inventory = new PhospherInventory();
        private IBitTarget _seekTarget;

        public event System.Action<BitSpec> OnBitPickedUp;
        public event System.Action<BitSpec> OnBitDeposited;

        // readonly vars for fogrevealer
        public PhospherState State => state;
        public Vector2 Velocity => velocity;
        public PhospherInventory Inventory => inventory;


        public void Init(PhospherManager mgr, Transform anchor, IVectorField field, PhospherSettings s)
        {
            _mgr = mgr;
            _anchor = anchor;
            _field = field;
            settings = s;
            lifetimeRemaining = s.PickLifetime();
            velocity = Random.insideUnitCircle.normalized * (0.5f * Mathf.Min(1f, s.maxSpeed));

            var a = anchor != null ? anchor.GetComponent<Phosphers.World.Anchor>() : null;
            _anchorOuterRadius = a != null ? a.OuterRadius : s.depositRadius;
            if (perception != null) perception.BindInventory(inventory);

        }


        private void Update()
        {
            if (enableDevHotkeys && Input.GetKeyDown(KeyCode.T))
                ToggleReturn();

            TickLifetime(Time.deltaTime);

            // --- Perception-driven transitions (no pickup yet) ---
            if (state != PhospherState.Recover && state != PhospherState.Return)
            {
                var seen = perception != null ? perception.Nearest : null;

                // If we can take a bit and see one > SEEK that bit
                if (state == PhospherState.Forage && !inventory.IsFullFor(BitSpec.Generic) && seen != null)
                {
                    _seekTarget = seen;
                    state = PhospherState.Seek;
                    OnBitSeen?.Invoke(_seekTarget);
                }

                // If in SEEK but target lost/unavailable > back to FORAGE
                if (state == PhospherState.Seek)
                {
                    bool lost = _seekTarget == null || !_seekTarget.IsAvailable;
                    if (lost)
                    {
                        _seekTarget = null;
                        state = PhospherState.Forage;
                    }
                }
            }

            StepKinematics(Time.deltaTime);
            debugStateName = state.ToString();
        }


        private void TickLifetime(float dt)
        {
            lifetimeRemaining -= dt;
            if (lifetimeRemaining <= 0f) DestroySelf();
        }

        private void StepKinematics(float dt)
        {
            // --- Early inside-anchor detection to enter Recover ---
            if (_anchor != null && state != PhospherState.Recover)
            {
                float distToAnchor = ((Vector2)transform.position - (Vector2)_anchor.position).magnitude;
                if (distToAnchor < _anchorOuterRadius - settings.recoverInsideSlack)
                {
                    EnterRecover();                      // NEW
                }
            }

            // --- Run the failsafe and bail for this frame ---
            if (state == PhospherState.Recover)          // NEW
            {
                StepRecover(dt);                         // NEW
                return;
            }

            // Decide whether to ignore flocking/signals temporarily (post-recover)
            bool suppressSteering = _suppressSteeringTimer > 0f;   // NEW

            // -- Gather neighbours
            var neigh = suppressSteering ? null : _mgr.GetNeighbours(this, settings.neighbourRadius); // NEW: skip query if suppressing

            // -- Steering terms
            Vector2 accel = Vector2.zero;

            // (new) reduce flocking when seeking
            float flockScale = (state == PhospherState.Seek) ? settings.seekFlockDampen : 1f;

            // Separation
            if (!suppressSteering && neigh != null && neigh.Count > 0)
            {
                Vector2 sep = Vector2.zero;
                float sepR2 = settings.separationRadius * settings.separationRadius;
                foreach (var other in neigh)
                {
                    Vector2 d = (Vector2)transform.position - (Vector2)other.transform.position;
                    float d2 = d.sqrMagnitude;
                    if (d2 < 0.0001f) continue;
                    if (d2 <= sepR2) sep += d / d2;
                }
                accel += sep * settings.weightSeparation;
            }

            // Cohesion
            if (!suppressSteering && neigh != null && neigh.Count > 0)
            {
                Vector2 centroid = Vector2.zero;
                foreach (var other in neigh) centroid += (Vector2)other.transform.position;
                centroid /= neigh.Count;
                Vector2 desiredVel = ((centroid - (Vector2)transform.position).normalized) * settings.maxSpeed;
                Vector2 steer = desiredVel - velocity;
                accel += steer * settings.weightCohesion * flockScale;
            }

            // Alignment
            if (!suppressSteering && neigh != null && neigh.Count > 0)
            {
                Vector2 avgVel = Vector2.zero;
                foreach (var other in neigh) avgVel += other.velocity;
                avgVel /= neigh.Count;
                if (avgVel.sqrMagnitude > 1e-6f)
                {
                    Vector2 desiredVel = avgVel.normalized * settings.maxSpeed;
                    Vector2 steer = desiredVel - velocity;
                    accel += steer * settings.weightAlignment * flockScale;
                }
            }

            // Signal / external field
            if (!suppressSteering && _field != null)
            {
                Vector2 f = _field.Sample(transform.position);
                accel += f * settings.weightSignal;
            }

            // Return seek (still active even if suppressing flocking)
            if (state == PhospherState.Return && _anchor != null)
            {
                Vector2 toAnchor = (Vector2)_anchor.position - (Vector2)transform.position;
                float dist = toAnchor.magnitude;
                if (dist > 1e-5f)
                {
                    Vector2 desiredVel = (toAnchor / dist) * settings.maxSpeed;
                    Vector2 steer = desiredVel - velocity;
                    accel += steer * settings.weightReturn;
                }
            }

            // SEEK: steer hard toward the current target Bit (if any)
            if (state == PhospherState.Seek && _seekTarget != null && _seekTarget.IsAvailable)
            {
                Vector2 to = _seekTarget.Position - (Vector2)transform.position;
                float d = to.magnitude;
                if (d > 1e-4f)
                {
                    Vector2 desiredVel = (to / d) * settings.maxSpeed;
                    Vector2 steer = desiredVel - velocity;
                    accel += steer * settings.seekSteerWeight;
                }
            }

            // Obstacle steering (FORAGE boundary layer) still applies even if suppressing,
            // so a freshly recovered agent doesn't immediately re-enter.
            if (state == PhospherState.Forage && _anchor != null)
            {
                var obs = new Phosphers.Navigation.CircleObstacle((Vector2)_anchor.position, _anchorOuterRadius);
                Vector2 aObs = Phosphers.Navigation.ObstacleSteering2D.BoundaryLayerSteering(
                    obs, transform.position, velocity,
                    settings.anchorAvoidBand,
                    settings.anchorAvoidNormalK,
                    settings.anchorAvoidTangentK
                );
                accel += aObs;
            }

            // Noise (small wander) – keep it even when suppressing, it's harmless
            if (settings.weightNoise > 0f && settings.noiseJitterPerSec > 0f)
            {
                Vector2 jitter = Random.insideUnitCircle * settings.noiseJitterPerSec;
                accel += jitter * settings.weightNoise;
            }

            // -- Caps: accel, turn, speed
            float aMag = accel.magnitude;
            if (aMag > settings.maxAccel) accel *= (settings.maxAccel / aMag);

            Vector2 newVel = velocity + accel * dt;

            if (velocity.sqrMagnitude > 1e-6f && newVel.sqrMagnitude > 1e-6f)
            {
                float maxTurnRad = settings.maxTurnRateDeg * Mathf.Deg2Rad * dt;
                float ang = Vector2.SignedAngle(velocity, newVel) * Mathf.Deg2Rad;
                ang = Mathf.Clamp(ang, -maxTurnRad, maxTurnRad);
                float speed = newVel.magnitude;
                float s = Mathf.Sin(ang), c = Mathf.Cos(ang);
                Vector2 v = velocity;
                newVel = new Vector2(v.x * c - v.y * s, v.x * s + v.y * c).normalized * speed;
            }

            float vMag = newVel.magnitude;
            if (vMag > settings.maxSpeed) newVel *= (settings.maxSpeed / vMag);

            // --- Continuous collision (unchanged from your latest version) ---
            Vector2 prevPos = transform.position;
            Vector2 nextPos = prevPos + newVel * dt;
            bool handled = false;

            // --- before this block you already have: prevPos, newVel, nextPos, handled=false

            if (state == PhospherState.Return && _anchor != null)
            {
                // Two radii:
                // rDeposit: the true rim where a deposit should occur
                // rSnap:    slightly outside to keep motion from penetrating
                float rDeposit = _anchorOuterRadius; // TRUE deposit rim
                float rSnap = _anchorOuterRadius + settings.anchorEdgeSnapMargin;

                // 1) Prefer to test deposit rim first. If we cross it this frame, we must deposit.
                if (SegmentCircleHit(prevPos, nextPos, (Vector2)_anchor.position, rDeposit,
                                     out float tHitDep, out Vector2 hitDep))
                {
                    // Snap to the rim + a small epsilon outward so we don't end exactly inside
                    Vector2 n = (hitDep - (Vector2)_anchor.position);
                    float d = n.magnitude;
                    n = (d > 1e-6f) ? (n / d) : Vector2.right;

                    Vector2 posOnRim = (Vector2)_anchor.position + n * (rDeposit + settings.anchorCollisionEpsilon);
                    float dtLeft = Mathf.Max(0f, dt * (1f - Mathf.Clamp01(tHitDep)));

                    // DEPOSIT once, if carrying
                    bool deposited = TryDepositAtAnchor();

                    // Reverse velocity with jitter and continue motion for leftover dt
                    Vector2 vInDir = newVel.sqrMagnitude > 1e-8f ? newVel.normalized : -n;
                    float j = settings.anchorExitJitterDeg * Mathf.Deg2Rad;
                    float ang = Random.Range(-j, j);
                    float sj = Mathf.Sin(ang), cj = Mathf.Cos(ang);
                    Vector2 vOutDir = new Vector2(-vInDir.x * cj - vInDir.y * sj,
                                                   -vInDir.x * sj + vInDir.y * cj);
                    float speedOut = Mathf.Min(settings.maxSpeed, newVel.magnitude) * settings.anchorExitSpeedFactor;
                    Vector2 vOut = vOutDir.normalized * Mathf.Max(0.01f, speedOut);

                    Vector2 finalPos = posOnRim + vOut * dtLeft;

                    transform.position = finalPos;
                    velocity = vOut;

                    // After a successful deposit, we leave RETURN and go back to Forage
                    if (deposited)
                    {
                        state = PhospherState.Forage;
                        _suppressSteeringTimer = Mathf.Max(_suppressSteeringTimer, 0.15f); // tiny cool-off
                    }

                    handled = true;
                }
                // 2) Otherwise, if we didn't cross the deposit rim, still prevent penetration with the snap rim.
                else if (SegmentCircleHit(prevPos, nextPos, (Vector2)_anchor.position, rSnap,
                                          out float tHitSnap, out Vector2 hitSnap))
                {
                    Vector2 n = (hitSnap - (Vector2)_anchor.position);
                    float d = n.magnitude;
                    n = (d > 1e-6f) ? (n / d) : Vector2.right;

                    Vector2 posOnRim = (Vector2)_anchor.position + n * (rSnap + settings.anchorCollisionEpsilon);
                    float dtLeft = Mathf.Max(0f, dt * (1f - Mathf.Clamp01(tHitSnap)));

                    // Safety: if we're carrying and are already within rSnap, allow deposit anyway.
                    // (Covers glancing hits where snap radius is crossed but deposit rim was grazed numerically.)
                    TryDepositAtAnchor();

                    Vector2 vInDir = newVel.sqrMagnitude > 1e-8f ? newVel.normalized : -n;
                    float j = settings.anchorExitJitterDeg * Mathf.Deg2Rad;
                    float ang = Random.Range(-j, j);
                    float sj = Mathf.Sin(ang), cj = Mathf.Cos(ang);
                    Vector2 vOutDir = new Vector2(-vInDir.x * cj - vInDir.y * sj,
                                                   -vInDir.x * sj + vInDir.y * cj);
                    float speedOut = Mathf.Min(settings.maxSpeed, newVel.magnitude) * settings.anchorExitSpeedFactor;
                    Vector2 vOut = vOutDir.normalized * Mathf.Max(0.01f, speedOut);

                    Vector2 finalPos = posOnRim + vOut * dtLeft;

                    transform.position = finalPos;
                    velocity = vOut;

                    // Only return to Forage if a deposit actually occurred (inventory now empty)
                    if (inventory.IsEmpty)
                        state = PhospherState.Forage;

                    handled = true;
                }
            }


            // FORAGE: slide along rim (your existing block)
            if (!handled && state == PhospherState.Forage && _anchor != null)
            {
                float rim = _anchorOuterRadius;
                if (SegmentCircleHit(prevPos, nextPos, (Vector2)_anchor.position, rim, out float tHit, out Vector2 hit))
                {
                    Vector2 n = (hit - (Vector2)_anchor.position).normalized; // outward
                    Vector2 t = new Vector2(-n.y, n.x);
                    float sign = Mathf.Sign(Vector2.Dot(t, newVel));
                    if (Mathf.Approximately(sign, 0f)) sign = 1f;
                    Vector2 tangentDir = (t * sign).normalized;

                    float speedOut = newVel.magnitude * settings.forageSlideSpeedFactor;
                    Vector2 posOnRim = (Vector2)_anchor.position + n * (rim + settings.forageCollisionEpsilon);
                    float dtLeft = Mathf.Max(0f, dt * (1f - Mathf.Clamp01(tHit)));
                    Vector2 vOut = tangentDir * Mathf.Max(0.01f, speedOut);
                    Vector2 finalPos = posOnRim + vOut * dtLeft;

                    transform.position = finalPos;
                    velocity = vOut;
                }
            }

            // --- Safety: if still RETURN and ended up at/beyond the deposit rim, deposit now.
            if (!handled && state == PhospherState.Return && _anchor != null)
            {
                float dist = ((Vector2)transform.position - (Vector2)_anchor.position).magnitude;
                if (dist <= _anchorOuterRadius + settings.anchorCollisionEpsilon)
                {
                    if (TryDepositAtAnchor())
                        state = PhospherState.Forage;
                }
            }

            if (!handled)
            {
                transform.position = nextPos;
                velocity = newVel;
            }

            if (velocity.sqrMagnitude > 1e-6f)
            {
                float z = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg + settings.spriteForwardOffsetDeg;
                transform.rotation = Quaternion.Euler(0, 0, z);
            }
        }


        public void ToggleReturn()
        {
            state = state == PhospherState.Forage ? PhospherState.Return : PhospherState.Forage;
        }

        public void DestroySelf()
        {
            _mgr?.Unregister(this);
            Destroy(gameObject);
        }

        private static bool SegmentCircleHit(Vector2 p0, Vector2 p1, Vector2 c, float r, out float tHit, out Vector2 hit)
        {
            // Solve |p0 + t*(p1-p0) - c|^2 = r^2 for t in [0,1]
            Vector2 d = p1 - p0;
            Vector2 f = p0 - c;
            float a = Vector2.Dot(d, d);
            float b = 2f * Vector2.Dot(f, d);
            float cTerm = Vector2.Dot(f, f) - r * r;

            float disc = b * b - 4f * a * cTerm;
            if (disc < 0f || a < 1e-12f)
            {
                tHit = 0f; hit = default; return false;
            }

            float s = Mathf.Sqrt(disc);
            float t1 = (-b - s) / (2f * a);
            float t2 = (-b + s) / (2f * a);

            // We want the earliest intersection along the segment
            bool t1ok = t1 >= 0f && t1 <= 1f;
            bool t2ok = t2 >= 0f && t2 <= 1f;

            if (t1ok && t2ok) tHit = Mathf.Min(t1, t2);
            else if (t1ok) tHit = t1;
            else if (t2ok) tHit = t2;
            else { tHit = 0f; hit = default; return false; }

            hit = p0 + d * tHit;
            return true;
        }

        // NEW: enter the failsafe
        private void EnterRecover()
        {
            _resumeState = state;
            state = PhospherState.Recover;
            _recoverTimer = 0f;
        }

        // NEW: push outward to just outside the rim, ignore flocking, then resume
        private void StepRecover(float dt)
        {
            if (_anchor == null) { state = _resumeState; return; }

            Vector2 pos = transform.position;
            Vector2 c = _anchor.position;
            Vector2 n = pos - c;
            float d = n.magnitude;
            if (d < 1e-6f) n = Vector2.right; else n /= d;

            float targetR = _anchorOuterRadius + settings.recoverExitBuffer;

            // Move outward by at most 'recoverStepOutSpeed * dt' toward the target radius
            float moveOut = Mathf.Max(0f, targetR - d);
            float step = settings.recoverStepOutSpeed * dt;
            Vector2 delta = n * Mathf.Min(moveOut, step);
            Vector2 newPos = pos + delta;

            // Give outward velocity with a touch of jitter
            float j = settings.recoverJitterDeg * Mathf.Deg2Rad;
            float ang = Random.Range(-j, j);
            float s = Mathf.Sin(ang), ccos = Mathf.Cos(ang);
            Vector2 dir = new Vector2(n.x * ccos - n.y * s, n.x * s + n.y * ccos);
            float baseSpeed = Mathf.Max(velocity.magnitude, settings.maxSpeed * 0.6f);
            velocity = dir.normalized * Mathf.Min(settings.maxSpeed, baseSpeed);

            transform.position = newPos;

            _recoverTimer += dt;
            bool outside = (((Vector2)newPos - (Vector2)_anchor.position).magnitude >= targetR - 1e-4f);
            if (outside || _recoverTimer >= 0.25f)
            {
                state = _resumeState;                               // go back to what we were doing
                _suppressSteeringTimer = settings.recoverHoldSeconds; // but ignore flocking briefly
            }

            // Face velocity + sprite offset
            if (velocity.sqrMagnitude > 1e-6f)
            {
                float z = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg + settings.spriteForwardOffsetDeg;
                transform.rotation = Quaternion.Euler(0, 0, z);
            }
        }

        public bool TryPickup(BitSpec spec)
        {
            if (!inventory.CanAccept(spec)) return false;

            inventory.Add(spec);
            // Clear seek target & flip to Return
            _seekTarget = null;
            state = PhospherState.Return;
            OnBitPickedUp?.Invoke(spec);
            return true;
        }

        // --- Deposit helper: performs one deposit if carrying; returns true if deposited.
        private bool TryDepositAtAnchor()
        {
            if (_anchor == null) return false;
            if (!inventory.TryTakeOne(out var spec))
                return false;

            var anchorComp = _anchor.GetComponent<Phosphers.World.Anchor>();
            Phosphers.Core.ResourceSystem.Instance?.OnDeposit(anchorComp, this, spec);

            OnBitDeposited?.Invoke(spec); // <- add this
            return true;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.DrawWireSphere(transform.position, settings != null ? settings.depositRadius : 0.5f);
        }
    }
}
