using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Mindrift.Auth;
using Mindrift.Core;
using Mindrift.Online;
using Mindrift.Online.Core;
using Mindrift.Online.Models;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Mindrift.UI
{
    public sealed class MainMenuController : MonoBehaviour
    {
        [Serializable]
        private sealed class LeaderboardSeedEntry
        {
            public string playerName = "PLAYER";
            public int score = 0;
        }

        [Header("Scene")]
        [SerializeField] private string gameplaySceneName = "Games";

        [Header("Buttons")]
        [SerializeField] private Button startButton;
        [SerializeField] private Button optionsButton;
        [SerializeField] private Button quitButton;

        [Header("Branding")]
        [SerializeField] private string subtitleText = "DESCEND THROUGH COGNITIVE NOISE";

        [Header("Footer")]
        [SerializeField] private string footerHintText = "KEYBOARD: ARROWS/WASD + ENTER  |  CONTROLLER: DPAD/STICK + A";
        [SerializeField] private string websiteUrl = "https://nekuzaky.com/mindrift";
        [SerializeField] private string websiteLabel = "WEBSITE: nekuzaky.com/mindrift";

        [Header("Presentation")]
        [SerializeField] private float startTransitionDuration = 0.25f;
        [SerializeField] private Color startTransitionColor = new Color(0.01f, 0.03f, 0.08f, 1f);

        [Header("Auth")]
        [SerializeField] private string authGuestHintText = "Sign in with your nekuzaky.com account to sync runs, stats, and settings.";
        [SerializeField] private string authConnectedHintText = "Online session active. Your profile is synced with the API.";

        [Header("Leaderboard")]
        [SerializeField] private string localPlayerLeaderboardName = "YOU";
        [SerializeField] private int leaderboardEntryCount = 5;
        [SerializeField] private List<LeaderboardSeedEntry> fallbackLeaderboardEntries = new List<LeaderboardSeedEntry>
        {
            new LeaderboardSeedEntry { playerName = "NEON_GHOST", score = 12480 },
            new LeaderboardSeedEntry { playerName = "PIXELVORE", score = 11820 },
            new LeaderboardSeedEntry { playerName = "SYNTHKID", score = 10340 },
            new LeaderboardSeedEntry { playerName = "VOIDRUNNER", score = 9870 },
            new LeaderboardSeedEntry { playerName = "MINDDROP", score = 9440 }
        };

        private IAuthService authService;
        private Text statsHeaderText;
        private OptionsMenuController optionsMenu;
        private Button footerLinkButton;
        private Text authHeaderText;
        private Text authStatusText;
        private Text authHintText;
        private Text authMessageText;
        private GameObject authUsernameRow;
        private GameObject authIdentifierRow;
        private GameObject authPasswordRow;
        private InputField authUsernameInput;
        private InputField authIdentifierInput;
        private InputField authPasswordInput;
        private Button authSignInButton;
        private Button authRegisterButton;
        private Button authSignOutButton;
        private string authFeedbackMessage = string.Empty;
        private Text leaderboardTitleText;
        private readonly List<Text> leaderboardNameTexts = new List<Text>();
        private readonly List<Text> leaderboardScoreTexts = new List<Text>();
        private Text totalRunsValueText;
        private Text totalDeathsValueText;
        private Text topScoreValueText;
        private Text topHeightValueText;
        private RectTransform menuLayoutRoot;
        private GameObject rightInfoPanel;
        private Image backgroundOverlay;
        private Image sceneTransitionOverlay;
        private MainMenuAtmosphereFx atmosphereFx;
        private Vector2 lastKnownCanvasSize;
        private float nextLogoGlitchAt = 1.5f;
        private float logoGlitchUntil;
        private bool isSceneTransitionRunning;
        private MindriftOnlineService onlineService;
        private bool isAuthRequestInFlight;
        private bool isOnlineRefreshInFlight;

        private void Awake()
        {
            SettingsManager.EnsureInitialized();
            MenuInputSystemUtility.EnsureEventSystem();
            authService = AuthRuntime.Service;
            onlineService = MindriftOnlineService.Instance;
            _ = authService.TryRestoreSessionAsync();

            Time.timeScale = 1f;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            ResolveReferences();
            atmosphereFx = MainMenuAtmosphereFx.EnsureForCanvas(transform);
            EnsureSceneTransitionOverlay();
            ConfigureHierarchyStyle();
            ApplyResponsiveLayout(force: true);
            ApplyCyberpunkTheme();
            ConfigureButtonNavigation();
            HookEvents();

            optionsMenu = OptionsMenuController.EnsureForCanvas(transform);
            RefreshLeaderboardPanel();
            RefreshStatsPanel();
            _ = RefreshOnlinePanelsAsync();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                return;
            }

            EditorApplication.delayCall -= EnsureStatsPanelInEditor;
            EditorApplication.delayCall += EnsureStatsPanelInEditor;
        }
#endif

        private void OnEnable()
        {
            isSceneTransitionRunning = false;
            SetPrimaryButtonsInteractable(true);
            EnsureSceneTransitionOverlay();
            HookEvents();
            SubscribeAuthEvents(true);
            atmosphereFx = MainMenuAtmosphereFx.EnsureForCanvas(transform);
            onlineService = MindriftOnlineService.Instance;
            MenuNavigationController.SelectDefault(this, startButton);
            ApplyResponsiveLayout(force: true);
            ApplyCyberpunkTheme();
            RefreshAuthPanel();
            RefreshLeaderboardPanel();
            RefreshStatsPanel();
            _ = RefreshOnlinePanelsAsync();
        }

        private void OnDisable()
        {
            SubscribeAuthEvents(false);
            UnhookEvents();
            isSceneTransitionRunning = false;
            SetPrimaryButtonsInteractable(true);
        }

        public void StartGame()
        {
            if (isSceneTransitionRunning)
            {
                return;
            }

            Time.timeScale = 1f;

            if (!IsSceneInBuildSettings(gameplaySceneName))
            {
                Debug.LogError($"[MINDRIFT] Scene '{gameplaySceneName}' is not in Build Settings.");
                return;
            }

            StartCoroutine(StartGameTransitionRoutine());
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

        private void Update()
        {
            ApplyResponsiveLayout(force: false);
            UpdateLogoGlitch();
        }

        private IEnumerator StartGameTransitionRoutine()
        {
            isSceneTransitionRunning = true;
            SetPrimaryButtonsInteractable(false);
            EnsureSceneTransitionOverlay();

            if (sceneTransitionOverlay != null)
            {
                float duration = Mathf.Max(0.01f, startTransitionDuration);
                Color color = startTransitionColor;
                color.a = 0f;
                sceneTransitionOverlay.color = color;
                sceneTransitionOverlay.transform.SetAsLastSibling();

                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    color.a = Mathf.SmoothStep(0f, 1f, t);
                    sceneTransitionOverlay.color = color;
                    yield return null;
                }

                color.a = 1f;
                sceneTransitionOverlay.color = color;
            }

            SceneManager.LoadScene(gameplaySceneName);
        }

        private void SetPrimaryButtonsInteractable(bool interactable)
        {
            if (startButton != null)
            {
                startButton.interactable = interactable;
            }

            if (optionsButton != null)
            {
                optionsButton.interactable = interactable;
            }

            if (quitButton != null)
            {
                quitButton.interactable = interactable;
            }
        }

        private void EnsureSceneTransitionOverlay()
        {
            GameObject overlayObject = FindChild(transform, "SceneTransitionOverlay");
            if (overlayObject == null)
            {
                overlayObject = CreateUiObject("SceneTransitionOverlay", transform, typeof(Image));
            }

            sceneTransitionOverlay = overlayObject.GetComponent<Image>();
            if (sceneTransitionOverlay == null)
            {
                sceneTransitionOverlay = overlayObject.AddComponent<Image>();
            }

            sceneTransitionOverlay.raycastTarget = false;

            RectTransform rect = overlayObject.transform as RectTransform;
            if (rect != null)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }

            Color color = startTransitionColor;
            color.a = 0f;
            sceneTransitionOverlay.color = color;
            overlayObject.transform.SetAsLastSibling();
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

            EnsureBackgroundOverlay();
            EnsureFooterHints();
            EnsureAuthPanel();
            EnsureLeaderboardPanel();
            EnsureStatsPanel();
            EnsureCenterMenuLayout();
            EnsureRightInfoPanel();
        }

        private void ConfigureHierarchyStyle()
        {
            StyleButton(startButton);
            StyleButton(optionsButton);
            StyleButton(quitButton);
        }

        private void ApplyCyberpunkTheme()
        {
            StyleBranding();
            StyleMenuFrame();
            StylePanelShell(FindChild(transform, "LeaderboardPanel"), new Color(0.12f, 1f, 0.98f, 0.48f), new Color(1f, 0.18f, 0.72f, 0.85f), 0.5f);
            StylePanelShell(rightInfoPanel, new Color(0.12f, 1f, 0.98f, 0.48f), new Color(1f, 0.18f, 0.72f, 0.85f), 0.5f);
            StyleSubPanel(FindChild(rightInfoPanel != null ? rightInfoPanel.transform : null, "AuthPanel"));
            StyleSubPanel(FindChild(rightInfoPanel != null ? rightInfoPanel.transform : null, "StatsPanel"));
            StyleButton(startButton);
            StyleButton(optionsButton);
            StyleButton(quitButton);
            StyleInputField(authUsernameInput);
            StyleInputField(authIdentifierInput);
            StyleInputField(authPasswordInput);
            StyleFooter();
        }

        private void ApplyResponsiveLayout(bool force)
        {
            Vector2 canvasSize = GetCanvasSize();
            if (!force && ApproximatelyEqual(canvasSize, lastKnownCanvasSize))
            {
                return;
            }

            lastKnownCanvasSize = canvasSize;

            float width = Mathf.Max(1f, canvasSize.x);
            float height = Mathf.Max(1f, canvasSize.y);
            bool compact = width < 1440f || height < 900f;

            LayoutBackgroundOverlay();
            LayoutBranding(compact);
            LayoutMenuColumn();
            LayoutLeaderboardPanel(compact);
            LayoutAuthAndStatsPanels(compact);
            LayoutFooter(compact);
        }

        private void ConfigureButtonNavigation()
        {
            ConfigurePrimaryNavigation();
        }

        private void SubscribeAuthEvents(bool subscribe)
        {
            authService ??= AuthRuntime.Service;
            if (authService == null)
            {
                return;
            }

            authService.SessionChanged -= HandleAuthSessionChanged;
            if (subscribe)
            {
                authService.SessionChanged += HandleAuthSessionChanged;
            }
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

            if (footerLinkButton != null)
            {
                footerLinkButton.onClick.AddListener(OpenWebsiteLink);
            }

            if (authSignInButton != null)
            {
                authSignInButton.onClick.AddListener(HandleSignInPressed);
            }

            if (authRegisterButton != null)
            {
                authRegisterButton.onClick.AddListener(HandleRegisterPressed);
            }

            if (authSignOutButton != null)
            {
                authSignOutButton.onClick.AddListener(HandleSignOutPressed);
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

            if (footerLinkButton != null)
            {
                footerLinkButton.onClick.RemoveListener(OpenWebsiteLink);
            }

            if (authSignInButton != null)
            {
                authSignInButton.onClick.RemoveListener(HandleSignInPressed);
            }

            if (authRegisterButton != null)
            {
                authRegisterButton.onClick.RemoveListener(HandleRegisterPressed);
            }

            if (authSignOutButton != null)
            {
                authSignOutButton.onClick.RemoveListener(HandleSignOutPressed);
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
                label.color = new Color(0.94f, 0.98f, 1f, 1f);
                label.fontSize = 34;
                label.fontStyle = FontStyle.Bold;
                EnsureTextFx(label, new Color(0.02f, 0.04f, 0.08f, 0.95f), new Vector2(1f, -1f), new Color(0.1f, 0.95f, 1f, 0.34f), new Vector2(0f, -1f));
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
            rect.sizeDelta = new Vector2(470f, 92f);
        }

        private static void ResizeButton(Button button, float width, float height)
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

            rect.sizeDelta = new Vector2(width, height);
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
                image.color = new Color(0.03f, 0.05f, 0.08f, 0.88f);
            }

            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.03f, 0.05f, 0.08f, 0.88f);
            colors.highlightedColor = new Color(0.08f, 0.12f, 0.18f, 0.94f);
            colors.selectedColor = new Color(0.08f, 0.12f, 0.18f, 0.94f);
            colors.pressedColor = new Color(0.16f, 0.28f, 0.36f, 0.98f);
            colors.disabledColor = new Color(0.22f, 0.22f, 0.22f, 0.48f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            MenuSelectableFeedback feedback = button.GetComponent<MenuSelectableFeedback>();
            if (feedback != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(feedback);
                }
                else
                {
                    DestroyImmediate(feedback);
                }
            }

            Outline outline = image != null ? image.GetComponent<Outline>() : null;
            if (outline == null && image != null)
            {
                outline = image.gameObject.AddComponent<Outline>();
            }

            if (outline != null)
            {
                outline.effectColor = new Color(0.12f, 1f, 0.98f, 0.58f);
                outline.effectDistance = new Vector2(1f, -1f);
                outline.useGraphicAlpha = true;
            }

            Shadow shadow = image != null ? image.GetComponent<Shadow>() : null;
            if (shadow == null && image != null)
            {
                shadow = image.gameObject.AddComponent<Shadow>();
            }

            if (shadow != null)
            {
                shadow.effectColor = new Color(0.12f, 1f, 0.98f, 0.08f);
                shadow.effectDistance = Vector2.zero;
                shadow.useGraphicAlpha = true;
            }

            SetDecorActive(button.transform, "TopAccent", false);
            SetDecorActive(button.transform, "BottomAccent", false);
            SetDecorActive(button.transform, "SideAccent", false);
            SetDecorActive(button.transform, "CornerNode", false);

            MainMenuButtonFx buttonFx = button.GetComponent<MainMenuButtonFx>();
            if (buttonFx == null)
            {
                buttonFx = button.gameObject.AddComponent<MainMenuButtonFx>();
            }
        }

        private static void AddSelectableIfActive(List<Selectable> navigationOrder, Selectable selectable)
        {
            if (navigationOrder == null || selectable == null)
            {
                return;
            }

            if (!selectable.gameObject.activeInHierarchy || !selectable.IsInteractable())
            {
                return;
            }

            navigationOrder.Add(selectable);
        }

        private Vector2 GetCanvasSize()
        {
            RectTransform rectTransform = transform as RectTransform;
            if (rectTransform != null && rectTransform.rect.width > 0f && rectTransform.rect.height > 0f)
            {
                return rectTransform.rect.size;
            }

            Canvas canvas = GetComponentInParent<Canvas>();
            RectTransform canvasRect = canvas != null ? canvas.transform as RectTransform : null;
            if (canvasRect != null && canvasRect.rect.width > 0f && canvasRect.rect.height > 0f)
            {
                return canvasRect.rect.size;
            }

            return new Vector2(Screen.width, Screen.height);
        }

        private static bool ApproximatelyEqual(Vector2 a, Vector2 b)
        {
            return Mathf.Abs(a.x - b.x) < 0.5f && Mathf.Abs(a.y - b.y) < 0.5f;
        }

        private static void SetTopAnchoredRect(GameObject gameObject, float anchoredY, float height)
        {
            if (gameObject == null)
            {
                return;
            }

            RectTransform rect = gameObject.transform as RectTransform;
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -anchoredY);
            rect.sizeDelta = new Vector2(-36f, height);
        }

        private static void PositionActionButton(Button button, Vector2 anchorMin, Vector2 anchorMax, float bottom, float top)
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

            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = new Vector2(0f, bottom);
            rect.offsetMax = new Vector2(0f, top);
        }

        private void EnsureBackgroundOverlay()
        {
            GameObject overlayObject = FindChild(transform, "BackgroundOverlay");
            if (overlayObject == null)
            {
                overlayObject = CreateUiObject("BackgroundOverlay", transform, typeof(Image));
            }

            backgroundOverlay = overlayObject.GetComponent<Image>();
            backgroundOverlay.raycastTarget = false;

            RectTransform rect = overlayObject.transform as RectTransform;
            if (rect != null)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }

            Transform background = transform.Find("Background");
            if (background != null)
            {
                overlayObject.transform.SetSiblingIndex(background.GetSiblingIndex() + 1);
            }
            else
            {
                overlayObject.transform.SetAsFirstSibling();
            }
        }

        private void EnsureCenterMenuLayout()
        {
            GameObject menuPanelObject = FindChild(transform, "MenuPanel");
            if (menuPanelObject == null)
            {
                menuPanelObject = CreateUiObject("MenuPanel", transform, typeof(Image));
            }

            Transform menuPanel = menuPanelObject.transform;
            if (menuPanel == null)
            {
                return;
            }

            Image panelImage = menuPanel.GetComponent<Image>();
            if (panelImage != null)
            {
                panelImage.raycastTarget = false;
            }

            menuLayoutRoot = menuPanel as RectTransform;
            VerticalLayoutGroup layoutGroup = menuPanel.GetComponent<VerticalLayoutGroup>();
            if (layoutGroup == null)
            {
                layoutGroup = menuPanel.gameObject.AddComponent<VerticalLayoutGroup>();
            }

            layoutGroup.childAlignment = TextAnchor.MiddleCenter;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = true;
            layoutGroup.childForceExpandWidth = false;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.spacing = 26f;
            layoutGroup.padding = new RectOffset(0, 0, 0, 0);

            ContentSizeFitter fitter = menuPanel.GetComponent<ContentSizeFitter>();
            if (fitter == null)
            {
                fitter = menuPanel.gameObject.AddComponent<ContentSizeFitter>();
            }

            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            MoveButtonToMenuPanel(startButton, menuPanel);
            MoveButtonToMenuPanel(optionsButton, menuPanel);
            MoveButtonToMenuPanel(quitButton, menuPanel);
        }

        private void EnsureRightInfoPanel()
        {
            rightInfoPanel = FindChild(transform, "RightInfoPanel");
            if (rightInfoPanel == null)
            {
                rightInfoPanel = CreateUiObject("RightInfoPanel", transform, typeof(Image));
            }

            Image image = rightInfoPanel.GetComponent<Image>();
            if (image != null)
            {
                image.raycastTarget = false;
            }

            EnsureUniquePanelUnderRightInfo("AuthPanel");
            EnsureUniquePanelUnderRightInfo("StatsPanel");
        }

        private void EnsureUniquePanelUnderRightInfo(string panelName)
        {
            if (rightInfoPanel == null || string.IsNullOrWhiteSpace(panelName))
            {
                return;
            }

            List<Transform> matches = new List<Transform>();
            CollectChildrenByName(transform, panelName, matches);
            if (matches.Count == 0)
            {
                return;
            }

            Transform panelToKeep = null;
            for (int i = 0; i < matches.Count; i++)
            {
                if (matches[i] != null && matches[i].parent == rightInfoPanel.transform)
                {
                    panelToKeep = matches[i];
                    break;
                }
            }

            panelToKeep ??= matches[0];
            if (panelToKeep != null && panelToKeep.parent != rightInfoPanel.transform)
            {
                panelToKeep.SetParent(rightInfoPanel.transform, false);
            }

            for (int i = 0; i < matches.Count; i++)
            {
                Transform duplicate = matches[i];
                if (duplicate == null || duplicate == panelToKeep)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(duplicate.gameObject);
                }
                else
                {
                    DestroyImmediate(duplicate.gameObject);
                }
            }
        }

        private static void CollectChildrenByName(Transform parent, string name, List<Transform> result)
        {
            if (parent == null || result == null)
            {
                return;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (string.Equals(child.name, name, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(child);
                }

                CollectChildrenByName(child, name, result);
            }
        }

        private static void MoveButtonToMenuPanel(Button button, Transform menuPanel)
        {
            if (button == null || menuPanel == null)
            {
                return;
            }

            if (button.transform.parent != menuPanel)
            {
                button.transform.SetParent(menuPanel, false);
            }
        }

        private static void ApplyButtonLayout(Button button)
        {
            if (button == null)
            {
                return;
            }

            RectTransform rect = button.transform as RectTransform;
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(420f, 74f);
                rect.localScale = Vector3.one;
            }

            LayoutElement layoutElement = button.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = button.gameObject.AddComponent<LayoutElement>();
            }

            layoutElement.preferredWidth = 420f;
            layoutElement.preferredHeight = 74f;
            layoutElement.minWidth = 420f;
            layoutElement.minHeight = 74f;
            layoutElement.flexibleHeight = 0f;
            layoutElement.flexibleWidth = 0f;
        }

        private void ConfigurePrimaryNavigation()
        {
            if (startButton != null)
            {
                Navigation navigation = startButton.navigation;
                navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnDown = optionsButton;
                navigation.selectOnUp = null;
                navigation.selectOnLeft = null;
                navigation.selectOnRight = null;
                startButton.navigation = navigation;
            }

            if (optionsButton != null)
            {
                Navigation navigation = optionsButton.navigation;
                navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnUp = startButton;
                navigation.selectOnDown = quitButton;
                navigation.selectOnLeft = null;
                navigation.selectOnRight = null;
                optionsButton.navigation = navigation;
            }

            if (quitButton != null)
            {
                Navigation navigation = quitButton.navigation;
                navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnUp = optionsButton;
                navigation.selectOnDown = null;
                navigation.selectOnLeft = null;
                navigation.selectOnRight = null;
                quitButton.navigation = navigation;
            }
        }

        private void UpdateLogoGlitch()
        {
            Text title = FindText("Title");
            if (title == null)
            {
                return;
            }

            if (Time.unscaledTime >= nextLogoGlitchAt)
            {
                logoGlitchUntil = Time.unscaledTime + UnityEngine.Random.Range(0.05f, 0.09f);
                nextLogoGlitchAt = Time.unscaledTime + UnityEngine.Random.Range(2.2f, 4.1f);
            }

            Vector2 offset = Vector2.zero;
            if (Time.unscaledTime < logoGlitchUntil)
            {
                offset = new Vector2(UnityEngine.Random.Range(-1.5f, 1.5f), UnityEngine.Random.Range(-0.8f, 0.8f));
            }

            SyncTitleGhost(title, "TitleGhostCyan", new Vector2(2f, -1f) + offset, new Color(0.12f, 1f, 0.98f, 0.26f));
            SyncTitleGhost(title, "TitleGhostMagenta", new Vector2(-2f, 1f) - offset, new Color(1f, 0.16f, 0.72f, 0.24f));
        }

        private static void EnsureTextFx(Text text, Color outlineColor, Vector2 outlineDistance, Color shadowColor, Vector2 shadowDistance)
        {
            if (text == null)
            {
                return;
            }

            Outline outline = text.GetComponent<Outline>();
            if (outline == null)
            {
                outline = text.gameObject.AddComponent<Outline>();
            }

            outline.effectColor = outlineColor;
            outline.effectDistance = outlineDistance;
            outline.useGraphicAlpha = true;

            Shadow shadow = text.GetComponent<Shadow>();
            if (shadow == null)
            {
                shadow = text.gameObject.AddComponent<Shadow>();
            }

            shadow.effectColor = shadowColor;
            shadow.effectDistance = shadowDistance;
            shadow.useGraphicAlpha = true;
        }

        private static void EnsureDecorImage(Transform parent, string objectName, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            if (parent == null)
            {
                return;
            }

            GameObject child = FindChild(parent, objectName);
            if (child == null)
            {
                child = CreateUiObject(objectName, parent, typeof(Image));
                RectTransform rect = child.transform as RectTransform;
                if (rect != null)
                {
                    rect.anchorMin = anchorMin;
                    rect.anchorMax = anchorMax;
                    rect.offsetMin = offsetMin;
                    rect.offsetMax = offsetMax;
                }
            }

            Image image = child.GetComponent<Image>();
            if (image != null)
            {
                image.color = color;
                image.raycastTarget = false;
            }
        }

        private static void SetDecorActive(Transform parent, string objectName, bool active)
        {
            if (parent == null)
            {
                return;
            }

            GameObject child = FindChild(parent, objectName);
            if (child != null)
            {
                child.SetActive(active);
            }
        }

        private static void SetGraphicOutline(Graphic graphic, Color color, Vector2 distance)
        {
            if (graphic == null)
            {
                return;
            }

            Outline outline = graphic.GetComponent<Outline>();
            if (outline == null)
            {
                outline = graphic.gameObject.AddComponent<Outline>();
            }

            outline.effectColor = color;
            outline.effectDistance = distance;
            outline.useGraphicAlpha = true;
        }

        private void EnsureTitleGhost(Text source, string objectName, Color color, Vector2 offset)
        {
            if (source == null || source.transform.parent == null)
            {
                return;
            }

            Text ghost = FindText(objectName);
            if (ghost == null)
            {
                GameObject ghostObject = CreateUiObject(objectName, source.transform.parent, typeof(Text));
                ghost = ghostObject.GetComponent<Text>();
                ghost.transform.SetSiblingIndex(Mathf.Max(0, source.transform.GetSiblingIndex()));
            }

            ghost.font = source.font;
            ghost.fontSize = source.fontSize;
            ghost.fontStyle = FontStyle.Bold;
            ghost.alignment = source.alignment;
            ghost.color = color;
            ghost.text = source.text;
            ghost.raycastTarget = false;

            RectTransform sourceRect = source.transform as RectTransform;
            RectTransform ghostRect = ghost.transform as RectTransform;
            if (sourceRect != null && ghostRect != null)
            {
                ghostRect.anchorMin = sourceRect.anchorMin;
                ghostRect.anchorMax = sourceRect.anchorMax;
                ghostRect.pivot = sourceRect.pivot;
                ghostRect.anchoredPosition = sourceRect.anchoredPosition + offset;
                ghostRect.sizeDelta = sourceRect.sizeDelta;
            }
        }

        private void SyncTitleGhosts(Text source)
        {
            if (source == null)
            {
                return;
            }

            SyncTitleGhost(source, "TitleGhostCyan", new Vector2(8f, -2f), new Color(0.12f, 1f, 0.98f, 0.55f));
            SyncTitleGhost(source, "TitleGhostMagenta", new Vector2(-8f, 2f), new Color(1f, 0.16f, 0.72f, 0.52f));
        }

        private void SyncTitleGhost(Text source, string objectName, Vector2 offset, Color color)
        {
            Text ghost = FindText(objectName);
            if (ghost == null)
            {
                return;
            }

            ghost.font = source.font;
            ghost.fontSize = source.fontSize;
            ghost.fontStyle = FontStyle.Bold;
            ghost.alignment = source.alignment;
            ghost.text = source.text;
            ghost.color = color;
            ghost.raycastTarget = false;

            RectTransform sourceRect = source.transform as RectTransform;
            RectTransform ghostRect = ghost.transform as RectTransform;
            if (sourceRect != null && ghostRect != null)
            {
                ghostRect.anchorMin = sourceRect.anchorMin;
                ghostRect.anchorMax = sourceRect.anchorMax;
                ghostRect.pivot = sourceRect.pivot;
                ghostRect.anchoredPosition = sourceRect.anchoredPosition + offset;
                ghostRect.sizeDelta = sourceRect.sizeDelta;
            }
        }

        private static void StyleInputField(InputField inputField)
        {
            if (inputField == null)
            {
                return;
            }

            Image image = inputField.GetComponent<Image>();
            if (image != null)
            {
                image.color = new Color(0.03f, 0.08f, 0.14f, 0.92f);
            }

            EnsureDecorImage(inputField.transform, "TopAccent", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(8f, -2f), new Vector2(-18f, 0f), new Color(0.12f, 1f, 0.98f, 0.86f));
            EnsureDecorImage(inputField.transform, "BottomAccent", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(20f, 0f), new Vector2(-8f, 2f), new Color(1f, 0.22f, 0.72f, 0.6f));

            Text text = inputField.textComponent;
            if (text != null)
            {
                text.color = new Color(0.92f, 0.98f, 1f, 1f);
                EnsureTextFx(text, new Color(0.02f, 0.04f, 0.08f, 0.88f), new Vector2(1f, -1f), new Color(0.12f, 1f, 0.98f, 0.2f), new Vector2(0f, -1f));
            }

            Text placeholder = inputField.placeholder as Text;
            if (placeholder != null)
            {
                placeholder.color = new Color(0.6f, 0.76f, 0.84f, 0.76f);
                EnsureTextFx(placeholder, new Color(0.01f, 0.03f, 0.06f, 0.75f), new Vector2(1f, -1f), new Color(1f, 0.2f, 0.72f, 0.15f), new Vector2(-1f, 0f));
            }
        }

        private void EnsureFooterHints()
        {
            Font font = ResolveBuiltinFont();
            Text footerText = FindText("FooterHints");
            if (footerText == null)
            {
                GameObject footerObject = CreateUiObject("FooterHints", transform, typeof(Text));

                RectTransform rect = footerObject.transform as RectTransform;
                if (rect != null)
                {
                    rect.anchorMin = new Vector2(0f, 0f);
                    rect.anchorMax = new Vector2(1f, 0f);
                    rect.pivot = new Vector2(0.5f, 0f);
                    rect.anchoredPosition = new Vector2(0f, 28f);
                    rect.sizeDelta = new Vector2(-100f, 28f);
                }

                footerText = footerObject.GetComponent<Text>();
            }

            footerText.font = font;
            footerText.fontSize = 18;
            footerText.alignment = TextAnchor.MiddleCenter;
            footerText.color = new Color(0.5f, 0.92f, 1f, 0.9f);
            footerText.text = footerHintText;
            footerText.raycastTarget = false;

            EnsureFooterLink(font);
        }

        private void LayoutBackgroundOverlay()
        {
            EnsureBackgroundOverlay();
            if (backgroundOverlay == null)
            {
                return;
            }

            RectTransform rect = backgroundOverlay.transform as RectTransform;
            if (rect != null)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }

            backgroundOverlay.color = new Color(0f, 0f, 0f, 0.3f);
        }

        private void LayoutBranding(bool compact)
        {
            Text title = FindText("Title");
            if (title != null)
            {
                RectTransform rect = title.transform as RectTransform;
                if (rect != null)
                {
                    rect.anchorMin = new Vector2(0.5f, 1f);
                    rect.anchorMax = new Vector2(0.5f, 1f);
                    rect.pivot = new Vector2(0.5f, 1f);
                    rect.anchoredPosition = new Vector2(0f, -95f);
                    rect.sizeDelta = compact ? new Vector2(560f, 136f) : new Vector2(620f, 150f);
                }

                title.alignment = TextAnchor.MiddleCenter;
                title.fontSize = compact ? 74 : 84;
                SyncTitleGhosts(title);
            }

            Transform subtitleFrame = FindChild(transform, "SubtitleFrame")?.transform;
            if (subtitleFrame != null)
            {
                RectTransform rect = subtitleFrame as RectTransform;
                if (rect != null)
                {
                    rect.anchorMin = new Vector2(0.5f, 1f);
                    rect.anchorMax = new Vector2(0.5f, 1f);
                    rect.pivot = new Vector2(0.5f, 1f);
                    rect.anchoredPosition = new Vector2(0f, -178f);
                    rect.sizeDelta = compact ? new Vector2(340f, 28f) : new Vector2(390f, 30f);
                }
            }

            Text subtitle = FindText("Subtitle");
            if (subtitle != null)
            {
                RectTransform rect = subtitle.transform as RectTransform;
                if (rect != null)
                {
                    rect.anchorMin = new Vector2(0.5f, 1f);
                    rect.anchorMax = new Vector2(0.5f, 1f);
                    rect.pivot = new Vector2(0.5f, 1f);
                    rect.anchoredPosition = new Vector2(0f, -178f);
                    rect.sizeDelta = compact ? new Vector2(340f, 28f) : new Vector2(390f, 30f);
                }

                subtitle.alignment = TextAnchor.MiddleCenter;
                subtitle.fontSize = compact ? 14 : 16;
            }
        }

        private void LayoutMenuColumn()
        {
            EnsureCenterMenuLayout();

            if (menuLayoutRoot != null)
            {
                menuLayoutRoot.anchorMin = new Vector2(0.5f, 0.5f);
                menuLayoutRoot.anchorMax = new Vector2(0.5f, 0.5f);
                menuLayoutRoot.pivot = new Vector2(0.5f, 0.5f);
                menuLayoutRoot.anchoredPosition = Vector2.zero;
                menuLayoutRoot.sizeDelta = new Vector2(420f, 274f);
                menuLayoutRoot.localScale = Vector3.one;
            }

            ApplyButtonLayout(startButton);
            ApplyButtonLayout(optionsButton);
            ApplyButtonLayout(quitButton);
        }

        private void LayoutLeaderboardPanel(bool compact)
        {
            Transform panel = FindChild(transform, "LeaderboardPanel")?.transform;
            if (panel == null)
            {
                return;
            }

            RectTransform rect = panel as RectTransform;
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0f, 0.5f);
                rect.anchorMax = new Vector2(0f, 0.5f);
                rect.pivot = new Vector2(0f, 0.5f);
                rect.anchoredPosition = new Vector2(120f, 0f);
                rect.sizeDelta = compact ? new Vector2(270f, 238f) : new Vector2(290f, 250f);
                rect.localScale = Vector3.one;
            }

            if (leaderboardTitleText != null)
            {
                leaderboardTitleText.fontSize = compact ? 18 : 20;
            }

            for (int i = 0; i < leaderboardNameTexts.Count; i++)
            {
                if (leaderboardNameTexts[i] != null)
                {
                    leaderboardNameTexts[i].fontSize = compact ? 18 : 20;
                }
            }

            for (int i = 0; i < leaderboardScoreTexts.Count; i++)
            {
                if (leaderboardScoreTexts[i] != null)
                {
                    leaderboardScoreTexts[i].fontSize = compact ? 18 : 20;
                }
            }
        }

        private void LayoutAuthAndStatsPanels(bool compact)
        {
            EnsureRightInfoPanel();
            if (rightInfoPanel == null)
            {
                return;
            }

            RectTransform rightRect = rightInfoPanel.transform as RectTransform;
            if (rightRect != null)
            {
                rightRect.anchorMin = new Vector2(1f, 0.5f);
                rightRect.anchorMax = new Vector2(1f, 0.5f);
                rightRect.pivot = new Vector2(1f, 0.5f);
                rightRect.anchoredPosition = new Vector2(-120f, 0f);
                rightRect.sizeDelta = compact ? new Vector2(300f, 308f) : new Vector2(320f, 320f);
                rightRect.localScale = Vector3.one;
            }

            Transform authPanel = FindChild(transform, "AuthPanel")?.transform;
            if (authPanel != null)
            {
                RectTransform rect = authPanel as RectTransform;
                if (rect != null)
                {
                    rect.anchorMin = new Vector2(0f, 1f);
                    rect.anchorMax = new Vector2(1f, 1f);
                    rect.pivot = new Vector2(0.5f, 1f);
                    rect.anchoredPosition = new Vector2(0f, -18f);
                    rect.sizeDelta = new Vector2(-28f, compact ? 186f : 194f);
                }

                LayoutAuthPanelContents(authPanel, true);
            }

            Transform statsPanel = FindChild(transform, "StatsPanel")?.transform;
            if (statsPanel != null)
            {
                RectTransform rect = statsPanel as RectTransform;
                if (rect != null)
                {
                    rect.anchorMin = new Vector2(0f, 0f);
                    rect.anchorMax = new Vector2(1f, 0f);
                    rect.pivot = new Vector2(0.5f, 0f);
                    rect.anchoredPosition = new Vector2(0f, 14f);
                    rect.sizeDelta = new Vector2(-28f, 108f);
                }

                LayoutStatsPanelContents(statsPanel, true);
            }
        }

        private void LayoutFooter(bool compact)
        {
            Text footerText = FindText("FooterHints");
            if (footerText != null)
            {
                RectTransform rect = footerText.transform as RectTransform;
                if (rect != null)
                {
                    rect.anchorMin = new Vector2(0.5f, 0f);
                    rect.anchorMax = new Vector2(0.5f, 0f);
                    rect.pivot = new Vector2(0.5f, 0f);
                    rect.anchoredPosition = new Vector2(0f, 18f);
                    rect.sizeDelta = new Vector2(640f, 20f);
                }

                footerText.fontSize = compact ? 11 : 12;
                footerText.horizontalOverflow = HorizontalWrapMode.Overflow;
                footerText.verticalOverflow = VerticalWrapMode.Overflow;
            }

            if (footerLinkButton != null)
            {
                RectTransform rect = footerLinkButton.transform as RectTransform;
                if (rect != null)
                {
                    rect.anchorMin = new Vector2(0.5f, 0f);
                    rect.anchorMax = new Vector2(0.5f, 0f);
                    rect.pivot = new Vector2(0.5f, 0f);
                    rect.anchoredPosition = new Vector2(0f, 38f);
                    rect.sizeDelta = new Vector2(260f, 24f);
                }

                Text linkText = footerLinkButton.GetComponentInChildren<Text>(true);
                if (linkText != null)
                {
                    linkText.fontSize = compact ? 11 : 12;
                }
            }
        }

        private void StyleBranding()
        {
            Text title = FindText("Title");
            if (title != null)
            {
                title.color = new Color(0.96f, 0.98f, 1f, 0.98f);
                title.fontStyle = FontStyle.Bold;
                EnsureTextFx(title, new Color(0f, 0.02f, 0.05f, 0.96f), new Vector2(2f, -2f), new Color(0.12f, 1f, 0.98f, 0.18f), new Vector2(0f, -1f));
                EnsureTitleGhost(title, "TitleGhostCyan", new Color(0.12f, 1f, 0.98f, 0.22f), new Vector2(2f, -1f));
                EnsureTitleGhost(title, "TitleGhostMagenta", new Color(1f, 0.16f, 0.72f, 0.2f), new Vector2(-2f, 1f));
                SyncTitleGhosts(title);
            }

            Text subtitle = FindText("Subtitle");
            if (subtitle != null)
            {
                subtitle.color = new Color(0.84f, 0.92f, 0.98f, 0.92f);
                subtitle.fontStyle = FontStyle.Bold;
                EnsureTextFx(subtitle, new Color(0.01f, 0.02f, 0.06f, 0.92f), new Vector2(1f, -1f), new Color(0.12f, 1f, 0.98f, 0.08f), new Vector2(0f, -1f));
            }

            GameObject subtitleFrame = FindChild(transform, "SubtitleFrame");
            if (subtitleFrame != null)
            {
                Image frameImage = subtitleFrame.GetComponent<Image>();
                if (frameImage == null)
                {
                    frameImage = subtitleFrame.AddComponent<Image>();
                }

                frameImage.color = new Color(0.01f, 0.04f, 0.08f, 0.18f);
                SetGraphicOutline(frameImage, new Color(0.12f, 1f, 0.98f, 0.18f), new Vector2(1f, -1f));
                EnsureDecorImage(subtitleFrame.transform, "CornerAccent", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -10f), new Vector2(12f, 0f), new Color(1f, 0.2f, 0.72f, 0.76f));
                SetDecorActive(subtitleFrame.transform, "TopAccent", false);
                SetDecorActive(subtitleFrame.transform, "BottomAccent", false);
            }
        }

        private void StyleMenuFrame()
        {
            GameObject menuPanel = FindChild(transform, "MenuPanel");
            if (menuPanel == null)
            {
                return;
            }

            Image image = menuPanel.GetComponent<Image>();
            if (image == null)
            {
                image = menuPanel.AddComponent<Image>();
            }

            image.color = new Color(0.01f, 0.03f, 0.07f, 0.08f);
            SetGraphicOutline(image, new Color(0.12f, 1f, 0.98f, 0.12f), new Vector2(1f, -1f));
            SetDecorActive(menuPanel.transform, "TopAccent", false);
            SetDecorActive(menuPanel.transform, "BottomAccent", false);
            SetDecorActive(menuPanel.transform, "RightAccent", false);
            SetDecorActive(menuPanel.transform, "CornerNode", false);
        }

        private void StylePanelShell(GameObject panelObject, Color primary, Color secondary, float backgroundAlpha)
        {
            if (panelObject == null)
            {
                return;
            }

            Image panelImage = panelObject.GetComponent<Image>();
            if (panelImage != null)
            {
                panelImage.color = new Color(0.01f, 0.04f, 0.08f, backgroundAlpha);
                SetGraphicOutline(panelImage, primary, new Vector2(1f, -1f));
            }

            SetDecorActive(panelObject.transform, "TopAccent", false);
            SetDecorActive(panelObject.transform, "BottomAccent", false);
            SetDecorActive(panelObject.transform, "SideAccent", false);
            EnsureDecorImage(panelObject.transform, "CornerTopLeft", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -12f), new Vector2(14f, 0f), secondary);
            SetDecorActive(panelObject.transform, "CornerBottomRight", false);
        }

        private void StyleSubPanel(GameObject panelObject)
        {
            if (panelObject == null)
            {
                return;
            }

            Image image = panelObject.GetComponent<Image>();
            if (image != null)
            {
                image.color = new Color(0f, 0f, 0f, 0f);
            }
        }

        private void StyleFooter()
        {
            Text footerText = FindText("FooterHints");
            if (footerText != null)
            {
                footerText.color = new Color(0.84f, 0.9f, 0.96f, 0.72f);
                EnsureTextFx(footerText, new Color(0.01f, 0.02f, 0.07f, 0.75f), new Vector2(1f, -1f), new Color(0f, 0f, 0f, 0f), Vector2.zero);
            }

            if (footerLinkButton != null)
            {
                Image image = footerLinkButton.GetComponent<Image>();
                if (image != null)
                {
                    image.color = new Color(0.02f, 0.04f, 0.06f, 0.22f);
                    SetGraphicOutline(image, new Color(0.12f, 1f, 0.98f, 0.14f), new Vector2(1f, -1f));
                }

                SetDecorActive(footerLinkButton.transform, "TopAccent", false);
                SetDecorActive(footerLinkButton.transform, "BottomAccent", false);
                SetDecorActive(footerLinkButton.transform, "SideAccent", false);

                Text linkLabel = footerLinkButton.GetComponentInChildren<Text>(true);
                if (linkLabel != null)
                {
                    linkLabel.color = new Color(0.86f, 0.92f, 0.98f, 0.7f);
                    EnsureTextFx(linkLabel, new Color(0.01f, 0.03f, 0.06f, 0.75f), new Vector2(1f, -1f), new Color(0f, 0f, 0f, 0f), Vector2.zero);
                }
            }
        }

        private void EnsureFooterLink(Font font)
        {
            footerLinkButton = FindButton("FooterLinkButton");
            if (footerLinkButton == null)
            {
                GameObject linkObject = CreateUiObject("FooterLinkButton", transform, typeof(Image), typeof(Button));

                RectTransform rect = linkObject.transform as RectTransform;
                if (rect != null)
                {
                    rect.anchorMin = new Vector2(0.5f, 0f);
                    rect.anchorMax = new Vector2(0.5f, 0f);
                    rect.pivot = new Vector2(0.5f, 0f);
                    rect.anchoredPosition = new Vector2(0f, 58f);
                    rect.sizeDelta = new Vector2(460f, 34f);
                }

                Image image = linkObject.GetComponent<Image>();
                image.color = new Color(0.05f, 0.14f, 0.2f, 0.55f);

                footerLinkButton = linkObject.GetComponent<Button>();
                ColorBlock colors = footerLinkButton.colors;
                colors.normalColor = new Color(0.05f, 0.14f, 0.2f, 0.55f);
                colors.highlightedColor = new Color(0.12f, 0.3f, 0.42f, 0.92f);
                colors.selectedColor = new Color(0.15f, 0.38f, 0.52f, 0.92f);
                colors.pressedColor = new Color(0.04f, 0.18f, 0.25f, 0.95f);
                colors.disabledColor = new Color(0.15f, 0.15f, 0.15f, 0.3f);
                colors.fadeDuration = 0.08f;
                footerLinkButton.colors = colors;

                Text label = CreateStatsText(linkObject.transform, font, "Label", websiteLabel, TextAnchor.MiddleCenter, new Color(0.76f, 0.96f, 1f, 1f));
                label.fontSize = 18;
                RectTransform labelRect = label.transform as RectTransform;
                if (labelRect != null)
                {
                    labelRect.anchorMin = Vector2.zero;
                    labelRect.anchorMax = Vector2.one;
                    labelRect.offsetMin = Vector2.zero;
                    labelRect.offsetMax = Vector2.zero;
                }
            }

            Text linkLabel = footerLinkButton.GetComponentInChildren<Text>(true);
            if (linkLabel != null)
            {
                linkLabel.font = font;
                linkLabel.fontSize = 18;
                linkLabel.alignment = TextAnchor.MiddleCenter;
                linkLabel.color = new Color(0.76f, 0.96f, 1f, 1f);
                linkLabel.text = websiteLabel;
                linkLabel.raycastTarget = false;
            }
        }

        private void LayoutAuthPanelContents(Transform authPanel, bool compact)
        {
            SetTopAnchoredRect(FindChild(authPanel, "AuthHeader"), 6f, 28f);
            SetTopAnchoredRect(FindChild(authPanel, "AuthStatus"), 34f, 24f);
            SetTopAnchoredRect(FindChild(authPanel, "AuthHint"), 58f, compact ? 30f : 36f);
            SetTopAnchoredRect(authUsernameRow, 92f, 24f);
            SetTopAnchoredRect(authIdentifierRow, 120f, 24f);
            SetTopAnchoredRect(authPasswordRow, 148f, 24f);

            if (authMessageText != null)
            {
                RectTransform messageRect = authMessageText.transform as RectTransform;
                if (messageRect != null)
                {
                    messageRect.anchorMin = new Vector2(0f, 0f);
                    messageRect.anchorMax = new Vector2(1f, 0f);
                    messageRect.pivot = new Vector2(0.5f, 0f);
                    messageRect.anchoredPosition = new Vector2(0f, 42f);
                    messageRect.sizeDelta = new Vector2(-20f, 22f);
                }

                authMessageText.fontSize = 11;
                authMessageText.horizontalOverflow = HorizontalWrapMode.Wrap;
            }

            PositionActionButton(authSignInButton, new Vector2(0.05f, 0f), new Vector2(0.47f, 0f), 8f, 34f);
            PositionActionButton(authRegisterButton, new Vector2(0.53f, 0f), new Vector2(0.95f, 0f), 8f, 34f);
            PositionActionButton(authSignOutButton, new Vector2(0.05f, 0f), new Vector2(0.95f, 0f), 8f, 34f);

            SetAuthFontSizes(17, 11, 11, 11);
        }

        private void LayoutStatsPanelContents(Transform statsPanel, bool compact)
        {
            SetTopAnchoredRect(FindChild(statsPanel, "StatsHeader"), 2f, 24f);
            SetTopAnchoredRect(FindChild(statsPanel, "TotalRunsRow"), 28f, 18f);
            SetTopAnchoredRect(FindChild(statsPanel, "TotalDeathsRow"), 48f, 18f);
            SetTopAnchoredRect(FindChild(statsPanel, "TopScoreRow"), 68f, 18f);
            SetTopAnchoredRect(FindChild(statsPanel, "TopHeightRow"), 88f, 18f);

            SetStatsFontSizes(16, 13, 13);
        }

        private void SetAuthFontSizes(int headerSize, int statusSize, int bodySize, int inputSize)
        {
            if (authHeaderText != null) authHeaderText.fontSize = headerSize;
            if (authStatusText != null) authStatusText.fontSize = statusSize;
            if (authHintText != null) authHintText.fontSize = bodySize;
            if (authMessageText != null) authMessageText.fontSize = bodySize;
            SetInputRowFontSize(authUsernameRow, bodySize, inputSize);
            SetInputRowFontSize(authIdentifierRow, bodySize, inputSize);
            SetInputRowFontSize(authPasswordRow, bodySize, inputSize);
            SetButtonLabelFontSize(authSignInButton, 16);
            SetButtonLabelFontSize(authRegisterButton, 16);
            SetButtonLabelFontSize(authSignOutButton, 16);
        }

        private void SetStatsFontSizes(int headerSize, int labelSize, int valueSize)
        {
            if (statsHeaderText != null) statsHeaderText.fontSize = headerSize;
            SetStatsRowFontSize("TotalRunsRow", labelSize, valueSize);
            SetStatsRowFontSize("TotalDeathsRow", labelSize, valueSize);
            SetStatsRowFontSize("TopScoreRow", labelSize, valueSize);
            SetStatsRowFontSize("TopHeightRow", labelSize, valueSize);
        }

        private void SetStatsRowFontSize(string rowName, int labelSize, int valueSize)
        {
            GameObject rowObject = FindChild(transform, rowName) ?? FindChild(statsHeaderText != null ? statsHeaderText.transform.parent : null, rowName);
            if (rowObject == null)
            {
                return;
            }

            Text[] texts = rowObject.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i].name.EndsWith("Label", StringComparison.OrdinalIgnoreCase))
                {
                    texts[i].fontSize = labelSize;
                }
                else if (texts[i].name.EndsWith("Value", StringComparison.OrdinalIgnoreCase))
                {
                    texts[i].fontSize = valueSize;
                }
            }
        }

        private static void SetInputRowFontSize(GameObject rowObject, int labelSize, int inputSize)
        {
            if (rowObject == null)
            {
                return;
            }

            Text[] texts = rowObject.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i].name.EndsWith("Label", StringComparison.OrdinalIgnoreCase))
                {
                    texts[i].fontSize = labelSize;
                }
                else if (texts[i].name == "Placeholder" || texts[i].name == "Text")
                {
                    texts[i].fontSize = inputSize;
                }
            }
        }

        private static void SetButtonLabelFontSize(Button button, int size)
        {
            if (button == null)
            {
                return;
            }

            Text label = button.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.fontSize = size;
            }
        }

        public void OpenWebsiteLink()
        {
            if (string.IsNullOrWhiteSpace(websiteUrl))
            {
                return;
            }

            Application.OpenURL(websiteUrl);
        }

        private void EnsureAuthPanel()
        {
            Transform existing = FindChild(transform, "AuthPanel")?.transform;
            if (existing != null)
            {
                CacheAuthControls(existing);
                return;
            }

            Font font = ResolveBuiltinFont();
            GameObject panelObject = CreateUiObject("AuthPanel", transform, typeof(Image));

            RectTransform panelRect = panelObject.transform as RectTransform;
            if (panelRect != null)
            {
                panelRect.anchorMin = new Vector2(1f, 1f);
                panelRect.anchorMax = new Vector2(1f, 1f);
                panelRect.pivot = new Vector2(1f, 1f);
                panelRect.anchoredPosition = new Vector2(-84f, -84f);
                panelRect.sizeDelta = new Vector2(360f, 300f);
            }

            Image panelImage = panelObject.GetComponent<Image>();
            panelImage.color = new Color(0.03f, 0.08f, 0.12f, 0.86f);

            authHeaderText = CreatePanelHeader(panelObject.transform, font, "AuthHeader", "ACCOUNT");
            authStatusText = CreatePanelBodyText(panelObject.transform, font, "AuthStatus", "STATUS: GUEST", 62f, 20, FontStyle.Bold, new Color(0.55f, 0.96f, 1f, 0.98f));
            authHintText = CreatePanelBodyText(panelObject.transform, font, "AuthHint", authGuestHintText, 96f, 16, FontStyle.Normal, new Color(0.75f, 0.88f, 0.96f, 0.9f));
            authHintText.horizontalOverflow = HorizontalWrapMode.Wrap;

            authUsernameRow = CreateInputFieldRow(panelObject.transform, font, "AuthUsernameRow", "USERNAME", 162f, false, out authUsernameInput);
            authIdentifierRow = CreateInputFieldRow(panelObject.transform, font, "AuthIdentifierRow", "EMAIL / LOGIN", 206f, false, out authIdentifierInput);
            authPasswordRow = CreateInputFieldRow(panelObject.transform, font, "AuthPasswordRow", "PASSWORD", 250f, true, out authPasswordInput);

            authSignInButton = CreateActionButton(panelObject.transform, font, "AuthSignInButton", "SIGN IN", new Vector2(0.05f, 0f), new Vector2(0.47f, 0f), new Vector2(0f, 18f), new Vector2(0f, 58f));
            authRegisterButton = CreateActionButton(panelObject.transform, font, "AuthRegisterButton", "REGISTER", new Vector2(0.53f, 0f), new Vector2(0.95f, 0f), new Vector2(0f, 18f), new Vector2(0f, 58f));
            authSignOutButton = CreateActionButton(panelObject.transform, font, "AuthSignOutButton", "SIGN OUT", new Vector2(0.05f, 0f), new Vector2(0.95f, 0f), new Vector2(0f, 18f), new Vector2(0f, 58f));

            authMessageText = CreatePanelBodyText(panelObject.transform, font, "AuthMessage", authFeedbackMessage, 0f, 15, FontStyle.Normal, new Color(0.98f, 0.84f, 0.5f, 0.96f));
            RectTransform messageRect = authMessageText.transform as RectTransform;
            if (messageRect != null)
            {
                messageRect.anchorMin = new Vector2(0f, 0f);
                messageRect.anchorMax = new Vector2(1f, 0f);
                messageRect.pivot = new Vector2(0.5f, 0f);
                messageRect.anchoredPosition = new Vector2(0f, 62f);
                messageRect.sizeDelta = new Vector2(-36f, 42f);
            }

            RefreshAuthPanel();
        }

        private void CacheAuthControls(Transform authPanel)
        {
            authHeaderText = FindTextUnder(authPanel, "AuthHeader");
            authStatusText = FindTextUnder(authPanel, "AuthStatus");
            authHintText = FindTextUnder(authPanel, "AuthHint");
            authMessageText = FindTextUnder(authPanel, "AuthMessage");
            authUsernameRow = FindChild(authPanel, "AuthUsernameRow");
            authIdentifierRow = FindChild(authPanel, "AuthIdentifierRow");
            authPasswordRow = FindChild(authPanel, "AuthPasswordRow");
            authUsernameInput = FindComponentUnder<InputField>(authPanel, "AuthUsernameInput");
            authIdentifierInput = FindComponentUnder<InputField>(authPanel, "AuthIdentifierInput");
            authPasswordInput = FindComponentUnder<InputField>(authPanel, "AuthPasswordInput");
            authSignInButton = FindComponentUnder<Button>(authPanel, "AuthSignInButton");
            authRegisterButton = FindComponentUnder<Button>(authPanel, "AuthRegisterButton");
            authSignOutButton = FindComponentUnder<Button>(authPanel, "AuthSignOutButton");
        }

        private void RefreshAuthPanel()
        {
            EnsureAuthPanel();

            authService ??= AuthRuntime.Service;
            AuthSessionData session = authService.CurrentSession ?? AuthSessionData.CreateGuest();
            session.Sanitize();

            bool isGuest = session.isGuest;
            if (authHeaderText != null)
            {
                authHeaderText.text = isGuest ? "ACCOUNT" : "CONNECTED ACCOUNT";
            }

            if (authStatusText != null)
            {
                authStatusText.text = isGuest ? "STATUS: GUEST" : $"STATUS: {session.DisplayName.ToUpperInvariant()}";
            }

            if (authHintText != null)
            {
                authHintText.text = isGuest ? authGuestHintText : authConnectedHintText;
            }

            if (authUsernameRow != null) authUsernameRow.SetActive(isGuest);
            if (authIdentifierRow != null) authIdentifierRow.SetActive(isGuest);
            if (authPasswordRow != null) authPasswordRow.SetActive(isGuest);
            if (authSignInButton != null) authSignInButton.gameObject.SetActive(isGuest);
            if (authRegisterButton != null) authRegisterButton.gameObject.SetActive(isGuest);
            if (authSignOutButton != null) authSignOutButton.gameObject.SetActive(!isGuest);

            if (authMessageText != null)
            {
                string fallbackMessage = isGuest
                    ? "Sign in with your nekuzaky.com account."
                    : $"EMAIL: {(string.IsNullOrWhiteSpace(session.email) ? "NOT SET" : session.email)}";
                authMessageText.text = string.IsNullOrWhiteSpace(authFeedbackMessage) ? fallbackMessage : authFeedbackMessage;
            }

            if (authUsernameInput != null)
            {
                authUsernameInput.interactable = isGuest;
            }

            if (authIdentifierInput != null)
            {
                authIdentifierInput.interactable = isGuest;
            }

            if (authPasswordInput != null)
            {
                authPasswordInput.interactable = isGuest;
            }

            ConfigureButtonNavigation();
        }

        private async void HandleSignInPressed()
        {
            if (isAuthRequestInFlight)
            {
                return;
            }

            authService ??= AuthRuntime.Service;
            isAuthRequestInFlight = true;
            SetAuthButtonsInteractable(false);

            if (authMessageText != null)
            {
                authMessageText.text = "Signing in...";
            }

            try
            {
                AuthOperationResult result = await authService.SignInAsync(
                    authIdentifierInput != null ? authIdentifierInput.text : string.Empty,
                    authPasswordInput != null ? authPasswordInput.text : string.Empty);
                HandleAuthOperationResult(result);
            }
            finally
            {
                isAuthRequestInFlight = false;
                SetAuthButtonsInteractable(true);
            }
        }

        private void HandleRegisterPressed()
        {
            authFeedbackMessage = "Create your account on nekuzaky.com, then return here and sign in.";
            OpenWebsiteLink();
            RefreshAuthPanel();
        }

        private async void HandleSignOutPressed()
        {
            if (isAuthRequestInFlight)
            {
                return;
            }

            authService ??= AuthRuntime.Service;
            isAuthRequestInFlight = true;
            SetAuthButtonsInteractable(false);
            try
            {
                await authService.SignOutAsync();
            }
            finally
            {
                isAuthRequestInFlight = false;
                SetAuthButtonsInteractable(true);
            }
        }

        private void HandleAuthOperationResult(AuthOperationResult result)
        {
            if (result == null)
            {
                authFeedbackMessage = "Authentication service did not return a result.";
                RefreshAuthPanel();
                return;
            }

            authFeedbackMessage = string.IsNullOrWhiteSpace(result.message)
                ? (result.success ? "Authentication successful." : "Authentication failed.")
                : result.message;

            if (result.success)
            {
                ClearAuthInputs(clearUsername: false);
                _ = RefreshOnlinePanelsAsync();
            }

            RefreshAuthPanel();
            RefreshLeaderboardPanel();
            RefreshStatsPanel();
        }

        private void HandleAuthSessionChanged(AuthSessionData session)
        {
            authFeedbackMessage = session != null && !session.isGuest
                ? $"Session active for {session.DisplayName}."
                : "Signed out. You are now playing as guest.";

            ClearAuthInputs(clearUsername: false);
            RefreshAuthPanel();
            RefreshLeaderboardPanel();
            RefreshStatsPanel();
            _ = RefreshOnlinePanelsAsync();
        }

        private void SetAuthButtonsInteractable(bool interactable)
        {
            if (authSignInButton != null)
            {
                authSignInButton.interactable = interactable;
            }

            if (authRegisterButton != null)
            {
                authRegisterButton.interactable = interactable;
            }

            if (authSignOutButton != null)
            {
                authSignOutButton.interactable = interactable;
            }
        }

        private async Task RefreshOnlinePanelsAsync()
        {
            if (isOnlineRefreshInFlight)
            {
                return;
            }

            authService ??= AuthRuntime.Service;
            onlineService ??= MindriftOnlineService.Instance;
            if (authService == null || onlineService == null)
            {
                return;
            }

            AuthSessionData session = authService.CurrentSession ?? AuthSessionData.CreateGuest();
            if (session.isGuest)
            {
                isOnlineRefreshInFlight = true;
                try
                {
                    ApiRequestResult<LeaderboardResponseData> guestLeaderboardResult = await onlineService.FetchLeaderboardAsync();
                    if (!guestLeaderboardResult.Success && string.IsNullOrWhiteSpace(authFeedbackMessage))
                    {
                        authFeedbackMessage = "Failed to load leaderboard.";
                    }
                }
                finally
                {
                    isOnlineRefreshInFlight = false;
                }

                RefreshStatsPanel();
                RefreshLeaderboardPanel();
                return;
            }

            isOnlineRefreshInFlight = true;
            try
            {
                ApiRequestResult<MindriftStatsDto> statsResult = await onlineService.FetchMyStatsAsync();
                if (!statsResult.Success && statsResult.IsUnauthorized)
                {
                    authFeedbackMessage = "Session expired. Please sign in again.";
                }

                ApiRequestResult<LeaderboardResponseData> leaderboardResult = await onlineService.FetchLeaderboardAsync();
                if (!leaderboardResult.Success && !leaderboardResult.IsUnauthorized && string.IsNullOrWhiteSpace(authFeedbackMessage))
                {
                    authFeedbackMessage = "Failed to load leaderboard.";
                }
            }
            finally
            {
                isOnlineRefreshInFlight = false;
                RefreshAuthPanel();
                RefreshStatsPanel();
                RefreshLeaderboardPanel();
            }
        }

        private void ClearAuthInputs(bool clearUsername)
        {
            if (clearUsername && authUsernameInput != null)
            {
                authUsernameInput.text = string.Empty;
            }

            if (authIdentifierInput != null)
            {
                authIdentifierInput.text = string.Empty;
            }

            if (authPasswordInput != null)
            {
                authPasswordInput.text = string.Empty;
            }
        }

        private void EnsureStatsPanel()
        {
            Transform existing = FindChild(transform, "StatsPanel")?.transform;
            if (existing != null)
            {
                CacheStatsTexts(existing);
                return;
            }

            Font font = ResolveBuiltinFont();

            GameObject panelObject = CreateUiObject("StatsPanel", transform, typeof(Image));

            RectTransform panelRect = panelObject.transform as RectTransform;
            if (panelRect != null)
            {
                panelRect.anchorMin = new Vector2(1f, 0.5f);
                panelRect.anchorMax = new Vector2(1f, 0.5f);
                panelRect.pivot = new Vector2(1f, 0.5f);
                panelRect.anchoredPosition = new Vector2(-84f, -18f);
                panelRect.sizeDelta = new Vector2(360f, 260f);
            }

            Image panelImage = panelObject.GetComponent<Image>();
            panelImage.color = new Color(0.03f, 0.08f, 0.12f, 0.82f);

            statsHeaderText = CreatePanelHeader(panelObject.transform, font, "StatsHeader", "LOCAL PROFILE");

            totalRunsValueText = CreateStatsRow(panelObject.transform, font, "Total Runs", 82f);
            totalDeathsValueText = CreateStatsRow(panelObject.transform, font, "Total Deaths", 126f);
            topScoreValueText = CreateStatsRow(panelObject.transform, font, "Top Score", 170f);
            topHeightValueText = CreateStatsRow(panelObject.transform, font, "Top Height", 214f);
        }

        private void CacheStatsTexts(Transform statsPanel)
        {
            statsHeaderText = FindTextUnder(statsPanel, "StatsHeader");
            totalRunsValueText = FindTextUnder(statsPanel, "TotalRunsValue");
            totalDeathsValueText = FindTextUnder(statsPanel, "TotalDeathsValue");
            topScoreValueText = FindTextUnder(statsPanel, "TopScoreValue");
            topHeightValueText = FindTextUnder(statsPanel, "TopHeightValue");
        }

        private void RefreshStatsPanel()
        {
            EnsureStatsPanel();

            PlayerStatsData localStats = PlayerStatsStorage.Load();
            PlayerStatsData stats = localStats;
            if (onlineService != null && onlineService.CachedMyStats != null && !(authService?.CurrentSession?.isGuest ?? true))
            {
                stats = onlineService.CachedMyStats.ToLocalStatsData();
            }

            AuthSessionData session = (authService ?? AuthRuntime.Service).CurrentSession ?? AuthSessionData.CreateGuest();
            if (statsHeaderText != null)
            {
                statsHeaderText.text = session.isGuest ? "LOCAL PROFILE" : $"PROFILE: {session.DisplayName.ToUpperInvariant()}";
            }

            if (totalRunsValueText != null)
            {
                totalRunsValueText.text = stats.totalRuns.ToString();
            }

            if (totalDeathsValueText != null)
            {
                totalDeathsValueText.text = stats.totalDeaths.ToString();
            }

            if (topScoreValueText != null)
            {
                topScoreValueText.text = stats.topScore.ToString();
            }

            if (topHeightValueText != null)
            {
                topHeightValueText.text = $"{stats.topHeight:0.00}";
            }
        }

        private void EnsureLeaderboardPanel()
        {
            Transform existing = FindChild(transform, "LeaderboardPanel")?.transform;
            if (existing != null)
            {
                CacheLeaderboardTexts(existing);
                return;
            }

            Font font = ResolveBuiltinFont();
            GameObject panelObject = CreateUiObject("LeaderboardPanel", transform, typeof(Image));

            RectTransform panelRect = panelObject.transform as RectTransform;
            if (panelRect != null)
            {
                panelRect.anchorMin = new Vector2(0f, 0.5f);
                panelRect.anchorMax = new Vector2(0f, 0.5f);
                panelRect.pivot = new Vector2(0f, 0.5f);
                panelRect.anchoredPosition = new Vector2(84f, -18f);
                panelRect.sizeDelta = new Vector2(420f, 320f);
            }

            Image panelImage = panelObject.GetComponent<Image>();
            panelImage.color = new Color(0.03f, 0.08f, 0.12f, 0.82f);

            leaderboardTitleText = CreateLeaderboardHeader(panelObject.transform, font);
            leaderboardNameTexts.Clear();
            leaderboardScoreTexts.Clear();

            int rowCount = Mathf.Max(3, leaderboardEntryCount);
            for (int i = 0; i < rowCount; i++)
            {
                CreateLeaderboardRow(panelObject.transform, font, i);
            }
        }

        private void CacheLeaderboardTexts(Transform panel)
        {
            leaderboardNameTexts.Clear();
            leaderboardScoreTexts.Clear();
            leaderboardTitleText = FindTextUnder(panel, "LeaderboardHeader");

            int rowCount = Mathf.Max(3, leaderboardEntryCount);
            for (int i = 0; i < rowCount; i++)
            {
                Text nameText = FindTextUnder(panel, $"LeaderboardName{i + 1}");
                Text scoreText = FindTextUnder(panel, $"LeaderboardScore{i + 1}");
                if (nameText != null)
                {
                    leaderboardNameTexts.Add(nameText);
                }

                if (scoreText != null)
                {
                    leaderboardScoreTexts.Add(scoreText);
                }
            }
        }

        private void RefreshLeaderboardPanel()
        {
            EnsureLeaderboardPanel();

            if (leaderboardTitleText != null)
            {
                leaderboardTitleText.text = "BEST SCORES";
            }

            List<(string name, int score)> entries = BuildLeaderboardEntries();
            int rowCount = Mathf.Min(Mathf.Min(leaderboardNameTexts.Count, leaderboardScoreTexts.Count), Mathf.Max(1, leaderboardEntryCount));
            for (int i = 0; i < rowCount; i++)
            {
                Text nameText = leaderboardNameTexts[i];
                Text scoreText = leaderboardScoreTexts[i];

                if (i < entries.Count)
                {
                    nameText.text = $"{i + 1}. {entries[i].name}";
                    scoreText.text = entries[i].score.ToString();
                }
                else
                {
                    nameText.text = $"{i + 1}. ---";
                    scoreText.text = "0";
                }
            }
        }

        private List<(string name, int score)> BuildLeaderboardEntries()
        {
            List<(string name, int score)> entries = new List<(string name, int score)>();

            if (onlineService != null && onlineService.CachedLeaderboard != null && onlineService.CachedLeaderboard.Count > 0)
            {
                for (int i = 0; i < onlineService.CachedLeaderboard.Count; i++)
                {
                    LeaderboardEntryDto onlineEntry = onlineService.CachedLeaderboard[i];
                    if (onlineEntry == null)
                    {
                        continue;
                    }

                    onlineEntry.Sanitize();
                    entries.Add((onlineEntry.ResolveDisplayName(), onlineEntry.score));
                }
            }

            for (int i = 0; i < fallbackLeaderboardEntries.Count; i++)
            {
                LeaderboardSeedEntry entry = fallbackLeaderboardEntries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.playerName))
                {
                    continue;
                }

                entries.Add((entry.playerName.Trim(), Mathf.Max(0, entry.score)));
            }

            PlayerStatsData stats = onlineService != null && onlineService.CachedMyStats != null
                ? onlineService.CachedMyStats.ToLocalStatsData()
                : PlayerStatsStorage.Load();
            AuthSessionData session = (authService ?? AuthRuntime.Service).CurrentSession ?? AuthSessionData.CreateGuest();
            string localName = session != null && !session.isGuest
                ? session.DisplayName
                : (string.IsNullOrWhiteSpace(localPlayerLeaderboardName) ? "YOU" : localPlayerLeaderboardName.Trim());
            entries.Add((localName, Mathf.Max(0, stats.topScore)));

            entries.Sort((a, b) =>
            {
                int scoreComparison = b.score.CompareTo(a.score);
                return scoreComparison != 0 ? scoreComparison : string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
            });

            List<(string name, int score)> trimmed = new List<(string name, int score)>();
            int targetCount = Mathf.Max(1, leaderboardEntryCount);
            for (int i = 0; i < entries.Count && trimmed.Count < targetCount; i++)
            {
                trimmed.Add(entries[i]);
            }

            return trimmed;
        }

        private Text CreateLeaderboardHeader(Transform parent, Font font)
        {
            GameObject headerObject = CreateUiObject("LeaderboardHeader", parent, typeof(Text));

            RectTransform rect = headerObject.transform as RectTransform;
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2(0f, -22f);
                rect.sizeDelta = new Vector2(-36f, 36f);
            }

            Text text = headerObject.GetComponent<Text>();
            text.font = font;
            text.fontSize = 23;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = new Color(0.72f, 0.96f, 1f, 0.96f);
            text.text = "BEST SCORES";
            text.raycastTarget = false;
            EnsureTextFx(text, new Color(0.01f, 0.03f, 0.07f, 0.92f), new Vector2(1f, -1f), new Color(1f, 0.2f, 0.72f, 0.22f), new Vector2(-1f, 0f));
            return text;
        }

        private void CreateLeaderboardRow(Transform parent, Font font, int index)
        {
            GameObject rowObject = CreateUiObject($"LeaderboardRow{index + 1}", parent);

            RectTransform rowRect = rowObject.transform as RectTransform;
            if (rowRect != null)
            {
                rowRect.anchorMin = new Vector2(0f, 1f);
                rowRect.anchorMax = new Vector2(1f, 1f);
                rowRect.pivot = new Vector2(0.5f, 1f);
                rowRect.anchoredPosition = new Vector2(0f, -(80f + index * 42f));
                rowRect.sizeDelta = new Vector2(-36f, 36f);
            }

            Text nameText = CreateStatsText(rowObject.transform, font, $"LeaderboardName{index + 1}", $"{index + 1}. ---", TextAnchor.MiddleLeft, new Color(0.78f, 0.88f, 0.95f, 0.92f));
            RectTransform nameRect = nameText.transform as RectTransform;
            if (nameRect != null)
            {
                nameRect.anchorMin = new Vector2(0f, 0f);
                nameRect.anchorMax = new Vector2(0.72f, 1f);
                nameRect.offsetMin = Vector2.zero;
                nameRect.offsetMax = Vector2.zero;
            }

            Text scoreText = CreateStatsText(rowObject.transform, font, $"LeaderboardScore{index + 1}", "0", TextAnchor.MiddleRight, new Color(1f, 0.86f, 0.42f, 0.98f));
            scoreText.fontStyle = FontStyle.Bold;
            RectTransform scoreRect = scoreText.transform as RectTransform;
            if (scoreRect != null)
            {
                scoreRect.anchorMin = new Vector2(0.72f, 0f);
                scoreRect.anchorMax = new Vector2(1f, 1f);
                scoreRect.offsetMin = Vector2.zero;
                scoreRect.offsetMax = Vector2.zero;
            }

            leaderboardNameTexts.Add(nameText);
            leaderboardScoreTexts.Add(scoreText);
        }

        private static Text CreatePanelHeader(Transform parent, Font font, string objectName, string value)
        {
            GameObject headerObject = CreateUiObject(objectName, parent, typeof(Text));

            RectTransform rect = headerObject.transform as RectTransform;
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2(0f, -22f);
                rect.sizeDelta = new Vector2(-36f, 36f);
            }

            Text text = headerObject.GetComponent<Text>();
            text.font = font;
            text.fontSize = 23;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = new Color(0.72f, 0.96f, 1f, 0.96f);
            text.text = value;
            text.raycastTarget = false;
            EnsureTextFx(text, new Color(0.01f, 0.03f, 0.07f, 0.92f), new Vector2(1f, -1f), new Color(1f, 0.2f, 0.72f, 0.22f), new Vector2(-1f, 0f));
            return text;
        }

        private static Text CreatePanelBodyText(Transform parent, Font font, string objectName, string value, float anchoredY, int fontSize, FontStyle fontStyle, Color color)
        {
            GameObject textObject = CreateUiObject(objectName, parent, typeof(Text));

            RectTransform rect = textObject.transform as RectTransform;
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2(0f, -anchoredY);
                rect.sizeDelta = new Vector2(-36f, 30f);
            }

            Text text = textObject.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = color;
            text.text = value;
            text.raycastTarget = false;
            EnsureTextFx(text, new Color(0.01f, 0.03f, 0.07f, 0.85f), new Vector2(1f, -1f), new Color(0.12f, 1f, 0.98f, 0.18f), new Vector2(0f, -1f));
            return text;
        }

        private static Text CreateStatsRow(Transform parent, Font font, string label, float anchoredY)
        {
            GameObject rowObject = CreateUiObject($"{SanitizeName(label)}Row", parent);

            RectTransform rowRect = rowObject.transform as RectTransform;
            if (rowRect != null)
            {
                rowRect.anchorMin = new Vector2(0f, 1f);
                rowRect.anchorMax = new Vector2(1f, 1f);
                rowRect.pivot = new Vector2(0.5f, 1f);
                rowRect.anchoredPosition = new Vector2(0f, -anchoredY);
                rowRect.sizeDelta = new Vector2(-36f, 40f);
            }

            Text labelText = CreateStatsText(rowObject.transform, font, $"{SanitizeName(label)}Label", label, TextAnchor.MiddleLeft, new Color(0.78f, 0.88f, 0.95f, 0.92f));
            RectTransform labelRect = labelText.transform as RectTransform;
            if (labelRect != null)
            {
                labelRect.anchorMin = new Vector2(0f, 0f);
                labelRect.anchorMax = new Vector2(0.62f, 1f);
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = Vector2.zero;
            }

            Text valueText = CreateStatsText(rowObject.transform, font, $"{SanitizeName(label)}Value", "0", TextAnchor.MiddleRight, new Color(0.56f, 0.95f, 1f, 0.98f));
            valueText.fontStyle = FontStyle.Bold;
            RectTransform valueRect = valueText.transform as RectTransform;
            if (valueRect != null)
            {
                valueRect.anchorMin = new Vector2(0.62f, 0f);
                valueRect.anchorMax = new Vector2(1f, 1f);
                valueRect.offsetMin = Vector2.zero;
                valueRect.offsetMax = Vector2.zero;
            }

            return valueText;
        }

        private static Text CreateStatsText(Transform parent, Font font, string objectName, string value, TextAnchor alignment, Color color)
        {
            GameObject textObject = CreateUiObject(objectName, parent, typeof(Text));

            Text text = textObject.GetComponent<Text>();
            text.font = font;
            text.fontSize = 24;
            text.alignment = alignment;
            text.color = color;
            text.text = value;
            text.raycastTarget = false;
            EnsureTextFx(text, new Color(0.01f, 0.03f, 0.07f, 0.85f), new Vector2(1f, -1f), new Color(0.12f, 1f, 0.98f, 0.18f), new Vector2(0f, -1f));
            return text;
        }

        private static GameObject CreateInputFieldRow(Transform parent, Font font, string rowName, string label, float anchoredY, bool password, out InputField inputField)
        {
            GameObject rowObject = CreateUiObject(rowName, parent);

            RectTransform rowRect = rowObject.transform as RectTransform;
            if (rowRect != null)
            {
                rowRect.anchorMin = new Vector2(0f, 1f);
                rowRect.anchorMax = new Vector2(1f, 1f);
                rowRect.pivot = new Vector2(0.5f, 1f);
                rowRect.anchoredPosition = new Vector2(0f, -anchoredY);
                rowRect.sizeDelta = new Vector2(-36f, 36f);
            }

            Text labelText = CreateStatsText(rowObject.transform, font, $"{rowName}Label", label, TextAnchor.MiddleLeft, new Color(0.78f, 0.88f, 0.95f, 0.92f));
            labelText.fontSize = 18;
            RectTransform labelRect = labelText.transform as RectTransform;
            if (labelRect != null)
            {
                labelRect.anchorMin = new Vector2(0f, 0f);
                labelRect.anchorMax = new Vector2(0.38f, 1f);
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = Vector2.zero;
            }

            GameObject fieldObject = CreateUiObject(rowName.Replace("Row", "Input"), rowObject.transform, typeof(Image), typeof(InputField));
            RectTransform fieldRect = fieldObject.transform as RectTransform;
            if (fieldRect != null)
            {
                fieldRect.anchorMin = new Vector2(0.4f, 0f);
                fieldRect.anchorMax = new Vector2(1f, 1f);
                fieldRect.offsetMin = Vector2.zero;
                fieldRect.offsetMax = Vector2.zero;
            }

            Image image = fieldObject.GetComponent<Image>();
            image.color = new Color(0.08f, 0.15f, 0.22f, 0.94f);

            inputField = fieldObject.GetComponent<InputField>();
            inputField.lineType = InputField.LineType.SingleLine;
            inputField.contentType = password ? InputField.ContentType.Password : InputField.ContentType.Standard;
            inputField.asteriskChar = '*';
            inputField.caretColor = new Color(0.9f, 0.98f, 1f, 1f);
            inputField.selectionColor = new Color(0.2f, 0.72f, 0.94f, 0.42f);
            inputField.targetGraphic = image;

            if (fieldObject.GetComponent<MenuSelectableFeedback>() == null)
            {
                fieldObject.AddComponent<MenuSelectableFeedback>();
            }

            StyleInputField(inputField);

            GameObject placeholderObject = CreateUiObject("Placeholder", fieldObject.transform, typeof(Text));
            RectTransform placeholderRect = placeholderObject.transform as RectTransform;
            if (placeholderRect != null)
            {
                placeholderRect.anchorMin = Vector2.zero;
                placeholderRect.anchorMax = Vector2.one;
                placeholderRect.offsetMin = new Vector2(12f, 0f);
                placeholderRect.offsetMax = new Vector2(-12f, 0f);
            }

            Text placeholder = placeholderObject.GetComponent<Text>();
            placeholder.font = font;
            placeholder.fontSize = 18;
            placeholder.alignment = TextAnchor.MiddleLeft;
            placeholder.fontStyle = FontStyle.Italic;
            placeholder.color = new Color(0.58f, 0.7f, 0.78f, 0.72f);
            placeholder.text = password ? "Enter password" : $"Enter {label.ToLowerInvariant()}";
            placeholder.raycastTarget = false;

            GameObject textObject = CreateUiObject("Text", fieldObject.transform, typeof(Text));
            RectTransform textRect = textObject.transform as RectTransform;
            if (textRect != null)
            {
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(12f, 0f);
                textRect.offsetMax = new Vector2(-12f, 0f);
            }

            Text inputText = textObject.GetComponent<Text>();
            inputText.font = font;
            inputText.fontSize = 18;
            inputText.alignment = TextAnchor.MiddleLeft;
            inputText.color = new Color(0.9f, 0.98f, 1f, 1f);
            inputText.supportRichText = false;
            inputText.raycastTarget = false;

            inputField.textComponent = inputText;
            inputField.placeholder = placeholder;
            return rowObject;
        }

        private static Button CreateActionButton(Transform parent, Font font, string objectName, string label, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject buttonObject = CreateUiObject(objectName, parent, typeof(Image), typeof(Button));
            RectTransform rect = buttonObject.transform as RectTransform;
            if (rect != null)
            {
                rect.anchorMin = anchorMin;
                rect.anchorMax = anchorMax;
                rect.offsetMin = offsetMin;
                rect.offsetMax = offsetMax;
            }

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.07f, 0.11f, 0.18f, 0.95f);

            Button button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.07f, 0.11f, 0.18f, 0.95f);
            colors.highlightedColor = new Color(0.13f, 0.34f, 0.46f, 1f);
            colors.selectedColor = new Color(0.2f, 0.56f, 0.73f, 1f);
            colors.pressedColor = new Color(0.06f, 0.21f, 0.29f, 1f);
            colors.disabledColor = new Color(0.22f, 0.22f, 0.22f, 0.48f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            if (buttonObject.GetComponent<MenuSelectableFeedback>() == null)
            {
                buttonObject.AddComponent<MenuSelectableFeedback>();
            }

            Text buttonLabel = CreateStatsText(buttonObject.transform, font, "Label", label, TextAnchor.MiddleCenter, new Color(0.92f, 0.98f, 1f, 1f));
            buttonLabel.fontSize = 22;
            buttonLabel.fontStyle = FontStyle.Bold;
            RectTransform labelRect = buttonLabel.transform as RectTransform;
            if (labelRect != null)
            {
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = Vector2.zero;
            }

            StyleButton(button);
            return button;
        }

        private static string SanitizeName(string label)
        {
            return label.Replace(" ", string.Empty);
        }

        private static Text FindTextUnder(Transform parent, string name)
        {
            if (parent == null)
            {
                return null;
            }

            Text[] texts = parent.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (string.Equals(texts[i].name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return texts[i];
                }
            }

            return null;
        }

        private static GameObject FindChild(Transform parent, string name)
        {
            if (parent == null)
            {
                return null;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (string.Equals(child.name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return child.gameObject;
                }

                GameObject nestedMatch = FindChild(child, name);
                if (nestedMatch != null)
                {
                    return nestedMatch;
                }
            }

            return null;
        }

        private static T FindComponentUnder<T>(Transform parent, string name) where T : Component
        {
            if (parent == null)
            {
                return null;
            }

            T[] components = parent.GetComponentsInChildren<T>(true);
            for (int i = 0; i < components.Length; i++)
            {
                if (string.Equals(components[i].name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return components[i];
                }
            }

            return null;
        }

        private static GameObject CreateUiObject(string name, Transform parent, params Type[] extraComponents)
        {
            Type[] components = new Type[2 + extraComponents.Length];
            components[0] = typeof(RectTransform);
            components[1] = typeof(CanvasRenderer);
            for (int i = 0; i < extraComponents.Length; i++)
            {
                components[i + 2] = extraComponents[i];
            }

            GameObject gameObject = new GameObject(name, components);
            gameObject.transform.SetParent(parent, false);

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Undo.RegisterCreatedObjectUndo(gameObject, $"Create {name}");
                EditorUtility.SetDirty(gameObject);
            }
#endif

            return gameObject;
        }

#if UNITY_EDITOR
        private void EnsureStatsPanelInEditor()
        {
            EditorApplication.delayCall -= EnsureStatsPanelInEditor;

            if (this == null || gameObject == null || Application.isPlaying)
            {
                return;
            }

            EnsureFooterHints();
            EnsureAuthPanel();
            RefreshAuthPanel();
            EnsureLeaderboardPanel();
            RefreshLeaderboardPanel();
            EnsureStatsPanel();
            RefreshStatsPanel();
            ApplyResponsiveLayout(force: true);
            ApplyCyberpunkTheme();
            EditorUtility.SetDirty(this);
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif

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
