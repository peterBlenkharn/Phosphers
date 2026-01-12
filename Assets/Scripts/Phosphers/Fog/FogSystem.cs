using UnityEngine;
using Phosphers.Core; // for GameManager
using System.Runtime.CompilerServices;

namespace Phosphers.Fog
{
    [DefaultExecutionOrder(-50)]
    public class FogSystem : MonoBehaviour
    {
        public static FogSystem Instance { get; private set; }

        [Header("Config")]
        [SerializeField] private FogSettings settings;

        [Header("Renderer hookup")]
        [SerializeField] private SpriteRenderer fogRenderer;           // SpriteRenderer showing the fog
        [SerializeField] private Material fogMaterialTemplate;         // Material using the Fog shader below

        [Header("Debug")]
        [SerializeField] private bool debugMousePaint = false;
        [SerializeField] private KeyCode debugHoldKey = KeyCode.Mouse0;
        [SerializeField] private float debugRevealRadiusOverride = -1f;

        [SerializeField] private bool fitToCameraView = false;     // keep OFF for world-anchored fog
        [SerializeField] private Camera targetCamera;               // optional; only used if you ever set fitToCameraView = true
        [SerializeField] private bool fitRendererToBounds = true;   // scale SpriteRenderer to world bounds

        // Runtime
        private Texture2D _maskTex;           // Alpha8 mask where A=seenAmount (0 unseen .. 1 seen)
        private byte[] _mask;                 // CPU buffer (0..255 seen)
        private float _cellX, _cellY;         // world-units per cell
        private Vector2 _min, _max;
        private int _w, _h;

        private float _applyTimer;
        private bool _dirty;
        private Material _runtimeMat;

        // Optional event hookup
#if UNITY_2023_1_OR_NEWER
        private GameManager _gm;
#endif

        public Texture MaskTexture => _maskTex;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Init();
        }

        private void OnEnable()
        {
#if UNITY_2023_1_OR_NEWER
            _gm = FindFirstObjectByType<GameManager>();
#else
            _gm = FindObjectOfType<GameManager>();
#endif
            if (_gm != null) _gm.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            if (_gm != null) _gm.OnStateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(GameState s)
        {
            if (s == GameState.Run && settings.clearOnRunStart)
                ResetFog();
        }

        private void Init()
        {
            // --- Basic validation ---
            if (settings == null)
            {
                Debug.LogError("[Fog] Missing FogSettings"); enabled = false; return;
            }
            if (fogRenderer == null)
            {
                Debug.LogError("[Fog] Missing SpriteRenderer reference"); enabled = false; return;
            }
            if (fogMaterialTemplate == null)
            {
                Debug.LogError("[Fog] Missing Fog material template"); enabled = false; return;
            }

            // --- Determine world bounds (WORLD-ANCHORED by default) ---
            Camera cam = null;
            if (fitToCameraView)
                cam = targetCamera != null ? targetCamera : Camera.main;

            if (cam != null && cam.orthographic)
            {
                // Optional dev mode: bind to camera view
                var p = cam.transform.position;
                float halfH = cam.orthographicSize;
                float halfW = halfH * cam.aspect;
                _min = new Vector2(p.x - halfW, p.y - halfH);
                _max = new Vector2(p.x + halfW, p.y + halfH);
            }
            else
            {
                // Production: fixed world bounds independent of zoom
                _min = settings.worldMin;
                _max = settings.worldMax;
            }

            // --- Pick texture resolution (auto or manual) ---
            ComputeTextureDims(); // sets _w and _h based on settings & screen

            Vector2 size = _max - _min;
            _cellX = size.x / _w;
            _cellY = size.y / _h;

            // --- Allocate CPU mask and GPU texture ---
            int len = _w * _h;
            _mask = new byte[len]; // start fully unseen (0)

            _maskTex = new Texture2D(_w, _h, TextureFormat.Alpha8, false, true)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = settings.filterMode,
                name = "FogMask"
            };

#if UNITY_2020_1_OR_NEWER
            _maskTex.SetPixelData(_mask, 0);   // zero-fill
#else
    var cols = new Color32[len];
    for (int i = 0; i < len; i++) cols[i].a = 0;
    _maskTex.SetPixels32(cols);
#endif
            _maskTex.Apply(false, false);

            // --- Instance a runtime material and bind the mask texture ---
            _runtimeMat = new Material(fogMaterialTemplate);
            _runtimeMat.name = fogMaterialTemplate.name + " (Instance)";
            _runtimeMat.SetTexture("_MaskTex", _maskTex);
            fogRenderer.sharedMaterial = _runtimeMat;

            // --- Fit the renderer to the world bounds (robust to any sprite size) ---
            if (fitRendererToBounds && fogRenderer.sprite != null)
            {
                var t = fogRenderer.transform;
                t.position = (Vector3)((_min + _max) * 0.5f);

                Vector2 spriteWorldSize = fogRenderer.sprite.bounds.size; // actual sprite size in world units
                if (spriteWorldSize.x <= 0f) spriteWorldSize.x = 1f;
                if (spriteWorldSize.y <= 0f) spriteWorldSize.y = 1f;

                t.localScale = new Vector3(
                    size.x / spriteWorldSize.x,
                    size.y / spriteWorldSize.y,
                    1f
                );
            }

            // --- Reset runtime flags ---
            _applyTimer = 0f;
            _dirty = false;
        }


