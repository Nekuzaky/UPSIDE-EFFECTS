using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Mindrift.UI
{
    public sealed class MainMenuController : MonoBehaviour
    {
        [Header("Scene")]
        [SerializeField] private string gameplaySceneName = "Games";

        [Header("UI")]
        [SerializeField] private Button startButton;
        [SerializeField] private Button quitButton;

        [Header("Input Shortcuts")]
        [SerializeField] private bool allowKeyboardShortcuts = true;
        [SerializeField] private KeyCode startKey = KeyCode.Return;
        [SerializeField] private KeyCode quitKey = KeyCode.Escape;

        private void Awake()
        {
            Time.timeScale = 1f;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            if (startButton != null)
            {
                startButton.onClick.AddListener(StartGame);
            }

            if (quitButton != null)
            {
                quitButton.onClick.AddListener(QuitGame);
            }
        }

        private void Update()
        {
            if (!allowKeyboardShortcuts)
            {
                return;
            }

            if (IsKeyPressed(startKey))
            {
                StartGame();
                return;
            }

            if (IsKeyPressed(quitKey))
            {
                QuitGame();
            }
        }

        public void StartGame()
        {
            Time.timeScale = 1f;

            if (!IsSceneInBuildSettings(gameplaySceneName))
            {
                Debug.LogError($"[MINDRIFT] Scene '{gameplaySceneName}' is not in Build Settings.");
                return;
            }

            SceneManager.LoadScene(gameplaySceneName);
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private static bool IsSceneInBuildSettings(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return false;
            }

            int count = SceneManager.sceneCountInBuildSettings;
            for (int i = 0; i < count; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                string name = Path.GetFileNameWithoutExtension(path);
                if (string.Equals(name, sceneName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
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

            if (Gamepad.current != null && keyCode == KeyCode.Return && Gamepad.current.startButton.wasPressedThisFrame)
            {
                return true;
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
                case KeyCode.Return:
                    key = Key.Enter;
                    return true;
                case KeyCode.Escape:
                    key = Key.Escape;
                    return true;
                case KeyCode.Space:
                    key = Key.Space;
                    return true;
                default:
                    key = Key.None;
                    return false;
            }
        }
    }
}
