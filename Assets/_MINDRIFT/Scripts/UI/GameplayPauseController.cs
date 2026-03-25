using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using Mindrift.Core;
using Mindrift.Player;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Mindrift.UI
{
    [DefaultExecutionOrder(-6100)]
    public sealed class GameplayPauseController : MonoBehaviour
    {
        [Header("Scenes")]
        [SerializeField] private string gameplaySceneName = "Games";
        [SerializeField] private string pauseSceneName = "Break";
        [SerializeField] private string mainMenuSceneName = "MainMenue";
        [SerializeField] private string mainMenuSceneFallbackName = "MainMenu";

        [Header("Input")]
        [SerializeField] private bool allowPauseInput = true;
        [SerializeField] private bool keyboardEscapePauses = true;
        [SerializeField] private bool gamepadStartPauses = true;

        [Header("References")]
        [SerializeField] private LivesSystem livesSystem;
        [SerializeField] private FirstPersonMotor firstPersonMotor;
        [SerializeField] private FirstPersonLook firstPersonLook;

        private static GameplayPauseController instance;

        private bool isPaused;
        private bool wasMotorEnabled;
        private bool wasLookEnabled;
        private float cachedTimeScale = 1f;

        public static bool IsPaused => instance != null && instance.isPaused;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            AutoWireReferences();

            if (!string.IsNullOrWhiteSpace(gameplaySceneName))
            {
                Scene activeScene = SceneManager.GetActiveScene();
                if (!string.Equals(activeScene.name, gameplaySceneName, StringComparison.OrdinalIgnoreCase))
                {
                    gameplaySceneName = activeScene.name;
                }
            }
        }

        private void OnDestroy()
        {
            if (instance != this)
            {
                return;
            }

            if (isPaused)
            {
                Time.timeScale = cachedTimeScale <= 0f ? 1f : cachedTimeScale;
            }

            instance = null;
        }

        private void Update()
        {
            if (!allowPauseInput)
            {
                return;
            }

            if (isPaused && OptionsMenuController.IsAnyMenuOpen)
            {
                return;
            }

            if (WasPausePressedThisFrame())
            {
                TogglePause();
            }
        }

        public void TogglePause()
        {
            if (isPaused)
            {
                ResumeFromPause();
            }
            else
            {
                PauseGame();
            }
        }

        public void PauseGame()
        {
            if (isPaused)
            {
                return;
            }

            AutoWireReferences();
            if (livesSystem != null && livesSystem.IsGameOver)
            {
                return;
            }

            cachedTimeScale = Time.timeScale;
            isPaused = true;

            ApplyGameplayControl(false);
            Time.timeScale = 0f;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            LoadPauseSceneIfNeeded();
        }

        public void ResumeFromPause()
        {
            if (!isPaused)
            {
                return;
            }

            isPaused = false;
            Time.timeScale = cachedTimeScale <= 0f ? 1f : cachedTimeScale;
            UnloadPauseSceneIfLoaded();
            ApplyGameplayControl(true);

            if (firstPersonLook == null)
            {
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
            }
        }

        public void LoadMainMenu()
        {
            isPaused = false;
            Time.timeScale = 1f;
            ApplyGameplayControl(true);

            if (livesSystem != null)
            {
                livesSystem.ClearGameOverState();
            }

            string resolvedSceneName = ResolveSceneName(mainMenuSceneName, mainMenuSceneFallbackName, "MainMenu", "MainMenue");
            if (string.IsNullOrWhiteSpace(resolvedSceneName))
            {
                Debug.LogError("[MINDRIFT] Main menu scene not found in Build Settings.");
                return;
            }

            SceneManager.LoadScene(resolvedSceneName);
        }

        public static bool TryResumeFromPause()
        {
            if (instance == null)
            {
                return false;
            }

            instance.ResumeFromPause();
            return true;
        }

        public static bool TryLoadMainMenu()
        {
            if (instance == null)
            {
                return false;
            }

            instance.LoadMainMenu();
            return true;
        }

        private void AutoWireReferences()
        {
            if (livesSystem == null)
            {
                livesSystem = FindFirstObjectByType<LivesSystem>();
            }

            if (firstPersonMotor == null)
            {
                firstPersonMotor = FindFirstObjectByType<FirstPersonMotor>();
            }

            if (firstPersonLook == null)
            {
                firstPersonLook = FindFirstObjectByType<FirstPersonLook>();
            }
        }

        private void ApplyGameplayControl(bool enableControl)
        {
            if (firstPersonMotor != null)
            {
                if (!enableControl)
                {
                    wasMotorEnabled = firstPersonMotor.enabled;
                    firstPersonMotor.enabled = false;
                }
                else
                {
                    firstPersonMotor.enabled = wasMotorEnabled;
                }
            }

            if (firstPersonLook != null)
            {
                if (!enableControl)
                {
                    wasLookEnabled = firstPersonLook.enabled;
                    firstPersonLook.enabled = false;
                    firstPersonLook.SetCursorLock(false);
                }
                else
                {
                    firstPersonLook.enabled = wasLookEnabled;
                    if (firstPersonLook.enabled)
                    {
                        firstPersonLook.SetCursorLock(true);
                    }
                }
            }
        }

        private void LoadPauseSceneIfNeeded()
        {
            string resolvedPauseSceneName = ResolveSceneName(pauseSceneName, "Break");
            if (string.IsNullOrWhiteSpace(resolvedPauseSceneName))
            {
                Debug.LogWarning("[MINDRIFT] Pause scene not found in Build Settings.");
                return;
            }

            Scene pauseScene = SceneManager.GetSceneByName(resolvedPauseSceneName);
            if (pauseScene.IsValid() && pauseScene.isLoaded)
            {
                return;
            }

            SceneManager.LoadSceneAsync(resolvedPauseSceneName, LoadSceneMode.Additive);
        }

        private void UnloadPauseSceneIfLoaded()
        {
            string resolvedPauseSceneName = ResolveSceneName(pauseSceneName, "Break");
            if (string.IsNullOrWhiteSpace(resolvedPauseSceneName))
            {
                return;
            }

            Scene pauseScene = SceneManager.GetSceneByName(resolvedPauseSceneName);
            if (pauseScene.IsValid() && pauseScene.isLoaded)
            {
                SceneManager.UnloadSceneAsync(resolvedPauseSceneName);
            }
        }

        private static string ResolveSceneName(params string[] candidates)
        {
            if (candidates == null)
            {
                return string.Empty;
            }

            for (int i = 0; i < candidates.Length; i++)
            {
                string candidate = candidates[i];
                if (string.IsNullOrWhiteSpace(candidate))
                {
                    continue;
                }

                if (IsSceneInBuildSettings(candidate))
                {
                    return candidate;
                }
            }

            return string.Empty;
        }

        private static bool IsSceneInBuildSettings(string sceneName)
        {
            int count = SceneManager.sceneCountInBuildSettings;
            for (int i = 0; i < count; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                string name = Path.GetFileNameWithoutExtension(path);
                if (string.Equals(name, sceneName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private bool WasPausePressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            bool keyboardPressed = keyboardEscapePauses && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
            bool gamepadPressed = gamepadStartPauses && Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame;
            return keyboardPressed || gamepadPressed;
#else
            return false;
#endif
        }
    }
}
