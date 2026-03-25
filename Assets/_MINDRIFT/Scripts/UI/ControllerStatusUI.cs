using UnityEngine;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Mindrift.UI
{
    [RequireComponent(typeof(Text))]
    public sealed class ControllerStatusUI : MonoBehaviour
    {
        [SerializeField] private Text statusLabel;
        [SerializeField] private string connectedText = "CONTROLLER: CONNECTED";
        [SerializeField] private string disconnectedText = "CONTROLLER: NOT DETECTED";
        [SerializeField] private Color connectedColor = new Color(0.2f, 0.95f, 1f, 1f);
        [SerializeField] private Color disconnectedColor = new Color(0.8f, 0.85f, 0.92f, 1f);

        private void Awake()
        {
            if (statusLabel == null)
            {
                statusLabel = GetComponent<Text>();
            }
        }

        private void OnEnable()
        {
#if ENABLE_INPUT_SYSTEM
            InputSystem.onDeviceChange += OnDeviceChange;
#endif
            Refresh();
        }

        private void OnDisable()
        {
#if ENABLE_INPUT_SYSTEM
            InputSystem.onDeviceChange -= OnDeviceChange;
#endif
        }

        public void Refresh()
        {
            if (statusLabel == null)
            {
                return;
            }

            bool connected = IsControllerConnected();
            statusLabel.text = connected ? connectedText : disconnectedText;
            statusLabel.color = connected ? connectedColor : disconnectedColor;
        }

#if ENABLE_INPUT_SYSTEM
        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            switch (change)
            {
                case InputDeviceChange.Added:
                case InputDeviceChange.Removed:
                case InputDeviceChange.Reconnected:
                case InputDeviceChange.Disconnected:
                    Refresh();
                    break;
            }
        }
#endif

        private static bool IsControllerConnected()
        {
#if ENABLE_INPUT_SYSTEM
            return Gamepad.all.Count > 0;
#else
            return false;
#endif
        }
    }
}
