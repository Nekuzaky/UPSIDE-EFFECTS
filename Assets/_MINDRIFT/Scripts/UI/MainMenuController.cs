using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Mindrift.UI
{
    public sealed class MainMenuController : MonoBehaviour
    {
        [Header("Scene")]
        [SerializeField] private string gameplaySceneName = "Games";

        [Header("Buttons")]
        [SerializeField] private Button startButton;
        [SerializeField] private Button optionsButton;
        [SerializeField] private Button quitButton;

        [Header("Branding")]
        [SerializeField] private string subtitleText = "DESCEND THROUGH COGNITIVE NOISE";

        private OptionsMenuController optionsMenu;

        private void Awake()
        {
            SettingsManager.EnsureInitialized();
            MenuInputSystemUtility.EnsureEventSystem();

            Time.timeScale = 1f;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            ResolveReferences();
            ConfigureHierarchyStyle();
            ConfigureButtonNavigation();
            HookEvents();

            optionsMenu = OptionsMenuController.EnsureForCanvas(transform);
        }

        private void OnEnable()
        {
            MenuNavigationController.SelectDefault(this, startButton);
        }

        private void OnDisable()
        {
            UnhookEvents();
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

        public void OpenOptions()
        {
            if (optionsMenu == null)
            {
                return;
            }

            optionsMenu.OpenAudioTab(() => MenuNavigationController.SelectDefault(this, optionsButton));
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void ResolveReferences()
        {
            startButton = startButton != null ? startButton : FindButton("StartButton", "PlayButton");
            quitButton = quitButton != null ? quitButton : FindButton("QuitButton");
            optionsButton = optionsButton != null ? optionsButton : FindButton("OptionsButton");

            if (optionsButton == null && startButton != null)
            {
                optionsButton = CreateButtonClone(startButton, "OptionsButton", "OPTIONS");
            }

            SetButtonLabel(startButton, "PLAY");
            SetButtonLabel(quitButton, "QUIT");
            SetButtonLabel(optionsButton, "OPTIONS");

            Text subtitle = FindText("Subtitle");
            if (subtitle != null)
            {
                subtitle.text = subtitleText;
                subtitle.color = new Color(0.5f, 0.92f, 1f, 0.95f);
                subtitle.fontSize = 28;
            }

            EnsureFooterHints();
        }

        private void ConfigureHierarchyStyle()
        {
            PositionButton(startButton, 0.5f);
            PositionButton(optionsButton, 0.4f);
            PositionButton(quitButton, 0.3f);

            StyleButton(startButton);
            StyleButton(optionsButton);
            StyleButton(quitButton);
        }

        private void ConfigureButtonNavigation()
        {
            List<Selectable> navigationOrder = new List<Selectable>();
            if (startButton != null) navigationOrder.Add(startButton);
            if (optionsButton != null) navigationOrder.Add(optionsButton);
            if (quitButton != null) navigationOrder.Add(quitButton);
            MenuNavigationController.ApplyVerticalNavigation(navigationOrder);
        }

        private void HookEvents()
        {
            UnhookEvents();

            if (startButton != null)
            {
                startButton.onClick.AddListener(StartGame);
            }

            if (optionsButton != null)
            {
                optionsButton.onClick.AddListener(OpenOptions);
            }

            if (quitButton != null)
            {
                quitButton.onClick.AddListener(QuitGame);
            }
        }

        private void UnhookEvents()
        {
            if (startButton != null)
            {
                startButton.onClick.RemoveListener(StartGame);
            }

            if (optionsButton != null)
            {
                optionsButton.onClick.RemoveListener(OpenOptions);
            }

            if (quitButton != null)
            {
                quitButton.onClick.RemoveListener(QuitGame);
            }
        }

        private Button FindButton(params string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                Transform found = transform.Find(names[i]);
                if (found != null)
                {
                    Button button = found.GetComponent<Button>();
                    if (button != null)
                    {
                        return button;
                    }
                }
            }

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

        private Text FindText(string name)
        {
            Text[] texts = GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (string.Equals(texts[i].name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return texts[i];
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
            rect.sizeDelta = new Vector2(420f, 86f);
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

        private void EnsureFooterHints()
        {
            if (FindText("FooterHints") != null)
            {
                return;
            }

            Font font = ResolveBuiltinFont();
            GameObject footerObject = new GameObject("FooterHints", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            footerObject.transform.SetParent(transform, false);

            RectTransform rect = footerObject.transform as RectTransform;
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.anchoredPosition = new Vector2(0f, 28f);
                rect.sizeDelta = new Vector2(-100f, 28f);
            }

            Text text = footerObject.GetComponent<Text>();
            text.font = font;
            text.fontSize = 18;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.5f, 0.92f, 1f, 0.9f);
            text.text = "KEYBOARD: ARROWS/WASD + ENTER  |  CONTROLLER: DPAD/STICK + A";
            text.raycastTarget = false;
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
    }
}
