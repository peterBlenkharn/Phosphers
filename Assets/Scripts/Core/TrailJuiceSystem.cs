using UnityEngine;

namespace Phosphers.Core
{
    public class TrailJuiceSystem : MonoBehaviour
    {
        public static TrailJuiceSystem Instance { get; private set; }

        [SerializeField] private GameManager gameManager;
        [SerializeField] private float maxJuice = 100f;
        [SerializeField] private float startingJuice = 100f;

        public float CurrentJuice { get; private set; }
        public float MaxJuice => maxJuice;

        public event System.Action<float, float> OnJuiceChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            ResetJuice();
        }

        private void OnEnable()
        {
            if (gameManager == null)
            {
#if UNITY_2023_1_OR_NEWER
                gameManager = FindFirstObjectByType<GameManager>();
#else
                gameManager = FindObjectOfType<GameManager>();
#endif
            }

            if (gameManager != null) gameManager.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            if (gameManager != null) gameManager.OnStateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(GameState state)
        {
            if (state == GameState.Run)
                ResetJuice();
        }

        public void ResetJuice()
        {
            CurrentJuice = Mathf.Clamp(startingJuice, 0f, maxJuice);
            OnJuiceChanged?.Invoke(CurrentJuice, maxJuice);
        }

        public bool TryConsume(float amount)
        {
            if (amount <= 0f) return true;
            if (CurrentJuice <= 0f) return false;
            if (amount > CurrentJuice) return false;

            CurrentJuice -= amount;
            OnJuiceChanged?.Invoke(CurrentJuice, maxJuice);
            return true;
        }

        public void SetMaxJuice(float value, bool refill = true)
        {
            maxJuice = Mathf.Max(0f, value);
            if (refill)
                CurrentJuice = maxJuice;
            else
                CurrentJuice = Mathf.Clamp(CurrentJuice, 0f, maxJuice);

            OnJuiceChanged?.Invoke(CurrentJuice, maxJuice);
        }
    }
}