        public void ResetFog()
        {
            System.Array.Clear(_mask, 0, _mask.Length);
#if UNITY_2020_1_OR_NEWER
            _maskTex.SetPixelData(_mask, 0);
#else
            var cols = new Color32[_mask.Length];
            for (int i = 0; i < cols.Length; i++) cols[i] = new Color32(0,0,0,0);
            _maskTex.SetPixels32(cols);
#endif
            _maskTex.Apply(false, false);
            _dirty = false;
            _applyTimer = 0f;
        }

        /// <summary>Reveal a soft circle at world position with given radius.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reveal(Vector2 worldPos, float radius)
        {
            // Clamp to bounds
            float r = Mathf.Max(0.0001f, radius);
            int minX = Mathf.FloorToInt((worldPos.x - r - _min.x) / _cellX);
            int minY = Mathf.FloorToInt((worldPos.y - r - _min.y) / _cellY);
            int maxX = Mathf.CeilToInt((worldPos.x + r - _min.x) / _cellX);
            int maxY = Mathf.CeilToInt((worldPos.y + r - _min.y) / _cellY);

            minX = Mathf.Clamp(minX, 0, _w - 1);
            minY = Mathf.Clamp(minY, 0, _h - 1);
            maxX = Mathf.Clamp(maxX, 0, _w - 1);
            maxY = Mathf.Clamp(maxY, 0, _h - 1);

            float feather = Mathf.Clamp01(settings.feather);
            float rInv = 1f / r;

            for (int y = minY; y <= maxY; y++)
            {
                float cy = _min.y + (y + 0.5f) * _cellY;
                for (int x = minX; x <= maxX; x++)
                {
                    float cx = _min.x + (x + 0.5f) * _cellX;
                    float dx = cx - worldPos.x;
                    float dy = cy - worldPos.y;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);

                    if (d <= r)
                    {
                        // Soft edge: smoothstep from center (1) to rim (0)
                        float t = 1f - (d * rInv); // 1 at center, 0 at rim
                        // Feathering (pow-like): lerp between hard and very soft
                        float w = Mathf.SmoothStep(0f, 1f, t);
                        w = Mathf.Lerp(w, t, 1f - feather);
                        byte val = (byte)Mathf.RoundToInt(255f * w);

                        int i = y * _w + x;
                        if (val > _mask[i]) _mask[i] = val; // monotonic increase
                    }
                }
            }
            _dirty = true;
        }

        // NEW: intensity in [0..1] multiplies reveal alpha (used by fade-in).
        public void Reveal(Vector2 worldPos, float radius, float intensity)
        {
            intensity = Mathf.Clamp01(intensity);
            float r = Mathf.Max(0.0001f, radius);

            int minX = Mathf.FloorToInt((worldPos.x - r - _min.x) / _cellX);
            int minY = Mathf.FloorToInt((worldPos.y - r - _min.y) / _cellY);
            int maxX = Mathf.CeilToInt((worldPos.x + r - _min.x) / _cellX);
            int maxY = Mathf.CeilToInt((worldPos.y + r - _min.y) / _cellY);

            minX = Mathf.Clamp(minX, 0, _w - 1);
            minY = Mathf.Clamp(minY, 0, _h - 1);
            maxX = Mathf.Clamp(maxX, 0, _w - 1);
            maxY = Mathf.Clamp(maxY, 0, _h - 1);

            float feather = Mathf.Clamp01(settings.feather);
            float rInv = 1f / r;

            for (int y = minY; y <= maxY; y++)
            {
                float cy = _min.y + (y + 0.5f) * _cellY;
                for (int x = minX; x <= maxX; x++)
                {
                    float cx = _min.x + (x + 0.5f) * _cellX;
                    float dx = cx - worldPos.x;
                    float dy = cy - worldPos.y;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);

                    if (d <= r)
                    {
                        // Base falloff  (center=1 to rim=0), softened by 'feather'
                        float t = 1f - (d * rInv);
                        float w = Mathf.SmoothStep(0f, 1f, t);
                        w = Mathf.Lerp(w, t, 1f - feather);

                        // Apply fade-in intensity
                        byte val = (byte)Mathf.RoundToInt(255f * (w * intensity));

                        int i = y * _w + x;
                        if (val > _mask[i]) _mask[i] = val; // still monotonic
                    }
                }
            }
            _dirty = true;
        }

        /// <summary>Has the cell containing this world position ever been revealed?</summary>
        public bool HasSeen(Vector2 worldPos)
        {
            int x = Mathf.FloorToInt((worldPos.x - _min.x) / _cellX);
            int y = Mathf.FloorToInt((worldPos.y - _min.y) / _cellY);
            if (x < 0 || x >= _w || y < 0 || y >= _h) return false;
            return _mask[y * _w + x] > 0;
        }

        private void Update()
        {
            // Debug mouse paint (hold-configurable key)
            if (debugMousePaint && Input.GetKey(debugHoldKey))
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    var m = cam.ScreenToWorldPoint(Input.mousePosition);
                    float rr = debugRevealRadiusOverride > 0f ? debugRevealRadiusOverride : settings.defaultRevealRadius;
                    Reveal(m, rr);
                }
            }

            // Throttled apply to GPU
            if (_dirty)
            {
                _applyTimer += Time.deltaTime;
                if (_applyTimer >= settings.applyInterval)
                {
#if UNITY_2020_1_OR_NEWER
                    _maskTex.SetPixelData(_mask, 0); // full upload (simple; subrect optimization can come later)
#else
                    var cols = new Color32[_mask.Length];
                    for (int i = 0; i < _mask.Length; i++) cols[i].a = _mask[i];
                    _maskTex.SetPixels32(cols);
#endif
                    _maskTex.Apply(false, false);
                    _dirty = false;
                    _applyTimer = 0f;
                }
            }
        }

        private void ComputeTextureDims()
        {
            if (settings.autoResolution)
            {
                // Pixels per world unit at MAX zoom-in (smallest ortho) using current screen height
                float ortho = Mathf.Max(0.0001f, settings.referenceOrthoSize);
                float pxPerWorld = Screen.height / (2f * ortho); // orthographic projection

                // World texel size in world units so that each texel around N pixels on screen
                float worldTexel = Mathf.Max(0.0001f, (float)settings.targetPixelsPerTexel / pxPerWorld);

                Vector2 size = settings.Size;
                int w = Mathf.CeilToInt(size.x / worldTexel);
                int h = Mathf.CeilToInt(size.y / worldTexel);

                // Clamp and round
                w = Mathf.Clamp(w, settings.minTexDim, settings.maxTexDim);
                h = Mathf.Clamp(h, settings.minTexDim, settings.maxTexDim);
                int m = Mathf.Max(1, settings.roundToMultiple);
                w = (w + m - 1) / m * m;
                h = (h + m - 1) / m * m;

                _w = Mathf.Max(8, w);
                _h = Mathf.Max(8, h);
            }
            else
            {
                _w = Mathf.Max(8, settings.texWidth);
                _h = Mathf.Max(8, settings.texHeight);
            }
        }


        private void OnDrawGizmosSelected()
        {
            if (settings == null) return;
            Gizmos.color = new Color(0f, 1f, 1f, 0.35f);
            Vector2 size = settings.Size;
            Vector3 c = (Vector3)((settings.worldMin + settings.worldMax) * 0.5f);
            Gizmos.DrawWireCube(c, new Vector3(size.x, size.y, 0f));
        }
    }
}
