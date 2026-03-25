using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Mindrift.UI
{
    [DefaultExecutionOrder(-6200)]
    public sealed class OptionsMenuController : MonoBehaviour
    {
        private enum Tab
        {
            Audio,
            Display,
            Controls
        }

        private static int openCount;
        public static bool IsAnyMenuOpen => openCount > 0;

        [Header("Root")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Button backButton;

        [Header("Tabs")]
        [SerializeField] private Button audioTabButton;
        [SerializeField] private Button displayTabButton;
        [SerializeField] private Button controlsTabButton;
        [SerializeField] private GameObject audioPanel;
        [SerializeField] private GameObject displayPanel;
        [SerializeField] private GameObject controlsPanel;

        [Header("Audio")]
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Text masterVolumeValueText;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Text musicVolumeValueText;
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private Text sfxVolumeValueText;

        [Header("Display")]
        [SerializeField] private Toggle fullscreenToggle;
        [SerializeField] private Dropdown resolutionDropdown;
        [SerializeField] private Dropdown qualityDropdown;

        [Header("Controls")]
        [SerializeField] private Text controllerStatusText;
        [SerializeField] private Slider sensitivitySlider;
        [SerializeField] private Text sensitivityValueText;
        [SerializeField] private Toggle invertYToggle;
        [SerializeField] private Slider deadzoneSlider;
        [SerializeField] private Text deadzoneValueText;

        private readonly List<Selectable> audioSelectables = new List<Selectable>();
        private readonly List<Selectable> displaySelectables = new List<Selectable>();
        private readonly List<Selectable> controlsSelectables = new List<Selectable>();

        private Tab currentTab = Tab.Audio;
        private Action onClosed;
        private bool isBuilt;
        private bool isOpen;
        private bool suppressCallbacks;
        private Font sharedFont;

        public static OptionsMenuController EnsureForCanvas(Transform canvasTransform)
        {
            if (canvasTransform == null)
            {
                return null;
            }

            OptionsMenuController existing = canvasTransform.GetComponentInChildren<OptionsMenuController>(true);
            if (existing != null)
            {
                existing.EnsureBuilt();
                return existing;
            }

            GameObject panel = new GameObject("OptionsPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(canvasTransform, false);
            OptionsMenuController controller = panel.AddComponent<OptionsMenuController>();
            controller.EnsureBuilt();
            return controller;
        }

        private void Awake()
        {
            EnsureBuilt();
        }

        private void OnEnable()
        {
            SettingsManager.OnSettingsApplied += RefreshFromSettings;
            RefreshFromSettings();
        }

        private void OnDisable()
        {
            SettingsManager.OnSettingsApplied -= RefreshFromSettings;
        }

        private void OnDestroy()
        {
            if (isOpen)
            {
                openCount = Mathf.Max(0, openCount - 1);
                isOpen = false;
            }
        }

        private void Update()
        {
            if (!isOpen)
            {
                return;
            }

            if (IsCancelPressed())
            {
                Close();
            }
        }

        public void Open(Action closedCallback = null)
        {
            EnsureBuilt();
            onClosed = closedCallback;
            SettingsManager.EnsureInitialized();
            RefreshFromSettings();
            SetPanelVisible(true);
            SwitchTab(currentTab);
            SelectFirstCurrentTabControl();
        }

        public void Close()
        {
            if (!isOpen)
            {
                return;
            }

            Action callback = onClosed;
            onClosed = null;
            SetPanelVisible(false);
            callback?.Invoke();
        }

        public void OpenAudioTab(Action closedCallback = null)
        {
            Open(closedCallback);
            SwitchTab(Tab.Audio);
            SelectFirstCurrentTabControl();
        }

        private void EnsureBuilt()
        {
            if (isBuilt)
            {
                return;
            }

            SettingsManager.EnsureInitialized();
            sharedFont = ResolveBuiltinFont();

            if (panelRoot == null)
            {
                panelRoot = gameObject;
            }

            BuildRoot();
            BuildTabs();
            BuildPanels();
            BuildFooter();
            HookEvents();
            ConfigureNavigation();
            SwitchTab(Tab.Audio);
            SetPanelVisible(false);
            isBuilt = true;
        }

        private void BuildRoot()
        {
            RectTransform rect = panelRoot.transform as RectTransform;
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(980f, 680f);
                rect.anchoredPosition = Vector2.zero;
            }

            Image image = panelRoot.GetComponent<Image>();
            if (image != null)
            {
                image.color = new Color(0.03f, 0.05f, 0.1f, 0.96f);
            }

            Outline outline = panelRoot.GetComponent<Outline>();
            if (outline == null)
            {
                outline = panelRoot.AddComponent<Outline>();
            }

            outline.effectColor = new Color(0.15f, 0.95f, 1f, 0.64f);
            outline.effectDistance = new Vector2(2f, -2f);

            Text title = CreateText(panelRoot.transform, "OptionsTitle", "OPTIONS", 34, TextAnchor.MiddleCenter, new Color(0.88f, 0.97f, 1f, 1f));
            SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(-120f, 52f));
        }

        private void BuildTabs()
        {
            GameObject tabsRoot = GetOrCreateChild(panelRoot.transform, "Tabs");
            SetRect(tabsRoot.transform as RectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -95f), new Vector2(-120f, 48f));

            HorizontalLayoutGroup layout = tabsRoot.GetComponent<HorizontalLayoutGroup>();
            if (layout == null)
            {
                layout = tabsRoot.AddComponent<HorizontalLayoutGroup>();
            }

            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            audioTabButton = audioTabButton != null ? audioTabButton : CreateButton(tabsRoot.transform, "AudioTabButton", "AUDIO", 22);
            displayTabButton = displayTabButton != null ? displayTabButton : CreateButton(tabsRoot.transform, "DisplayTabButton", "DISPLAY", 22);
            controlsTabButton = controlsTabButton != null ? controlsTabButton : CreateButton(tabsRoot.transform, "ControlsTabButton", "CONTROLS", 22);
        }

        private void BuildPanels()
        {
            GameObject content = GetOrCreateChild(panelRoot.transform, "Content");
            SetRect(content.transform as RectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(-120f, -190f));

            audioPanel = audioPanel != null ? audioPanel : GetOrCreateChild(content.transform, "AudioPanel");
            displayPanel = displayPanel != null ? displayPanel : GetOrCreateChild(content.transform, "DisplayPanel");
            controlsPanel = controlsPanel != null ? controlsPanel : GetOrCreateChild(content.transform, "ControlsPanel");

            PreparePanel(audioPanel);
            PreparePanel(displayPanel);
            PreparePanel(controlsPanel);

            BuildAudioPanel();
            BuildDisplayPanel();
            BuildControlsPanel();
        }

        private void BuildFooter()
        {
            Text hints = CreateText(
                panelRoot.transform,
                "FooterHints",
                "NAV: ARROWS/WASD OR DPAD/STICK  |  SELECT: ENTER/A  |  BACK: ESC/B/START",
                17,
                TextAnchor.MiddleCenter,
                new Color(0.48f, 0.91f, 1f, 0.92f));
            hints.raycastTarget = false;
            SetRect(hints.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(-120f, 30f));

            backButton = backButton != null ? backButton : CreateButton(panelRoot.transform, "BackButton", "BACK", 22);
            SetRect(backButton.transform as RectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-60f, 80f), new Vector2(190f, 50f));
        }

        private void BuildAudioPanel()
        {
            ClearPanel(audioPanel.transform);
            CreateText(audioPanel.transform, "AudioHeader", "AUDIO", 26, TextAnchor.MiddleLeft, new Color(0.86f, 0.96f, 1f, 1f), new Vector2(0.02f, 0.92f), new Vector2(0.45f, 1f));

            CreateSliderRow(audioPanel.transform, "MasterVolume", "MASTER VOLUME", 0.72f, out masterVolumeSlider, out masterVolumeValueText, audioSelectables, 0f, 1f);
            CreateSliderRow(audioPanel.transform, "MusicVolume", "MUSIC VOLUME", 0.53f, out musicVolumeSlider, out musicVolumeValueText, audioSelectables, 0f, 1f);
            CreateSliderRow(audioPanel.transform, "SfxVolume", "SFX VOLUME", 0.34f, out sfxVolumeSlider, out sfxVolumeValueText, audioSelectables, 0f, 1f);
        }

        private void BuildDisplayPanel()
        {
            ClearPanel(displayPanel.transform);
            CreateText(displayPanel.transform, "DisplayHeader", "DISPLAY", 26, TextAnchor.MiddleLeft, new Color(0.86f, 0.96f, 1f, 1f), new Vector2(0.02f, 0.92f), new Vector2(0.45f, 1f));

            CreateToggleRow(displayPanel.transform, "Fullscreen", "FULLSCREEN", 0.72f, out fullscreenToggle, displaySelectables);
            CreateDropdownRow(displayPanel.transform, "Resolution", "RESOLUTION", 0.53f, out resolutionDropdown, displaySelectables);
            CreateDropdownRow(displayPanel.transform, "Quality", "QUALITY", 0.34f, out qualityDropdown, displaySelectables);
        }

        private void BuildControlsPanel()
        {
            ClearPanel(controlsPanel.transform);
            CreateText(controlsPanel.transform, "ControlsHeader", "CONTROLS / CONTROLLER", 26, TextAnchor.MiddleLeft, new Color(0.86f, 0.96f, 1f, 1f), new Vector2(0.02f, 0.92f), new Vector2(0.68f, 1f));

            controllerStatusText = CreateText(
                controlsPanel.transform,
                "ControllerStatus",
                "CONTROLLER: NOT DETECTED",
                20,
                TextAnchor.MiddleLeft,
                new Color(0.32f, 0.95f, 1f, 1f),
                new Vector2(0.02f, 0.8f),
                new Vector2(0.65f, 0.88f));
            if (controllerStatusText.GetComponent<ControllerStatusUI>() == null)
            {
                controllerStatusText.gameObject.AddComponent<ControllerStatusUI>();
            }

            CreateSliderRow(controlsPanel.transform, "Sensitivity", "CONTROLLER SENSITIVITY", 0.62f, out sensitivitySlider, out sensitivityValueText, controlsSelectables, 0.4f, 2f);
            CreateToggleRow(controlsPanel.transform, "InvertY", "INVERT Y", 0.43f, out invertYToggle, controlsSelectables);
            CreateSliderRow(controlsPanel.transform, "Deadzone", "DEADZONE", 0.25f, out deadzoneSlider, out deadzoneValueText, controlsSelectables, 0f, 0.4f);

            Text hints = CreateText(
                controlsPanel.transform,
                "NavigationHints",
                "KEYBOARD: WASD/ARROWS + ENTER + ESC\nCONTROLLER: DPAD/STICK + A + B/START",
                17,
                TextAnchor.UpperLeft,
                new Color(0.56f, 0.9f, 0.98f, 0.96f),
                new Vector2(0.02f, 0.02f),
                new Vector2(0.82f, 0.18f));
            hints.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        private void CreateSliderRow(
            Transform panel,
            string rowName,
            string labelText,
            float anchorY,
            out Slider slider,
            out Text valueText,
            List<Selectable> targetList,
            float minValue,
            float maxValue)
        {
            GameObject row = GetOrCreateChild(panel, $"{rowName}Row");
            SetRect(row.transform as RectTransform, new Vector2(0.02f, anchorY - 0.08f), new Vector2(0.98f, anchorY + 0.08f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);

            Text label = CreateText(row.transform, "Label", labelText, 20, TextAnchor.MiddleLeft, new Color(0.78f, 0.9f, 1f, 1f));
            SetRect(label.rectTransform, new Vector2(0f, 0f), new Vector2(0.33f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);

            slider = CreateSlider(row.transform, "Slider");
            SetRect(slider.transform as RectTransform, new Vector2(0.36f, 0.2f), new Vector2(0.86f, 0.8f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            slider.minValue = minValue;
            slider.maxValue = maxValue;
            slider.wholeNumbers = false;
            targetList.Add(slider);
            EnsureFeedback(slider);

            valueText = CreateText(row.transform, "Value", "100%", 20, TextAnchor.MiddleRight, new Color(0.18f, 0.95f, 1f, 1f));
            SetRect(valueText.rectTransform, new Vector2(0.88f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);
        }

        private void CreateToggleRow(
            Transform panel,
            string rowName,
            string labelText,
            float anchorY,
            out Toggle toggle,
            List<Selectable> targetList)
        {
            GameObject row = GetOrCreateChild(panel, $"{rowName}Row");
            SetRect(row.transform as RectTransform, new Vector2(0.02f, anchorY - 0.08f), new Vector2(0.98f, anchorY + 0.08f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);

            Text label = CreateText(row.transform, "Label", labelText, 20, TextAnchor.MiddleLeft, new Color(0.78f, 0.9f, 1f, 1f));
            SetRect(label.rectTransform, new Vector2(0f, 0f), new Vector2(0.33f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);

            toggle = CreateToggle(row.transform, "Toggle");
            SetRect(toggle.transform as RectTransform, new Vector2(0.36f, 0.18f), new Vector2(0.78f, 0.82f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            targetList.Add(toggle);
            EnsureFeedback(toggle);
        }

        private void CreateDropdownRow(
            Transform panel,
            string rowName,
            string labelText,
            float anchorY,
            out Dropdown dropdown,
            List<Selectable> targetList)
        {
            GameObject row = GetOrCreateChild(panel, $"{rowName}Row");
            SetRect(row.transform as RectTransform, new Vector2(0.02f, anchorY - 0.08f), new Vector2(0.98f, anchorY + 0.08f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);

            Text label = CreateText(row.transform, "Label", labelText, 20, TextAnchor.MiddleLeft, new Color(0.78f, 0.9f, 1f, 1f));
            SetRect(label.rectTransform, new Vector2(0f, 0f), new Vector2(0.33f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);

            dropdown = CreateDropdown(row.transform, "Dropdown");
            SetRect(dropdown.transform as RectTransform, new Vector2(0.36f, 0.15f), new Vector2(0.88f, 0.85f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            targetList.Add(dropdown);
            EnsureFeedback(dropdown);
        }

        private void HookEvents()
        {
            audioTabButton.onClick.RemoveListener(OnAudioTabClicked);
            audioTabButton.onClick.AddListener(OnAudioTabClicked);

            displayTabButton.onClick.RemoveListener(OnDisplayTabClicked);
            displayTabButton.onClick.AddListener(OnDisplayTabClicked);

            controlsTabButton.onClick.RemoveListener(OnControlsTabClicked);
            controlsTabButton.onClick.AddListener(OnControlsTabClicked);

            backButton.onClick.RemoveListener(Close);
            backButton.onClick.AddListener(Close);

            masterVolumeSlider.onValueChanged.RemoveListener(OnMasterVolumeChanged);
            masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);

            musicVolumeSlider.onValueChanged.RemoveListener(OnMusicVolumeChanged);
            musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);

            sfxVolumeSlider.onValueChanged.RemoveListener(OnSfxVolumeChanged);
            sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);

            fullscreenToggle.onValueChanged.RemoveListener(OnFullscreenChanged);
            fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);

            resolutionDropdown.onValueChanged.RemoveListener(OnResolutionChanged);
            resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);

            qualityDropdown.onValueChanged.RemoveListener(OnQualityChanged);
            qualityDropdown.onValueChanged.AddListener(OnQualityChanged);

            sensitivitySlider.onValueChanged.RemoveListener(OnSensitivityChanged);
            sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);

            invertYToggle.onValueChanged.RemoveListener(OnInvertYChanged);
            invertYToggle.onValueChanged.AddListener(OnInvertYChanged);

            deadzoneSlider.onValueChanged.RemoveListener(OnDeadzoneChanged);
            deadzoneSlider.onValueChanged.AddListener(OnDeadzoneChanged);
        }

        private void ConfigureNavigation()
        {
            MenuNavigationController.ApplyVerticalNavigation(audioSelectables);
            MenuNavigationController.ApplyVerticalNavigation(displaySelectables);
            MenuNavigationController.ApplyVerticalNavigation(controlsSelectables);

            List<Selectable> tabs = new List<Selectable> { audioTabButton, displayTabButton, controlsTabButton, backButton };
            MenuNavigationController.ApplyVerticalNavigation(tabs);
        }

        private void RefreshFromSettings()
        {
            if (!isBuilt)
            {
                return;
            }

            suppressCallbacks = true;

            masterVolumeSlider.value = SettingsManager.MasterVolume;
            musicVolumeSlider.value = SettingsManager.MusicVolume;
            sfxVolumeSlider.value = SettingsManager.SfxVolume;
            UpdatePercent(masterVolumeValueText, SettingsManager.MasterVolume);
            UpdatePercent(musicVolumeValueText, SettingsManager.MusicVolume);
            UpdatePercent(sfxVolumeValueText, SettingsManager.SfxVolume);

            fullscreenToggle.isOn = SettingsManager.Fullscreen;

            PopulateResolutionDropdown();
            PopulateQualityDropdown();

            sensitivitySlider.value = SettingsManager.ControllerSensitivity;
            UpdatePercent(sensitivityValueText, Mathf.InverseLerp(0.4f, 2f, SettingsManager.ControllerSensitivity));

            invertYToggle.isOn = SettingsManager.InvertY;

            deadzoneSlider.value = SettingsManager.ControllerDeadzone;
            UpdatePercent(deadzoneValueText, Mathf.Clamp01(SettingsManager.ControllerDeadzone / 0.4f));

            ControllerStatusUI status = controllerStatusText != null ? controllerStatusText.GetComponent<ControllerStatusUI>() : null;
            status?.Refresh();

            suppressCallbacks = false;
        }

        private void PopulateResolutionDropdown()
        {
            IReadOnlyList<Resolution> resolutions = SettingsManager.GetAvailableResolutions();
            List<string> options = new List<string>(resolutions.Count);
            for (int i = 0; i < resolutions.Count; i++)
            {
                Resolution res = resolutions[i];
                options.Add($"{res.width} x {res.height}");
            }

            if (options.Count == 0)
            {
                options.Add($"{Screen.currentResolution.width} x {Screen.currentResolution.height}");
            }

            resolutionDropdown.ClearOptions();
            resolutionDropdown.AddOptions(options);
            resolutionDropdown.value = Mathf.Clamp(SettingsManager.ResolutionIndex, 0, Mathf.Max(options.Count - 1, 0));
            resolutionDropdown.RefreshShownValue();
        }

        private void PopulateQualityDropdown()
        {
            string[] names = QualitySettings.names;
            List<string> options = new List<string>(names.Length);
            for (int i = 0; i < names.Length; i++)
            {
                options.Add(names[i].ToUpperInvariant());
            }

            if (options.Count == 0)
            {
                options.Add("DEFAULT");
            }

            qualityDropdown.ClearOptions();
            qualityDropdown.AddOptions(options);
            qualityDropdown.value = Mathf.Clamp(SettingsManager.QualityIndex, 0, Mathf.Max(options.Count - 1, 0));
            qualityDropdown.RefreshShownValue();
        }

        private void SwitchTab(Tab tab)
        {
            currentTab = tab;
            audioPanel.SetActive(tab == Tab.Audio);
            displayPanel.SetActive(tab == Tab.Display);
            controlsPanel.SetActive(tab == Tab.Controls);
            TintTabButton(audioTabButton, tab == Tab.Audio);
            TintTabButton(displayTabButton, tab == Tab.Display);
            TintTabButton(controlsTabButton, tab == Tab.Controls);
        }

        private void SelectFirstCurrentTabControl()
        {
            Selectable target = null;
            switch (currentTab)
            {
                case Tab.Audio:
                    target = GetFirstSelectable(audioSelectables);
                    break;
                case Tab.Display:
                    target = GetFirstSelectable(displaySelectables);
                    break;
                case Tab.Controls:
                    target = GetFirstSelectable(controlsSelectables);
                    break;
            }

            if (target == null)
            {
                target = backButton;
            }

            MenuNavigationController.SelectDefault(this, target);
        }

        private static Selectable GetFirstSelectable(List<Selectable> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].IsInteractable())
                {
                    return list[i];
                }
            }

            return null;
        }

        private void SetPanelVisible(bool visible)
        {
            if (panelRoot.activeSelf != visible)
            {
                panelRoot.SetActive(visible);
            }

            if (isOpen == visible)
            {
                return;
            }

            isOpen = visible;
            if (visible)
            {
                openCount++;
            }
            else
            {
                openCount = Mathf.Max(0, openCount - 1);
            }
        }

        private void OnAudioTabClicked()
        {
            SwitchTab(Tab.Audio);
            SelectFirstCurrentTabControl();
        }

        private void OnDisplayTabClicked()
        {
            SwitchTab(Tab.Display);
            SelectFirstCurrentTabControl();
        }

        private void OnControlsTabClicked()
        {
            SwitchTab(Tab.Controls);
            SelectFirstCurrentTabControl();
        }

        private void OnMasterVolumeChanged(float value)
        {
            if (suppressCallbacks) return;
            SettingsManager.SetMasterVolume(value);
            UpdatePercent(masterVolumeValueText, value);
        }

        private void OnMusicVolumeChanged(float value)
        {
            if (suppressCallbacks) return;
            SettingsManager.SetMusicVolume(value);
            UpdatePercent(musicVolumeValueText, value);
        }

        private void OnSfxVolumeChanged(float value)
        {
            if (suppressCallbacks) return;
            SettingsManager.SetSfxVolume(value);
            UpdatePercent(sfxVolumeValueText, value);
        }

        private void OnFullscreenChanged(bool value)
        {
            if (suppressCallbacks) return;
            SettingsManager.SetFullscreen(value);
        }

        private void OnResolutionChanged(int value)
        {
            if (suppressCallbacks) return;
            SettingsManager.SetResolutionIndex(value);
        }

        private void OnQualityChanged(int value)
        {
            if (suppressCallbacks) return;
            SettingsManager.SetQualityIndex(value);
        }

        private void OnSensitivityChanged(float value)
        {
            if (suppressCallbacks) return;
            SettingsManager.SetControllerSensitivity(value);
            UpdatePercent(sensitivityValueText, Mathf.InverseLerp(0.4f, 2f, value));
        }

        private void OnInvertYChanged(bool value)
        {
            if (suppressCallbacks) return;
            SettingsManager.SetInvertY(value);
        }

        private void OnDeadzoneChanged(float value)
        {
            if (suppressCallbacks) return;
            SettingsManager.SetControllerDeadzone(value);
            UpdatePercent(deadzoneValueText, Mathf.Clamp01(value / 0.4f));
        }

        private static void UpdatePercent(Text label, float value01)
        {
            if (label == null)
            {
                return;
            }

            label.text = $"{Mathf.RoundToInt(Mathf.Clamp01(value01) * 100f)}%";
        }

        private static bool IsCancelPressed()
        {
#if ENABLE_INPUT_SYSTEM
            bool keyboard = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
            bool gamepad = Gamepad.current != null &&
                           (Gamepad.current.startButton.wasPressedThisFrame || Gamepad.current.buttonEast.wasPressedThisFrame);
            return keyboard || gamepad;
#else
            return false;
#endif
        }

        private Button CreateButton(Transform parent, string name, string label, int fontSize)
        {
            GameObject buttonObject = GetOrCreateChild(parent, name);
            buttonObject.transform.SetParent(parent, false);

            RectTransform rect = buttonObject.transform as RectTransform;
            if (rect != null)
            {
                rect.sizeDelta = new Vector2(280f, 48f);
            }

            Image image = buttonObject.GetComponent<Image>();
            if (image == null)
            {
                image = buttonObject.AddComponent<Image>();
            }

            image.color = new Color(0.08f, 0.14f, 0.22f, 0.92f);

            Button button = buttonObject.GetComponent<Button>();
            if (button == null)
            {
                button = buttonObject.AddComponent<Button>();
            }

            ColorBlock colors = button.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = new Color(0.14f, 0.35f, 0.5f, 1f);
            colors.selectedColor = new Color(0.15f, 0.44f, 0.61f, 1f);
            colors.pressedColor = new Color(0.1f, 0.24f, 0.33f, 1f);
            colors.disabledColor = new Color(0.25f, 0.25f, 0.25f, 0.5f);
            button.colors = colors;

            Text labelText = CreateText(buttonObject.transform, "Label", label, fontSize, TextAnchor.MiddleCenter, new Color(0.9f, 0.97f, 1f, 1f));
            SetRect(labelText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            EnsureFeedback(button);
            return button;
        }

        private Slider CreateSlider(Transform parent, string name)
        {
            GameObject sliderGO = DefaultControls.CreateSlider(new DefaultControls.Resources());
            sliderGO.name = name;
            sliderGO.transform.SetParent(parent, false);

            Slider slider = sliderGO.GetComponent<Slider>();
            Image bg = sliderGO.transform.Find("Background")?.GetComponent<Image>();
            if (bg != null) bg.color = new Color(0.08f, 0.1f, 0.16f, 1f);
            Image fill = sliderGO.transform.Find("Fill Area/Fill")?.GetComponent<Image>();
            if (fill != null) fill.color = new Color(0.14f, 0.95f, 1f, 0.96f);
            Image handle = sliderGO.transform.Find("Handle Slide Area/Handle")?.GetComponent<Image>();
            if (handle != null) handle.color = new Color(0.9f, 0.96f, 1f, 1f);

            return slider;
        }

        private Toggle CreateToggle(Transform parent, string name)
        {
            GameObject toggleGO = DefaultControls.CreateToggle(new DefaultControls.Resources());
            toggleGO.name = name;
            toggleGO.transform.SetParent(parent, false);

            Toggle toggle = toggleGO.GetComponent<Toggle>();

            Text label = toggleGO.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.text = "ENABLED";
                label.font = sharedFont;
                label.fontSize = 18;
                label.color = new Color(0.84f, 0.94f, 1f, 1f);
            }

            Image bg = toggleGO.transform.Find("Background")?.GetComponent<Image>();
            if (bg != null) bg.color = new Color(0.08f, 0.1f, 0.16f, 1f);
            Image check = toggleGO.transform.Find("Background/Checkmark")?.GetComponent<Image>();
            if (check != null) check.color = new Color(0.14f, 0.95f, 1f, 1f);

            return toggle;
        }

        private Dropdown CreateDropdown(Transform parent, string name)
        {
            GameObject dropdownGO = DefaultControls.CreateDropdown(new DefaultControls.Resources());
            dropdownGO.name = name;
            dropdownGO.transform.SetParent(parent, false);

            Dropdown dropdown = dropdownGO.GetComponent<Dropdown>();
            Image bg = dropdownGO.GetComponent<Image>();
            if (bg != null) bg.color = new Color(0.08f, 0.11f, 0.18f, 1f);

            if (dropdown.captionText != null)
            {
                dropdown.captionText.font = sharedFont;
                dropdown.captionText.fontSize = 18;
                dropdown.captionText.color = new Color(0.86f, 0.97f, 1f, 1f);
            }

            if (dropdown.itemText != null)
            {
                dropdown.itemText.font = sharedFont;
                dropdown.itemText.fontSize = 17;
                dropdown.itemText.color = new Color(0.86f, 0.97f, 1f, 1f);
            }

            Image templateBG = dropdownGO.transform.Find("Template")?.GetComponent<Image>();
            if (templateBG != null)
            {
                templateBG.color = new Color(0.05f, 0.08f, 0.14f, 0.96f);
            }

            return dropdown;
        }

        private Text CreateText(
            Transform parent,
            string name,
            string value,
            int fontSize,
            TextAnchor anchor,
            Color color,
            Vector2? anchorMin = null,
            Vector2? anchorMax = null)
        {
            GameObject textObject = GetOrCreateChild(parent, name);
            textObject.transform.SetParent(parent, false);

            Text text = textObject.GetComponent<Text>();
            if (text == null)
            {
                text = textObject.AddComponent<Text>();
            }

            text.text = value;
            text.font = sharedFont;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = anchor;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            if (anchorMin.HasValue && anchorMax.HasValue)
            {
                SetRect(text.rectTransform, anchorMin.Value, anchorMax.Value, new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            }

            return text;
        }

        private void PreparePanel(GameObject panel)
        {
            panel.transform.SetParent(panel.transform.parent, false);
            RectTransform rect = panel.transform as RectTransform;
            SetRect(rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            Image image = panel.GetComponent<Image>();
            if (image == null)
            {
                image = panel.AddComponent<Image>();
            }

            image.color = new Color(0.04f, 0.06f, 0.12f, 0.84f);
        }

        private void ClearPanel(Transform panel)
        {
            for (int i = panel.childCount - 1; i >= 0; i--)
            {
                Destroy(panel.GetChild(i).gameObject);
            }
        }

        private static GameObject GetOrCreateChild(Transform parent, string childName)
        {
            Transform existing = parent.Find(childName);
            if (existing != null)
            {
                return existing.gameObject;
            }

            GameObject child = new GameObject(childName, typeof(RectTransform), typeof(CanvasRenderer));
            child.transform.SetParent(parent, false);
            return child;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
        }

        private static void TintTabButton(Button button, bool active)
        {
            if (button == null)
            {
                return;
            }

            Image image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = active
                    ? new Color(0.13f, 0.43f, 0.59f, 1f)
                    : new Color(0.08f, 0.14f, 0.22f, 0.92f);
            }
        }

        private static void EnsureFeedback(Selectable selectable)
        {
            if (selectable == null)
            {
                return;
            }

            MenuSelectableFeedback feedback = selectable.GetComponent<MenuSelectableFeedback>();
            if (feedback == null)
            {
                selectable.gameObject.AddComponent<MenuSelectableFeedback>();
            }
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
    }
}
