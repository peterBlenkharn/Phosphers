using UnityEngine;

namespace Phosphers.Fog
{
    [CreateAssetMenu(menuName = "Phosphers/Fog Settings")]
    public class FogSettings : ScriptableObject
    {
        [Header("World Anchoring")]
        [Tooltip("Fog bounds in world units. Keep this >= the area any camera zoom can see.")]
        public Vector2 worldMin = new Vector2(-20f, -12f);
        public Vector2 worldMax = new Vector2(20f, 12f);

        [Header("Mask Resolution (manual fallback)")]
        public int texWidth = 512;   // acts as a fallback if auto-res is off
        public int texHeight = 512;

        [Header("Auto Resolution (recommended)")]
        [Tooltip("If on, FogSystem computes texture size from your max zoom-in and a pixels-per-texel target.")]
        public bool autoResolution = true;
        [Tooltip("Set this to your SMALLEST orthographic size (max zoom-in).")]
        public float referenceOrthoSize = 6f;  // e.g. your zoom step for “closest in”
        [Tooltip("Desired on-screen pixels per fog texel at max zoom-in (2–3 is a good target).")]
        public int targetPixelsPerTexel = 2;
        [Tooltip("Clamp auto resolution between these limits.")]
        public int minTexDim = 256;
        public int maxTexDim = 1024;
        [Tooltip("Round auto dims to a multiple of this (helps GPU alignment).")]
        public int roundToMultiple = 8;

        [Header("Reveal Defaults")]
        public float defaultRevealRadius = 1.2f;
        [Range(0f, 1f)] public float feather = 0.6f;

        [Header("Updates & Quality")]
        public float applyInterval = 0.05f; // 20 Hz
        public FilterMode filterMode = FilterMode.Bilinear;

        [Header("Lifecycle")]
        public bool clearOnRunStart = true;

        public Vector2 Size => worldMax - worldMin;

    }
}
