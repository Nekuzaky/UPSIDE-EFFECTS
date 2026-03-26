using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Mindrift.UI
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-6050)]
    public sealed class MainMenuAtmosphereFx : MonoBehaviour
    {
        [Serializable]
        private struct DailyModifier
        {
            public string label;
            public string effect;
        }

        private sealed class NodeState
        {
            public RectTransform rect;
            public Image image;
            public Vector2 velocity;
            public float phase;
            public float tint;
            public float baseAlpha;
            public float baseSize;
        }

        [Header("References")]
        [SerializeField] private RectTransform canvasRoot;
        [SerializeField] private Text subtitleText;
        [SerializeField] private Text footerHintsText;

        [Header("Ambient")]
        [SerializeField] [Range(8, 64)] private int ambientNodeCount = 22;
        [SerializeField] [Min(4f)] private float ambientNodeSpeed = 18f;
        [SerializeField] [Min(0.1f)] private float ambientPulseSpeed = 0.85f;
        [SerializeField] [Range(0f, 1f)] private float overlayStrength = 1f;

        [Header("Subtitle Rotation")]
        [SerializeField] private bool rotateSubtitle = true;
        [SerializeField] [Min(2f)] private float subtitleCycleInterval = 5.5f;
        [SerializeField] private string[] subtitleVariants = new[]
        {
            "THREAD THE VOID. KEEP THE SIGNAL.",
            "RIDE THE STATIC. OWN THE FALL.",
            "DESCEND FASTER. STAY SHARP.",
            "NEVER TRUST A STILL PLATFORM."
        };

        [Header("Daily Card")]
        [SerializeField] private DailyModifier[] dailyModifiers = new[]
        {
            new DailyModifier { label = "GLASS MIND", effect = "Score x2 but one missed jump ends the run" },
            new DailyModifier { label = "DEEP FOCUS", effect = "Lower visual noise, tighter timing windows" },
            new DailyModifier { label = "AUDIO SURGE", effect = "Music-reactive intensity grants bonus score" },
            new DailyModifier { label = "VOID WIND", effect = "Lateral drift pushes moving platforms" },
            new DailyModifier { label = "ECHO STEP", effect = "Every third jump grants an altitude burst" }
        };

        private RectTransform atmosphereRoot;
        private Image auraLayerA;
        private Image auraLayerB;
        private Image vignetteLayer;
        private RectTransform nodesRoot;
        private readonly List<NodeState> nodes = new List<NodeState>();

        private RectTransform dailyPanel;
        private Text dailyHeaderText;
        private Text dailyModifierText;
        private Text dailyObjectiveText;
        private Text dailyRewardText;

        private Font sharedFont;
        private Vector2 lastCanvasSize;
        private bool isInitialized;
        private string defaultSubtitle = "DESCEND THROUGH COGNITIVE NOISE";
        private int subtitleVariantIndex;
        private float nextSubtitleSwapAt;

        public static MainMenuAtmosphereFx EnsureForCanvas(Transform canvasTransform)
        {
            if (canvasTransform == null)
            {
                return null;
            }

            MainMenuAtmosphereFx fx = canvasTransform.GetComponent<MainMenuAtmosphereFx>();
            if (fx == null)
            {
                fx = canvasTransform.gameObject.AddComponent<MainMenuAtmosphereFx>();
            }

            fx.InitializeIfNeeded();
            return fx;
        }

        private void Awake()
        {
            InitializeIfNeeded();
        }

        private void OnEnable()
        {
            InitializeIfNeeded();
            ApplyLayout(force: true);
        }

        private void Update()
        {
            if (!isInitialized)
            {
                InitializeIfNeeded();
                if (!isInitialized)
                {
                    return;
                }
            }

            ApplyLayout(force: false);
            AnimateBackdrop(Time.unscaledTime);
            AnimateNodes(Time.unscaledDeltaTime, Time.unscaledTime);
            RotateSubtitle(Time.unscaledTime);
        }

        private void InitializeIfNeeded()
        {
            if (canvasRoot == null)
            {
                canvasRoot = transform as RectTransform;
            }

            if (canvasRoot == null)
            {
                return;
            }

            sharedFont ??= ResolveBuiltinFont();
            ResolveTextReferences();
            EnsureAtmosphereHierarchy();
            EnsureDailyPanel();
            RefreshDailyCard();
            nextSubtitleSwapAt = Time.unscaledTime + Mathf.Max(2f, subtitleCycleInterval);
            isInitialized = true;
        }

        private void ResolveTextReferences()
        {
            if (subtitleText == null)
            {
                subtitleText = FindTextByName("Subtitle");
            }

            if (subtitleText != null && !string.IsNullOrWhiteSpace(subtitleText.text))
            {
                defaultSubtitle = subtitleText.text.Trim();
            }

            if (footerHintsText == null)
            {
                footerHintsText = FindTextByName("FooterHints");
            }
        }

        private void EnsureAtmosphereHierarchy()
        {
            atmosphereRoot = EnsureRectTransform(canvasRoot, "AtmosphereRoot");
            Stretch(atmosphereRoot, new Vector2(-180f, -180f), new Vector2(180f, 180f));

            Transform backgroundOverlay = canvasRoot.Find("BackgroundOverlay");
            if (backgroundOverlay != null)
            {
                int siblingIndex = Mathf.Clamp(backgroundOverlay.GetSiblingIndex() + 1, 0, canvasRoot.childCount - 1);
                atmosphereRoot.SetSiblingIndex(siblingIndex);
            }
            else
            {
                atmosphereRoot.SetAsFirstSibling();
            }

            auraLayerA = EnsureLayerImage(atmosphereRoot, "AuraLayerA");
            auraLayerB = EnsureLayerImage(atmosphereRoot, "AuraLayerB");
            vignetteLayer = EnsureLayerImage(atmosphereRoot, "VignetteLayer");
            nodesRoot = EnsureRectTransform(atmosphereRoot, "AmbientNodes");
            Stretch(nodesRoot, Vector2.zero, Vector2.zero);

            EnsureAmbientNodes();
        }

        private void EnsureAmbientNodes()
        {
            if (nodesRoot == null)
            {
                return;
            }

            while (nodesRoot.childCount < ambientNodeCount)
            {
                CreateUiObject($"Node{nodesRoot.childCount + 1:00}", nodesRoot, typeof(Image));
            }

            while (nodesRoot.childCount > ambientNodeCount)
            {
                Transform child = nodesRoot.GetChild(nodesRoot.childCount - 1);
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                    break;
                }

                DestroyImmediate(child.gameObject);
            }

            Vector2 canvasSize = GetCanvasSize();
            nodes.Clear();

            for (int i = 0; i < nodesRoot.childCount; i++)
            {
                Transform nodeTransform = nodesRoot.GetChild(i);
                RectTransform rect = nodeTransform as RectTransform;
                if (rect == null)
                {
                    continue;
                }

                Image image = nodeTransform.GetComponent<Image>();
                if (image == null)
                {
                    image = nodeTransform.gameObject.AddComponent<Image>();
                }

                nodeTransform.gameObject.name = $"Node{i + 1:00}";
                image.raycastTarget = false;

                float size = UnityEngine.Random.Range(2.2f, 6.2f);
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(size, size);
                rect.anchoredPosition = new Vector2(
                    UnityEngine.Random.Range(-canvasSize.x * 0.52f, canvasSize.x * 0.52f),
                    UnityEngine.Random.Range(-canvasSize.y * 0.52f, canvasSize.y * 0.52f));

                Vector2 direction = UnityEngine.Random.insideUnitCircle.normalized;
                if (direction.sqrMagnitude < 0.001f)
                {
                    direction = Vector2.right;
                }

                NodeState state = new NodeState
                {
                    rect = rect,
                    image = image,
                    velocity = direction * (ambientNodeSpeed * UnityEngine.Random.Range(0.45f, 1.25f)),
                    phase = UnityEngine.Random.Range(0f, Mathf.PI * 2f),
                    tint = UnityEngine.Random.value,
                    baseAlpha = UnityEngine.Random.Range(0.12f, 0.38f),
                    baseSize = size
                };

                nodes.Add(state);
            }
        }

        private void EnsureDailyPanel()
        {
            GameObject panelObject = FindChild(canvasRoot, "DailyChallengePanel");
            if (panelObject == null)
            {
                panelObject = CreateUiObject("DailyChallengePanel", canvasRoot, typeof(Image));
            }

            dailyPanel = panelObject.transform as RectTransform;
            if (dailyPanel == null)
            {
                return;
            }

            Image panelImage = panelObject.GetComponent<Image>();
            panelImage.color = new Color(0.03f, 0.09f, 0.14f, 0.86f);
            panelImage.raycastTarget = false;

            Outline outline = panelObject.GetComponent<Outline>();
            if (outline == null)
            {
                outline = panelObject.AddComponent<Outline>();
            }

            outline.effectColor = new Color(0.12f, 1f, 0.98f, 0.26f);
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = true;

            Shadow shadow = panelObject.GetComponent<Shadow>();
            if (shadow == null)
            {
                shadow = panelObject.AddComponent<Shadow>();
            }

            shadow.effectColor = new Color(1f, 0.2f, 0.72f, 0.14f);
            shadow.effectDistance = new Vector2(0f, -1f);
            shadow.useGraphicAlpha = true;

            dailyHeaderText = EnsureCardText(panelObject.transform, "Header", 19, FontStyle.Bold, TextAnchor.UpperLeft, new Color(0.72f, 0.96f, 1f, 0.98f));
            dailyModifierText = EnsureCardText(panelObject.transform, "Modifier", 15, FontStyle.Bold, TextAnchor.UpperLeft, new Color(1f, 0.86f, 0.42f, 0.98f));
            dailyObjectiveText = EnsureCardText(panelObject.transform, "Objective", 14, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.84f, 0.92f, 0.98f, 0.94f));
            dailyRewardText = EnsureCardText(panelObject.transform, "Reward", 13, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.62f, 0.97f, 1f, 0.96f));

            PositionCardText(dailyHeaderText, 10f, 24f);
            PositionCardText(dailyModifierText, 38f, 22f);
            PositionCardText(dailyObjectiveText, 61f, 21f);
            PositionCardText(dailyRewardText, 83f, 30f);

            dailyPanel.SetAsLastSibling();
        }

        private void RefreshDailyCard()
        {
            if (dailyHeaderText == null || dailyModifierText == null || dailyObjectiveText == null || dailyRewardText == null)
            {
                return;
            }

            DateTime today = DateTime.Now.Date;
            int seed = today.Year * 1000 + today.DayOfYear;
            System.Random random = new System.Random(seed);

            DailyModifier modifier = default;
            if (dailyModifiers != null && dailyModifiers.Length > 0)
            {
                modifier = dailyModifiers[random.Next(dailyModifiers.Length)];
            }

            if (string.IsNullOrWhiteSpace(modifier.label))
            {
                modifier.label = "STABLE MIND";
            }

            if (string.IsNullOrWhiteSpace(modifier.effect))
            {
                modifier.effect = "Clean run with no side modifiers";
            }

            int objectiveAltitude = 850 + random.Next(0, 9) * 75;
            int scoreBonus = 1200 + random.Next(0, 8) * 220;

            dailyHeaderText.text = $"DAILY RIFT // {today:dd MMM yyyy}".ToUpperInvariant();
            dailyModifierText.text = $"MUTATOR: {modifier.label}".ToUpperInvariant();
            dailyObjectiveText.text = $"OBJECTIVE: REACH {objectiveAltitude}M";
            dailyRewardText.text = $"BONUS: +{scoreBonus:N0} SCORE // {modifier.effect}".ToUpperInvariant();
        }

        private void ApplyLayout(bool force)
        {
            Vector2 canvasSize = GetCanvasSize();
            if (!force && Mathf.Abs(canvasSize.x - lastCanvasSize.x) < 0.5f && Mathf.Abs(canvasSize.y - lastCanvasSize.y) < 0.5f)
            {
                return;
            }

            lastCanvasSize = canvasSize;
            bool compact = canvasSize.x < 1440f || canvasSize.y < 900f;

            if (atmosphereRoot != null)
            {
                Stretch(atmosphereRoot, new Vector2(-220f, -220f), new Vector2(220f, 220f));
            }

            LayoutDailyPanel(compact);
        }

        private void LayoutDailyPanel(bool compact)
        {
            if (dailyPanel == null)
            {
                return;
            }

            if (compact)
            {
                dailyPanel.anchorMin = new Vector2(0.5f, 1f);
                dailyPanel.anchorMax = new Vector2(0.5f, 1f);
                dailyPanel.pivot = new Vector2(0.5f, 1f);
                dailyPanel.anchoredPosition = new Vector2(0f, -218f);
                dailyPanel.sizeDelta = new Vector2(430f, 114f);
            }
            else
            {
                dailyPanel.anchorMin = new Vector2(0f, 1f);
                dailyPanel.anchorMax = new Vector2(0f, 1f);
                dailyPanel.pivot = new Vector2(0f, 1f);
                dailyPanel.anchoredPosition = new Vector2(84f, -212f);
                dailyPanel.sizeDelta = new Vector2(380f, 116f);
            }

            if (dailyHeaderText != null)
            {
                dailyHeaderText.fontSize = compact ? 17 : 19;
            }

            if (dailyModifierText != null)
            {
                dailyModifierText.fontSize = compact ? 14 : 15;
            }

            if (dailyObjectiveText != null)
            {
                dailyObjectiveText.fontSize = compact ? 13 : 14;
            }

            if (dailyRewardText != null)
            {
                dailyRewardText.fontSize = compact ? 12 : 13;
            }
        }

        private void AnimateBackdrop(float time)
        {
            float pulse = (Mathf.Sin(time * ambientPulseSpeed) + 1f) * 0.5f;
            Color cyan = Color.Lerp(new Color(0.04f, 0.24f, 0.3f, 0.12f), new Color(0.12f, 0.95f, 1f, 0.28f), pulse);
            Color magenta = Color.Lerp(new Color(0.16f, 0.04f, 0.2f, 0.1f), new Color(0.96f, 0.12f, 0.66f, 0.24f), 1f - pulse);

            if (auraLayerA != null)
            {
                cyan.a *= overlayStrength;
                auraLayerA.color = cyan;
                RectTransform rect = auraLayerA.rectTransform;
                rect.anchoredPosition = new Vector2(Mathf.Sin(time * 0.11f) * 56f, Mathf.Cos(time * 0.09f) * 42f);
                rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * 0.07f) * 4f);
            }

            if (auraLayerB != null)
            {
                magenta.a *= overlayStrength;
                auraLayerB.color = magenta;
                RectTransform rect = auraLayerB.rectTransform;
                rect.anchoredPosition = new Vector2(Mathf.Cos(time * 0.09f) * 62f, Mathf.Sin(time * 0.08f) * 48f);
                rect.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Cos(time * 0.06f) * 5f);
            }

            if (vignetteLayer != null)
            {
                Color color = new Color(0.01f, 0.02f, 0.05f, Mathf.Lerp(0.2f, 0.28f, pulse) * overlayStrength);
                vignetteLayer.color = color;
            }

            if (footerHintsText != null)
            {
                Color footerColor = footerHintsText.color;
                footerColor.a = Mathf.Lerp(0.66f, 0.84f, pulse);
                footerHintsText.color = footerColor;
            }
        }

        private void AnimateNodes(float deltaTime, float time)
        {
            if (nodes.Count == 0)
            {
                return;
            }

            Vector2 canvasSize = GetCanvasSize();
            float maxX = canvasSize.x * 0.58f;
            float maxY = canvasSize.y * 0.58f;

            for (int i = 0; i < nodes.Count; i++)
            {
                NodeState node = nodes[i];
                if (node.rect == null || node.image == null)
                {
                    continue;
                }

                Vector2 pos = node.rect.anchoredPosition + node.velocity * deltaTime;
                if (pos.x > maxX) pos.x = -maxX;
                if (pos.x < -maxX) pos.x = maxX;
                if (pos.y > maxY) pos.y = -maxY;
                if (pos.y < -maxY) pos.y = maxY;
                node.rect.anchoredPosition = pos;

                float pulse = (Mathf.Sin(time * (ambientPulseSpeed * 1.3f) + node.phase) + 1f) * 0.5f;
                Color tint = Color.Lerp(new Color(0.12f, 1f, 0.98f, 1f), new Color(1f, 0.18f, 0.72f, 1f), node.tint);
                tint.a = node.baseAlpha * Mathf.Lerp(0.4f, 1f, pulse);
                node.image.color = tint;

                float size = node.baseSize * Mathf.Lerp(0.8f, 1.25f, pulse);
                node.rect.sizeDelta = new Vector2(size, size);
            }
        }

        private void RotateSubtitle(float time)
        {
            if (!rotateSubtitle || subtitleText == null)
            {
                return;
            }

            if (time < nextSubtitleSwapAt)
            {
                return;
            }

            nextSubtitleSwapAt = time + Mathf.Max(2f, subtitleCycleInterval) + UnityEngine.Random.Range(-0.8f, 0.8f);
            subtitleVariantIndex++;
            subtitleText.text = ResolveSubtitleVariant(subtitleVariantIndex);
        }

        private string ResolveSubtitleVariant(int index)
        {
            int variantCount = subtitleVariants != null ? subtitleVariants.Length : 0;
            int cycleCount = variantCount + 1;
            int slot = Mathf.Abs(index) % Mathf.Max(1, cycleCount);

            if (slot == 0)
            {
                return defaultSubtitle;
            }

            string candidate = subtitleVariants[slot - 1];
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return defaultSubtitle;
            }

            return candidate.Trim().ToUpperInvariant();
        }

        private Text EnsureCardText(Transform parent, string name, int fontSize, FontStyle fontStyle, TextAnchor anchor, Color color)
        {
            GameObject textObject = FindChild(parent, name);
            if (textObject == null)
            {
                textObject = CreateUiObject(name, parent, typeof(Text));
            }

            Text text = textObject.GetComponent<Text>();
            text.font = sharedFont;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = anchor;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            Outline outline = textObject.GetComponent<Outline>();
            if (outline == null)
            {
                outline = textObject.AddComponent<Outline>();
            }

            outline.effectColor = new Color(0.01f, 0.03f, 0.08f, 0.8f);
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = true;

            return text;
        }

        private static void PositionCardText(Text text, float top, float height)
        {
            if (text == null)
            {
                return;
            }

            RectTransform rect = text.transform as RectTransform;
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -top);
            rect.sizeDelta = new Vector2(-20f, height);
        }

        private Image EnsureLayerImage(Transform parent, string name)
        {
            GameObject layerObject = FindChild(parent, name);
            if (layerObject == null)
            {
                layerObject = CreateUiObject(name, parent, typeof(Image));
            }

            Image image = layerObject.GetComponent<Image>();
            image.raycastTarget = false;

            RectTransform rect = layerObject.transform as RectTransform;
            if (rect != null)
            {
                Stretch(rect, Vector2.zero, Vector2.zero);
            }

            return image;
        }

        private Vector2 GetCanvasSize()
        {
            if (canvasRoot != null && canvasRoot.rect.width > 0f && canvasRoot.rect.height > 0f)
            {
                return canvasRoot.rect.size;
            }

            return new Vector2(Screen.width, Screen.height);
        }

        private Text FindTextByName(string objectName)
        {
            if (canvasRoot == null)
            {
                return null;
            }

            Text[] texts = canvasRoot.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (string.Equals(texts[i].name, objectName, StringComparison.OrdinalIgnoreCase))
                {
                    return texts[i];
                }
            }

            return null;
        }

        private static RectTransform EnsureRectTransform(Transform parent, string name)
        {
            GameObject child = FindChild(parent, name);
            if (child == null)
            {
                child = CreateUiObject(name, parent);
            }

            return child.transform as RectTransform;
        }

        private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static GameObject FindChild(Transform parent, string name)
        {
            if (parent == null)
            {
                return null;
            }

            Transform child = parent.Find(name);
            return child != null ? child.gameObject : null;
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
            return gameObject;
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
