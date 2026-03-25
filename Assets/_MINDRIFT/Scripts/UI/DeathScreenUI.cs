using UnityEngine;
using UnityEngine.SceneManagement;
using Mindrift.Core;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Mindrift.UI
{
    public sealed class DeathScreenUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private LivesSystem livesSystem;
        [SerializeField] private Component messageTextComponent;
        [SerializeField] private CanvasGroup overlayCanvasGroup;

        [Header("Labels")]
        [SerializeField] private string titleText = "NEURAL COLLAPSE";
        [SerializeField] private string subText = "LIVES DEPLETED";
        [SerializeField] private string retryText = "PRESS R TO RETRY";
        [SerializeField] private string menuText = "PRESS M FOR MAIN MENU";

        [Header("Input")]
        [SerializeField] private KeyCode retryKey = KeyCode.R;
        [SerializeField] private KeyCode menuKey = KeyCode.M;

        [Header("Scene")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        private bool isVisible;

        private void Awake()
        {
            if (livesSystem == null)
            {
                livesSystem = FindFirstObjectByType<LivesSystem>();
            }

            HideImmediate();
        }

        private void OnEnable()
        {
            if (livesSystem == null)
            {
                return;
            }

            livesSystem.LivesDepleted += HandleLivesDepleted;
            livesSystem.GameOverStateChanged += HandleGameOverStateChanged;
        }

        private void OnDisable()
        {
            if (livesSystem == null)
            {
                return;
            }

            livesSystem.LivesDepleted -= HandleLivesDepleted;
            livesSystem.GameOverStateChanged -= HandleGameOverStateChanged;
        }

        private void Update()
        {
            if (!isVisible)
            {
                return;
            }

            if (IsKeyPressed(retryKey))
            {
                OnRetryPressed();
                return;
            }

            if (IsKeyPressed(menuKey))
            {
                OnMainMenuPressed();
            }
        }

        public void OnRetryPressed()
        {
            if (livesSystem == null)
            {
                return;
            }

            livesSystem.ContinueFromGameOver();
            HideImmediate();
        }

        public void OnMainMenuPressed()
        {
            if (livesSystem != null)
            {
                livesSystem.ClearGameOverState();
            }

            if (string.IsNullOrWhiteSpace(mainMenuSceneName))
            {
                return;
            }

            Time.timeScale = 1f;
            SceneManager.LoadScene(mainMenuSceneName);
        }

        private void HandleLivesDepleted()
        {
            if (livesSystem == null || !livesSystem.IsGameOver)
            {
                return;
            }

            Show();
        }

        private void HandleGameOverStateChanged(bool isGameOver)
        {
            if (isGameOver)
            {
                Show();
            }
            else
            {
                HideImmediate();
            }
        }

        private void Show()
        {
            isVisible = true;
            if (overlayCanvasGroup != null)
            {
                overlayCanvasGroup.alpha = 1f;
            }

            if (messageTextComponent != null)
            {
                string message = $"{titleText}\n{subText}\n\n{retryText}\n{menuText}";
                UITextUtility.SetText(messageTextComponent, message);
            }
        }

        private void HideImmediate()
        {
            isVisible = false;
            if (overlayCanvasGroup != null)
            {
                overlayCanvasGroup.alpha = 0f;
            }
        }

        private static bool IsKeyPressed(KeyCode keyCode)
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && TryMapKeyCode(keyCode, out Key mappedKey))
            {
                var keyControl = Keyboard.current[mappedKey];
                if (keyControl != null && keyControl.wasPressedThisFrame)
                {
                    return true;
                }
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(keyCode);
#else
            return false;
#endif
        }

        private static bool TryMapKeyCode(KeyCode keyCode, out Key key)
        {
            switch (keyCode)
            {
                case KeyCode.R:
                    key = Key.R;
                    return true;
                case KeyCode.M:
                    key = Key.M;
                    return true;
                case KeyCode.Return:
                    key = Key.Enter;
                    return true;
                case KeyCode.Escape:
                    key = Key.Escape;
                    return true;
                default:
                    key = Key.None;
                    return false;
            }
        }
    }
}
