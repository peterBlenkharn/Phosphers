using UnityEngine;

namespace Phosphers.Signals
{
    public class TrailField : MonoBehaviour, IVectorField
    {
        [Header("Bounds")]
        [SerializeField] private Vector2 worldMin = new(-20f, -20f);
        [SerializeField] private Vector2 worldMax = new(20f, 20f);
        [SerializeField] private bool fitToCameraView = false;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private bool fitRendererToBounds = true;

        [Header("Resolution")]
        [SerializeField] private bool autoResolution = true;
        [SerializeField, Min(1)] private int targetPixelsPerTexel = 3;
        [SerializeField, Min(8)] private int minTexDim = 64;
        [SerializeField, Min(8)] private int maxTexDim = 512;
        [SerializeField, Min(1)] private int roundToMultiple = 4;
        [SerializeField] private FilterMode filterMode = FilterMode.Bilinear;
        [SerializeField, Min(0.001f)] private float referenceOrthoSize = 12f;

        [Header("Trail")]
        [SerializeField] private float decayPerSecond = 0.2f;
        [SerializeField, Min(0f)] private float minIntensity = 0.01f;
        [SerializeField, Min(0.01f)] private float applyInterval = 0.05f;

        [Header("Sampling")]
        [SerializeField] private float gradientStrength = 1f;
        [SerializeField, Range(0f, 1f)] private float intensityThreshold = 0.02f;
        [SerializeField] private bool scaleByIntensity = true;

        [Header("Renderer Hookup")]
        [SerializeField] private SpriteRenderer trailRenderer;
        [SerializeField] private Material trailMaterialTemplate;

        private Texture2D _trailTex;
        private float[] _trail;
        private byte[] _trailBytes;
        private Material _runtimeMat;

        private float _cellX;
        private float _cellY;
        private Vector2 _min;
        private Vector2 _max;
        private int _w;
        private int _h;

        private float _applyTimer;
        private bool _dirty;

        private void Awake()
        {
            Init();
        }

        private void Init()
        {
            Camera cam = null;
            if (fitToCameraView)
                cam = targetCamera != null ? targetCamera : Camera.main;

            if (cam != null && cam.orthographic)
            {
                var p = cam.transform.position;
                float halfH = cam.orthographicSize;
                float halfW = halfH * cam.aspect;
                _min = new Vector2(p.x - halfW, p.y - halfH);
                _max = new Vector2(p.x + halfW, p.y + halfH);
            }
            else
            {
                _min = worldMin;
                _max = worldMax;
            }

            ComputeTextureDims();

            Vector2 size = _max - _min;
            _cellX = size.x / _w;
            _cellY = size.y / _h;

            int len = _w * _h;
            _trail = new float[len];
            _trailBytes = new byte[len];

            _trailTex = new Texture2D(_w, _h, TextureFormat.Alpha8, false, true)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = filterMode,
                name = "TrailMask"
            };
#if UNITY_2020_1_OR_NEWER
            _trailTex.SetPixelData(_trailBytes, 0);
#else
            var cols = new Color32[len];
            for (int i = 0; i < len; i++) cols[i].a = 0;
            _trailTex.SetPixels32(cols);
#endif
            _trailTex.Apply(false, false);

            if (trailRenderer != null)
            {
                if (trailMaterialTemplate != null)
                {
                    _runtimeMat = new Material(trailMaterialTemplate);
                    _runtimeMat.name = trailMaterialTemplate.name + " (Instance)";
                    _runtimeMat.SetTexture("_MainTex", _trailTex);
                    trailRenderer.sharedMaterial = _runtimeMat;
                }

                var sprite = Sprite.Create(_trailTex, new Rect(0, 0, _w, _h),
                    new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);
                trailRenderer.sprite = sprite;

                if (fitRendererToBounds)
                {
                    var t = trailRenderer.transform;
                    t.position = (Vector3)((_min + _max) * 0.5f);
                    Vector2 spriteWorldSize = trailRenderer.sprite.bounds.size;
                    if (spriteWorldSize.x <= 0f) spriteWorldSize.x = 1f;
                    if (spriteWorldSize.y <= 0f) spriteWorldSize.y = 1f;

                    t.localScale = new Vector3(
                        size.x / spriteWorldSize.x,
                        size.y / spriteWorldSize.y,
                        1f
                    );
                }
            }

            _applyTimer = 0f;
            _dirty = false;
        }

        public void Clear()
        {
            if (_trail == null) return;
            System.Array.Clear(_trail, 0, _trail.Length);
            System.Array.Clear(_trailBytes, 0, _trailBytes.Length);
#if UNITY_2020_1_OR_NEWER
            _trailTex.SetPixelData(_trailBytes, 0);
#else
            var cols = new Color32[_trailBytes.Length];
            for (int i = 0; i < cols.Length; i++) cols[i].a = 0;
            _trailTex.SetPixels32(cols);
#endif
            _trailTex.Apply(false, false);
            _dirty = false;
            _applyTimer = 0f;
        }

