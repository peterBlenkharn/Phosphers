using UnityEngine;
using UnityEngine.UI;
using TMPro;                    // <-- always include TMP
using Phosphers.Core;

namespace Phosphers.UI
{
    public class RunHUD : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private GameManager gameManager;   // only for OnStateChanged
        [SerializeField] private CanvasGroup canvasGroup;

        // You can assign either TMP_Text OR legacy Text. The code will use whichever is set.
        [Header("Score UI (assign one)")]
        [SerializeField] private TMP_Text scoreTMP;
        [SerializeField] private Text scoreUGUI;

        [Header("Timer UI (assign one)")]
        [SerializeField] private TMP_Text timerTMP;
        [SerializeField] private Text timerUGUI;

        [Header("Juice UI (assign one)")]
        [SerializeField] private TMP_Text juiceTMP;
        [SerializeField] private Text juiceUGUI;

        [Header("Format")]
        [SerializeField] private string scoreFormat = "Score: {0}";
        [SerializeField] private string timerFormat = "{0:00}:{1:00}";
        [SerializeField] private string juiceFormat = "Juice: {0:0}";

        [Header("Behaviour")]
        [Tooltip("HUD stays hidden until a Run state change is raised.")]
        [SerializeField] private bool hideUntilRunEvent = true;

        private ResourceSystem _rs;
        private RunStats _stats;
        private TrailJuiceSystem _juice;
        private bool _visible;

        private void Reset()
        {
#if UNITY_2023_1_OR_NEWER
            gameManager = FindFirstObjectByType<GameManager>();
#else
            gameManager = FindObjectOfType<GameManager>();
#endif
            canvasGroup = GetComponentInParent<CanvasGroup>();
        }

        private void OnEnable()
        {
            _rs = ResourceSystem.Instance;
            _stats = RunStats.Instance;
            _juice = TrailJuiceSystem.Instance;

            if (gameManager == null)
            {
#if UNITY_2023_1_OR_NEWER
                gameManager = FindFirstObjectByType<GameManager>();
#else
                gameManager = FindObjectOfType<GameManager>();
#endif
            }
            if (gameManager != null) gameManager.OnStateChanged += HandleState;
            if (_rs != null) _rs.OnScoreChanged += HandleScoreChanged;
            if (_juice != null) _juice.OnJuiceChanged += HandleJuiceChanged;

            // Initial text
            UpdateScoreText(_rs != null ? _rs.Score : 0);
            UpdateTimerText(0f);
            UpdateJuiceText(_juice != null ? _juice.CurrentJuice : 0f);

            // Initial visibility
            ApplyVisibility(!hideUntilRunEvent);
        }

        private void OnDisable()
        {
            if (gameManager != null) gameManager.OnStateChanged -= HandleState;
            if (_rs != null) _rs.OnScoreChanged -= HandleScoreChanged;
            if (_juice != null) _juice.OnJuiceChanged -= HandleJuiceChanged;
        }

        private void HandleState(GameState s)
        {
            if (s == GameState.Run)
            {
                ApplyVisibility(true);
                UpdateScoreText(_rs != null ? _rs.Score : 0);
                UpdateTimerText(0f);
                UpdateJuiceText(_juice != null ? _juice.CurrentJuice : 0f);
            }
            else if (s == GameState.Menu || s == GameState.End)
            {
                ApplyVisibility(false);
            }
        }

        private void ApplyVisibility(bool show)
        {
            _visible = show;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = show ? 1f : 0f;
                canvasGroup.interactable = show;
                canvasGroup.blocksRaycasts = show;
            }
            else
            {
                gameObject.SetActive(show);
            }
        }

        private void HandleScoreChanged(int newScore) => UpdateScoreText(newScore);
        private void HandleJuiceChanged(float current, float max) => UpdateJuiceText(current);

        private void Update()
        {
            if (!_visible) return;
            if (_stats == null) _stats = RunStats.Instance;

            float t = (_stats != null) ? _stats.RunElapsed : 0f;
            UpdateTimerText(t);
        }

        private void UpdateScoreText(int score)
        {
            string s = string.Format(scoreFormat, score);
            if (scoreTMP != null) scoreTMP.text = s;
            else if (scoreUGUI != null) scoreUGUI.text = s;
        }

        private void UpdateTimerText(float seconds)
        {
            int m = Mathf.FloorToInt(seconds / 60f);
            int s = Mathf.FloorToInt(seconds % 60f);
            string t = string.Format(timerFormat, m, s);

            if (timerTMP != null) timerTMP.text = t;
            else if (timerUGUI != null) timerUGUI.text = t;
        }

        private void UpdateJuiceText(float value)
        {
            string text = string.Format(juiceFormat, value);
            if (juiceTMP != null) juiceTMP.text = text;
            else if (juiceUGUI != null) juiceUGUI.text = text;
        }
    }
}
