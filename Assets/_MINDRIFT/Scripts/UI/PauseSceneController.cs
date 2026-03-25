using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Mindrift.UI
{
    public sealed class PauseSceneController : MonoBehaviour
    {
        [Header("Scene Names")]
        [SerializeField] private string gameplaySceneName = "Games";
        [SerializeField] private string mainMenuSceneName = "MainMenu";
        [SerializeField] private string mainMenuSceneFallbackName = "MainMenue";

        [Header("UI")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button optionsButton;
        [SerializeField] private Button mainMenuButton;

        private OptionsMenuController optionsMenu;

        private void Awake()
        {
            SettingsManager.EnsureInitialized();
            MenuInputSystemUtility.EnsureEventSystem();

            Time.timeScale = 0f;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            ResolveReferences();
            ConfigureStyle();
            ConfigureNavigation();

            optionsMenu = OptionsMenuController.EnsureForCanvas(transform);
        }

        private void OnEnable()
        {
            HookEvents();
            MenuNavigationController.SelectDefault(this, resumeButton);
        }

        private void OnDisable()
        {
            UnhookEvents();
        }

        public void OnResumePressed()
        {
            if (GameplayPauseController.TryResumeFromPause())
            {
                return;
            }

            if (!IsSceneInBuildSettings(gameplaySceneName))
            {
                return;
            }

            Time.timeScale = 1f;
            SceneManager.LoadScene(gameplaySceneName);
        }

        public void OnOpenOptionsPressed()
        {
            if (optionsMenu == null)
            {
                return;
            }

            optionsMenu.OpenAudioTab(() => MenuNavigationController.SelectDefault(this, optionsButton));
        }

        public void OnMainMenuPressed()
        {
            if (GameplayPauseController.TryLoadMainMenu())
            {
                return;
            }

            string targetScene = ResolveSceneName(mainMenuSceneName, mainMenuSceneFallbackName, "MainMenu", "MainMenue");
            if (string.IsNullOrWhiteSpace(targetScene))
            {
                return;
            }

            Time.timeScale = 1f;
            SceneManager.LoadScene(targetScene);
        }

        private void ResolveReferences()
        {
            resumeButton = resumeButton != null ? resumeButton : FindButton("ResumeButton");
            mainMenuButton = mainMenuButton != null ? mainMenuButton : FindButton("MainMenuButton");
            optionsButton = optionsButton != null ? optionsButton : FindButton("OptionsButton");

            if (optionsButton == null && resumeButton != null)
            {
                optionsButton = CreateButtonClone(resumeButton, "OptionsButton", "OPTIONS");
            }

            SetButtonLabel(resumeButton, "RESUME");
            SetButtonLabel(optionsButton, "OPTIONS");
            SetButtonLabel(mainMenuButton, "MAIN MENU");

            EnsureHints();
        }

        private void ConfigureStyle()
        {
            PositionButton(resumeButton, 0.5f);
            PositionButton(optionsButton, 0.4f);
            PositionButton(mainMenuButton, 0.3f);

            StyleButton(resumeButton);
            StyleButton(optionsButton);
            StyleButton(mainMenuButton);
        }

        private void ConfigureNavigation()
        {
            List<Selectable> order = new List<Selectable>();
            if (resumeButton != null) order.Add(resumeButton);
            if (optionsButton != null) order.Add(optionsButton);
            if (mainMenuButton != null) order.Add(mainMenuButton);
            MenuNavigationController.ApplyVerticalNavigation(order);
        }

        private void HookEvents()
        {
            UnhookEvents();

            if (resumeButton != null)
            {
                resumeButton.onClick.AddListener(OnResumePressed);
            }

            if (optionsButton != null)
            {
                optionsButton.onClick.AddListener(OnOpenOptionsPressed);
            }

            if (mainMenuButton != null)
            {
                mainMenuButton.onClick.AddListener(OnMainMenuPressed);
            }
        }

        private void UnhookEvents()
        {
            if (resumeButton != null)
            {
                resumeButton.onClick.RemoveListener(OnResumePressed);
            }

            if (optionsButton != null)
            {
                optionsButton.onClick.RemoveListener(OnOpenOptionsPressed);
            }

            if (mainMenuButton != null)
            {
                mainMenuButton.onClick.RemoveListener(OnMainMenuPressed);
            }
        }

        private Button FindButton(params string[] names)
        {
            Button[] buttons = GetComponentsInChildren<Button>(true);
            for (int i = 0; i < names.Length; i++)
            {
                for (int j = 0; j < buttons.Length; j++)
                {
                    if (string.Equals(buttons[j].name, names[i], StringComparison.OrdinalIgnoreCase))
                    {
                        return buttons[j];
                    }
                }
            }

            return null;
        }

        private Button CreateButtonClone(Button source, string newName, string label)
        {
            if (source == null || source.transform.parent == null)
            {
                return null;
            }

            GameObject clone = Instantiate(source.gameObject, source.transform.parent, false);
            clone.name = newName;
            Button button = clone.GetComponent<Button>();
            SetButtonLabel(button, label);
            return button;
        }

        private static void SetButtonLabel(Button button, string text)
        {
            if (button == null)
            {
                return;
            }

            Text label = button.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.text = text;
                label.color = new Color(0.9f, 0.98f, 1f, 1f);
                label.fontStyle = FontStyle.Bold;
            }
        }

        private static void PositionButton(Button button, float anchorY)
        {
            if (button == null)
            {
                return;
            }

            RectTransform rect = button.transform as RectTransform;
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(0.5f, anchorY);
            rect.anchorMax = new Vector2(0.5f, anchorY);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(430f, 90f);
        }

        private static void StyleButton(Button button)
        {
            if (button == null)
            {
                return;
            }

            Image image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = new Color(0.09f, 0.14f, 0.22f, 0.92f);
            }

            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.09f, 0.14f, 0.22f, 0.92f);
            colors.highlightedColor = new Color(0.13f, 0.35f, 0.49f, 1f);
            colors.selectedColor = new Color(0.16f, 0.45f, 0.62f, 1f);
            colors.pressedColor = new Color(0.08f, 0.24f, 0.32f, 1f);
            colors.disabledColor = new Color(0.22f, 0.22f, 0.22f, 0.48f);
            button.colors = colors;

            if (button.GetComponent<MenuSelectableFeedback>() == null)
            {
                button.gameObject.AddComponent<MenuSelectableFeedback>();
            }
        }

        private void EnsureHints()
        {
            Text hints = FindHintLabel();
            if (hints == null)
            {
                GameObject hintGO = new GameObject("Hint", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                hintGO.transform.SetParent(transform, false);
                hints = hintGO.GetComponent<Text>();

                RectTransform rect = hintGO.transform as RectTransform;
                if (rect != null)
                {
                    rect.anchorMin = new Vector2(0f, 0f);
                    rect.anchorMax = new Vector2(1f, 0f);
                    rect.pivot = new Vector2(0.5f, 0f);
                    rect.anchoredPosition = new Vector2(0f, 30f);
                    rect.sizeDelta = new Vector2(-100f, 28f);
                }
            }

            hints.font = ResolveBuiltinFont();
            hints.fontSize = 18;
            hints.alignment = TextAnchor.MiddleCenter;
            hints.text = "RESUME: ESC/START  |  NAV: ARROWS/WASD OR DPAD/STICK";
            hints.color = new Color(0.5f, 0.92f, 1f, 0.9f);
            hints.raycastTarget = false;
        }

        private Text FindHintLabel()
        {
            Text[] texts = GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (string.Equals(texts[i].name, "Hint", StringComparison.OrdinalIgnoreCase))
                {
                    return texts[i];
                }
            }

            return null;
        }

        private static Font ResolveBuiltinFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
            {
                return font;
            }

            return Resources.GetBuiltinResource<Font>("Arial.ttf");
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
                if (string.Equals(name, sceneName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
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
    }
}