        public void Paint(Vector2 worldPos, float radius, float strength)
        {
            if (_trail == null) return;
            float r = Mathf.Max(0.0001f, radius);
            int minX = Mathf.FloorToInt((worldPos.x - r - _min.x) / _cellX);
            int minY = Mathf.FloorToInt((worldPos.y - r - _min.y) / _cellY);
            int maxX = Mathf.CeilToInt((worldPos.x + r - _min.x) / _cellX);
            int maxY = Mathf.CeilToInt((worldPos.y + r - _min.y) / _cellY);

            minX = Mathf.Clamp(minX, 0, _w - 1);
            minY = Mathf.Clamp(minY, 0, _h - 1);
            maxX = Mathf.Clamp(maxX, 0, _w - 1);
            maxY = Mathf.Clamp(maxY, 0, _h - 1);

            float rInv = 1f / r;
            float paintStrength = Mathf.Clamp01(strength);

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
                        float t = 1f - (d * rInv);
                        float w = Mathf.SmoothStep(0f, 1f, t);
                        int i = y * _w + x;
                        float next = Mathf.Clamp01(_trail[i] + (w * paintStrength));
                        _trail[i] = next;
                    }
                }
            }

            _dirty = true;
        }

        public Vector2 Sample(Vector2 worldPos)
        {
            if (_trail == null) return Vector2.zero;
            int x = Mathf.FloorToInt((worldPos.x - _min.x) / _cellX);
            int y = Mathf.FloorToInt((worldPos.y - _min.y) / _cellY);
            if (x < 0 || x >= _w || y < 0 || y >= _h) return Vector2.zero;

            float center = _trail[y * _w + x];
            if (center < intensityThreshold) return Vector2.zero;

            float left = GetCell(x - 1, y);
            float right = GetCell(x + 1, y);
            float down = GetCell(x, y - 1);
            float up = GetCell(x, y + 1);

            Vector2 grad = new Vector2(right - left, up - down);
            float mag = grad.magnitude;
            if (mag < 1e-5f) return Vector2.zero;

            float strength = gradientStrength * (scaleByIntensity ? center : 1f);
            return (grad / mag) * strength;
        }

        private void Update()
        {
            if (_trail == null) return;

            if (decayPerSecond > 0f)
            {
                float decay = decayPerSecond * Time.deltaTime;
                if (decay > 0f)
                {
                    bool changed = false;
                    for (int i = 0; i < _trail.Length; i++)
                    {
                        float v = _trail[i];
                        if (v <= 0f) continue;
                        float next = Mathf.Max(0f, v - decay);
                        if (!Mathf.Approximately(v, next))
                        {
                            _trail[i] = next;
                            if (next <= minIntensity) _trail[i] = 0f;
                            changed = true;
                        }
                    }

                    if (changed) _dirty = true;
                }
            }

            if (_dirty)
            {
                _applyTimer += Time.deltaTime;
                if (_applyTimer >= applyInterval)
                {
                    for (int i = 0; i < _trail.Length; i++)
                        _trailBytes[i] = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(_trail[i]));

#if UNITY_2020_1_OR_NEWER
                    _trailTex.SetPixelData(_trailBytes, 0);
#else
                    var cols = new Color32[_trailBytes.Length];
                    for (int i = 0; i < _trailBytes.Length; i++) cols[i].a = _trailBytes[i];
                    _trailTex.SetPixels32(cols);
#endif
                    _trailTex.Apply(false, false);
                    _dirty = false;
                    _applyTimer = 0f;
                }
            }
        }

        private void ComputeTextureDims()
        {
            if (!autoResolution)
            {
                _w = Mathf.Clamp(minTexDim, 8, maxTexDim);
                _h = Mathf.Clamp(minTexDim, 8, maxTexDim);
                return;
            }

            float ortho = Mathf.Max(0.0001f, referenceOrthoSize);
            float pxPerWorld = Screen.height / (2f * ortho);
            float worldTexel = Mathf.Max(0.0001f, (float)targetPixelsPerTexel / pxPerWorld);

            Vector2 size = _max - _min;
            int w = Mathf.CeilToInt(size.x / worldTexel);
            int h = Mathf.CeilToInt(size.y / worldTexel);

            w = Mathf.Clamp(w, minTexDim, maxTexDim);
            h = Mathf.Clamp(h, minTexDim, maxTexDim);
            int m = Mathf.Max(1, roundToMultiple);
            w = (w + m - 1) / m * m;
            h = (h + m - 1) / m * m;

            _w = Mathf.Max(8, w);
            _h = Mathf.Max(8, h);
        }

        private float GetCell(int x, int y)
        {
            x = Mathf.Clamp(x, 0, _w - 1);
            y = Mathf.Clamp(y, 0, _h - 1);
            return _trail[y * _w + x];
        }
    }
}
