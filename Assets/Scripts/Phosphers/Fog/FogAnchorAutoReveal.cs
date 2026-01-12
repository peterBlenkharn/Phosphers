using System.Collections;
using UnityEngine;
using Phosphers.Fog;
using Phosphers.World;

namespace Phosphers.Fog
{
    /// Attach to the Anchor prefab to auto-reveal a halo when a run starts.
    public class FogAnchorAutoReveal : MonoBehaviour
    {
        [Tooltip("Reveal radius = OuterRadius * (1 + marginPercent). 0.10 = +10%.")]
        [SerializeField, Range(0f, 1f)] private float marginPercent = 0.10f;
        [Tooltip("Optional extra world-units added to the computed radius.")]
        [SerializeField] private float extraMarginUnits = 0f;

        [Tooltip("Delay one frame so FogSystem can reset on Run start.")]
        [SerializeField] private bool waitOneFrame = true;

        private void OnEnable()
        {
            if (waitOneFrame) StartCoroutine(RevealNextFrame());
            else DoReveal();
        }

        private IEnumerator RevealNextFrame()
        {
            yield return null; // ensure FogSystem reset (Run) has happened
            DoReveal();
        }

        private void DoReveal()
        {
            var fog = FogSystem.Instance;
            if (fog == null) return;

            var anchor = GetComponent<Anchor>();
            float baseR = anchor != null ? anchor.OuterRadius : 1f;
            float r = baseR * (1f + Mathf.Max(0f, marginPercent)) + Mathf.Max(0f, extraMarginUnits);

            fog.Reveal(transform.position, r, 1f); // one-shot, full intensity
        }
    }
}
