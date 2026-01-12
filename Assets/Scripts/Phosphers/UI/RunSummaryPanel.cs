using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using Phosphers.Core;
using TMPro;

namespace Phosphers.UI
{
    public class RunSummaryPanel : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private GameManager gameManager;     // listens for OnStateChanged
        [SerializeField] private CanvasGroup canvasGroup;     // on the SummaryPanel

        // Assign either TMP or UGUI fields; leave the other null
        [Header("Texts (assign one per line)")]
        [SerializeField] private TMP_Text scoreTMP; [SerializeField] private Text scoreUGUI;
        [SerializeField] private TMP_Text timeTMP; [SerializeField] private Text timeUGUI;
        [SerializeField] private TMP_Text spawnedTMP; [SerializeField] private Text spawnedUGUI;
        [SerializeField] private TMP_Text pickedTMP; [SerializeField] private Text pickedUGUI;
        [SerializeField] private TMP_Text depositedTMP; [SerializeField] private Text depositedUGUI;
        [SerializeField] private TMP_Text srcDiscTMP; [SerializeField] private Text srcDiscUGUI;
        [SerializeField] private TMP_Text srcDeplTMP; [SerializeField] private Text srcDeplUGUI;

        [Header("Format")]
        [SerializeField] private string scoreFmt = "Score: {0}";
        [SerializeField] private string timeFmt = "Time: {0:00}:{1:00}";
        [SerializeField] private string spawnedFmt = "Bits Spawned: {0}";
        [SerializeField] private string pickedFmt = "Bits Picked Up: {0}";
        [SerializeField] private string depositedFmt = "Bits Deposited: {0}";
        [SerializeField] private string srcDiscFmt = "Sources Discovered: {0}";
        [SerializeField] private string srcDeplFmt = "Sources Depleted: {0}";

        [Header("Buttons (optional)")]
        public UnityEvent OnRestartPressed;    // wire to your GameManager StartRun or equivalent
        public UnityEvent OnMenuPressed;       // wire to your Menu action

        [Header("Appear")]
        [SerializeField] private float fadeTime = 0.18f;
        [SerializeField] private bool showInMenu = true;
        [SerializeField] private bool clearStatsOnMenu = true;

        private RunStats _stats;

        private void Reset()
        {
#if UNITY_2023_1_OR_NEWER
            gameManager = FindFirstObjectByType<GameManager>();
#else
            gameManager = FindObjectOfType<GameManager>();
#endif
            canvasGroup = GetComponent<CanvasGroup>();
        }

        private void OnEnable()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            HideImmediate();

            _stats = RunStats.Instance;

            if (gameManager == null)
            {
#if UNITY_2023_1_OR_NEWER
                gameManager = FindFirstObjectByType<GameManager>();
#else
                gameManager = FindObjectOfType<GameManager>();
#endif
            }
            if (gameManager != null) gameManager.OnStateChanged += HandleStateChanged;
            if (gameManager != null) HandleStateChanged(gameManager.CurrentState);
        }

        private void OnDisable()
        {
            if (gameManager != null) gameManager.OnStateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(GameState s)
        {
            if (s == GameState.End)
            {
                PopulateFromSnapshot();
                Show();
            }
            else if (s == GameState.Menu && showInMenu)
            {
                if (clearStatsOnMenu) PopulateMenuDefaults();
                Show();
            }
            else
            {
                Hide();
            }
        }

        private void PopulateFromSnapshot()
        {
            if (_stats == null) _stats = RunStats.Instance;
            var snap = (_stats != null) ? _stats.GetSnapshot() : default;

            SetText(scoreTMP, scoreUGUI, string.Format(scoreFmt, snap.score));
            int m = Mathf.FloorToInt(snap.elapsed / 60f);
            int sec = Mathf.FloorToInt(snap.elapsed % 60f);
            SetText(timeTMP, timeUGUI, string.Format(timeFmt, m, sec));
            SetText(spawnedTMP, spawnedUGUI, string.Format(spawnedFmt, snap.bitsSpawned));
            SetText(pickedTMP, pickedUGUI, string.Format(pickedFmt, snap.bitsPickedUp));
            SetText(depositedTMP, depositedUGUI, string.Format(depositedFmt, snap.bitsDeposited));
            SetText(srcDiscTMP, srcDiscUGUI, string.Format(srcDiscFmt, snap.sourcesDiscovered));
            SetText(srcDeplTMP, srcDeplUGUI, string.Format(srcDeplFmt, snap.sourcesDepleted));
        }

        private void PopulateMenuDefaults()
        {
            SetText(scoreTMP, scoreUGUI, string.Format(scoreFmt, 0));
            SetText(timeTMP, timeUGUI, string.Format(timeFmt, 0, 0));
            SetText(spawnedTMP, spawnedUGUI, string.Format(spawnedFmt, 0));
            SetText(pickedTMP, pickedUGUI, string.Format(pickedFmt, 0));
            SetText(depositedTMP, depositedUGUI, string.Format(depositedFmt, 0));
            SetText(srcDiscTMP, srcDiscUGUI, string.Format(srcDiscFmt, 0));
            SetText(srcDeplTMP, srcDeplUGUI, string.Format(srcDeplFmt, 0));
        }

        private static void SetText(TMP_Text tmp, Text ugui, string value)
        {
            if (tmp != null) tmp.text = value;
            else if (ugui != null) ugui.text = value;
        }

        private void Show()
        {
            StopAllCoroutines();
            StartCoroutine(FadeTo(1f, true));
        }

        private void Hide()
        {
            StopAllCoroutines();
            StartCoroutine(FadeTo(0f, false));
        }

        private void HideImmediate()
        {
            if (canvasGroup == null) return;
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        private System.Collections.IEnumerator FadeTo(float target, bool interactableAtEnd)
        {
            if (canvasGroup == null) yield break;

            float start = canvasGroup.alpha;
            float t = 0f;
            while (t < fadeTime)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / fadeTime);
                // simple ease
                float e = 1f - (1f - u) * (1f - u);
                canvasGroup.alpha = Mathf.LerpUnclamped(start, target, e);
                yield return null;
            }
            canvasGroup.alpha = target;
            canvasGroup.interactable = interactableAtEnd && target > 0.99f;
            canvasGroup.blocksRaycasts = canvasGroup.interactable;
        }

        // Button hooks
        public void PressRestart() => OnRestartPressed?.Invoke();
        public void PressMenu() => OnMenuPressed?.Invoke();
    }
}
