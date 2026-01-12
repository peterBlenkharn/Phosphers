using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Phosphers.Core;

namespace Phosphers.UI
{
    public class TrailJuiceHUD : MonoBehaviour
    {
        [SerializeField] private TMP_Text juiceTMP;
        [SerializeField] private Text juiceUGUI;
        [SerializeField] private string format = "Juice: {0:0}";

        private TrailJuiceSystem _juice;

        private void OnEnable()
        {
            _juice = TrailJuiceSystem.Instance;
            if (_juice != null) _juice.OnJuiceChanged += HandleJuiceChanged;
            UpdateText(_juice != null ? _juice.CurrentJuice : 0f);
        }

        private void OnDisable()
        {
            if (_juice != null) _juice.OnJuiceChanged -= HandleJuiceChanged;
        }

        private void HandleJuiceChanged(float current, float max)
        {
            UpdateText(current);
        }

        private void UpdateText(float value)
        {
            string text = string.Format(format, value);
            if (juiceTMP != null) juiceTMP.text = text;
            else if (juiceUGUI != null) juiceUGUI.text = text;
        }
    }
}
