using UnityEngine;
using UnityEngine.UI;

namespace Phosphers.Core
{
    public class UIController : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private GameManager gameManager;

        [Header("Canvases (CanvasGroup recommended)")]
        [SerializeField] private CanvasGroup menuCanvas;
        [SerializeField] private CanvasGroup endCanvas;

        [Header("Buttons (optional)")]
        [SerializeField] private Button startButton;
        [SerializeField] private Button restartButton;

        private void Reset()
        {
            if (gameManager == null)
                gameManager = FindGM();
        }

        private void OnEnable()
        {
            if (gameManager == null)
                gameManager = FindGM();

            if (gameManager != null)
            {
                gameManager.OnStateChanged += HandleStateChanged;
                HandleStateChanged(gameManager.CurrentState);
            }

            if (startButton != null) startButton.onClick.AddListener(() => gameManager.StartRun());
            if (restartButton != null) restartButton.onClick.AddListener(() => gameManager.ReturnToMenu());
        }

        private void OnDisable()
        {
            if (gameManager != null) gameManager.OnStateChanged -= HandleStateChanged;
            if (startButton != null) startButton.onClick.RemoveAllListeners();
            if (restartButton != null) restartButton.onClick.RemoveAllListeners();
        }

        private static GameManager FindGM()
        {
            // Prefer Inspector assignment; this is a fallback.
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<GameManager>();
#else
            return Object.FindObjectOfType<GameManager>();
#endif
        }

        private void HandleStateChanged(GameState state)
        {
            SetVisible(menuCanvas, state == GameState.Menu);
            SetVisible(endCanvas, state == GameState.End);
        }

        private static void SetVisible(CanvasGroup cg, bool visible)
        {
            if (cg == null) return;
            cg.alpha = visible ? 1f : 0f;
            cg.interactable = visible;
            cg.blocksRaycasts = visible;
        }
    }
}
