using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[DefaultExecutionOrder(-6500)]
public sealed class CyberpunkUIStyler : MonoBehaviour
{
    [Header("Auto")]
    [SerializeField] private bool buildInEditMode = true;
    [SerializeField] private bool autoFindElements = true;
    [SerializeField] private RectTransform canvasRoot;

    [Header("Text Targets")]
    [SerializeField] private Text altitudeText;
    [SerializeField] private Text runText;
    [SerializeField] private Text sideEffectsText;
    [SerializeField] private Text neuralLoadText;
    [SerializeField] private Text warningText;
    [SerializeField] private Text checkpointText;
    [SerializeField] private Text summaryText;

    [Header("Overlay")]
    [SerializeField] private Image warningFlash;

    [Header("Palette")]
    [SerializeField] private Color cyan = new(0.12f, 1f, 0.95f, 1f);
    [SerializeField] private Color magenta = new(1f, 0.16f, 0.72f, 1f);
    [SerializeField] private Color amber = new(1f, 0.86f, 0.36f, 1f);
    [SerializeField] private Color panelDark = new(0.02f, 0.03f, 0.08f, 0.56f);

    [Header("Animation")]
    [SerializeField] private float pulseSpeed = 1.9f;
    [SerializeField] [Range(0f, 1f)] private float lineAlpha = 0.9f;

    private Image topPanel;
    private Image bottomPanel;
    private Image topAccent;
    private Image bottomAccent;

    private readonly List<Text> allTexts = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstance()
    {
        if (FindFirstObjectByType<CyberpunkUIStyler>() != null)
        {
            return;
        }

        GameObject canvas = GameObject.Find("GameplayCanvas");
        if (canvas != null)
        {
            canvas.AddComponent<CyberpunkUIStyler>();
        }
    }

    private void Awake()
    {
        TryInitializeStyling();
    }

    private void OnEnable()
    {
        TryInitializeStyling();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying && buildInEditMode)
        {
            TryInitializeStyling();
        }
    }

    [ContextMenu("Build Cyberpunk HUD In Hierarchy")]
    public void BuildCyberpunkHUDInHierarchy()
    {
        TryInitializeStyling();
    }

    private void TryInitializeStyling()
    {
        if (!Application.isPlaying && !buildInEditMode)
        {
            return;
        }

        if (canvasRoot == null)
        {
            canvasRoot = transform as RectTransform;
        }

        ResolveReferences();
        BuildCyberpunkBars();
        ApplyTypographyAndGlow();
        ApplyAnimatedPalette(0f);
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        ApplyAnimatedPalette(Time.unscaledTime);
    }

    private void ResolveReferences()
    {
        if (!autoFindElements)
        {
            CollectTexts();
            return;
        }

        altitudeText = altitudeText != null ? altitudeText : FindTextByName("AltitudeText");
        runText = runText != null ? runText : FindTextByName("RunTimerText");
        sideEffectsText = sideEffectsText != null ? sideEffectsText : FindTextByName("SideEffectsText");
        neuralLoadText = neuralLoadText != null ? neuralLoadText : FindTextByName("NeuralLoadText");
        warningText = warningText != null ? warningText : FindTextByName("WarningText");
        checkpointText = checkpointText != null ? checkpointText : FindTextByName("CheckpointText");
        summaryText = summaryText != null ? summaryText : FindTextByName("RunSummaryText");
        warningFlash = warningFlash != null ? warningFlash : FindImageByName("WarningFlash");

        CollectTexts();
    }

    private void CollectTexts()
    {
        allTexts.Clear();
        AddTextIfValid(altitudeText);
        AddTextIfValid(runText);
        AddTextIfValid(sideEffectsText);
        AddTextIfValid(neuralLoadText);
        AddTextIfValid(warningText);
        AddTextIfValid(checkpointText);
        AddTextIfValid(summaryText);
    }

    private void AddTextIfValid(Text textComponent)
    {
        if (textComponent != null && !allTexts.Contains(textComponent))
        {
            allTexts.Add(textComponent);
        }
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
            if (texts[i].name == objectName)
            {
                return texts[i];
            }
        }

        return null;
    }

    private Image FindImageByName(string objectName)
    {
        if (canvasRoot == null)
        {
            return null;
        }

        Image[] images = canvasRoot.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            if (images[i].name == objectName)
            {
                return images[i];
            }
        }

        return null;
    }

    private void BuildCyberpunkBars()
    {
        if (canvasRoot == null)
        {
            return;
        }

        topPanel = CreateOrGetBar("CP_TopPanel", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(0f, 92f), panelDark);
        bottomPanel = CreateOrGetBar("CP_BottomPanel", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 54f), panelDark * new Color(1f, 1f, 1f, 0.85f));

        topAccent = CreateOrGetLine("CP_TopAccent", topPanel.transform as RectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), 3f);
        bottomAccent = CreateOrGetLine("CP_BottomAccent", bottomPanel.transform as RectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), 3f);
    }

    private Image CreateOrGetBar(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta, Color color)
    {
        Transform existing = canvasRoot.Find(name);
        GameObject go;
        if (existing == null)
        {
            go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(canvasRoot, false);
            rt.SetAsFirstSibling();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, anchorMax.y == 1f ? 1f : 0f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
        }
        else
        {
            go = existing.gameObject;
        }

        Image image = go.GetComponent<Image>();
        image.raycastTarget = false;
        image.color = color;
        return image;
    }

    private Image CreateOrGetLine(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, float thickness)
    {
        if (parent == null)
        {
            return null;
        }

        Transform existing = parent.Find(name);
        GameObject go;
        if (existing == null)
        {
            go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, anchorMin.y == 0f ? 0f : 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, thickness);
        }
        else
        {
            go = existing.gameObject;
        }

        Image image = go.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private void ApplyTypographyAndGlow()
    {
        for (int i = 0; i < allTexts.Count; i++)
        {
            Text label = allTexts[i];
            if (label == null)
            {
                continue;
            }

            label.fontStyle = FontStyle.Bold;

            Outline outline = label.GetComponent<Outline>();
            if (outline == null)
            {
                outline = label.gameObject.AddComponent<Outline>();
            }

            outline.effectColor = new Color(0f, 0f, 0f, 0.88f);
            outline.effectDistance = new Vector2(2f, -2f);

            Shadow shadow = label.GetComponent<Shadow>();
            if (shadow == null)
            {
                shadow = label.gameObject.AddComponent<Shadow>();
            }

            shadow.effectColor = new Color(0.02f, 0.9f, 1f, 0.55f);
            shadow.effectDistance = new Vector2(0f, -1f);
            shadow.useGraphicAlpha = true;
        }
    }

    private void ApplyAnimatedPalette(float time)
    {
        float t = (Mathf.Sin(time * pulseSpeed) + 1f) * 0.5f;
        Color accentA = Color.Lerp(cyan, magenta, t);
        Color accentB = Color.Lerp(magenta, cyan, t);

        SetTextColor(altitudeText, Color.Lerp(cyan, accentA, 0.35f));
        SetTextColor(runText, Color.Lerp(amber, accentB, 0.25f));
        SetTextColor(sideEffectsText, Color.Lerp(magenta, accentA, 0.4f));
        SetTextColor(neuralLoadText, Color.Lerp(cyan, accentB, 0.35f));
        SetTextColor(warningText, Color.Lerp(new Color(1f, 0.35f, 0.35f, 1f), accentB, 0.2f));
        SetTextColor(checkpointText, Color.Lerp(new Color(0.92f, 0.97f, 1f, 1f), accentA, 0.25f));
        SetTextColor(summaryText, Color.Lerp(new Color(0.95f, 0.98f, 1f, 1f), accentB, 0.25f));

        if (topAccent != null)
        {
            Color line = accentA;
            line.a = lineAlpha;
            topAccent.color = line;
        }

        if (bottomAccent != null)
        {
            Color line = accentB;
            line.a = lineAlpha;
            bottomAccent.color = line;
        }

        if (warningFlash != null)
        {
            Color flash = warningFlash.color;
            Color tint = Color.Lerp(magenta, cyan, t);
            flash.r = tint.r;
            flash.g = tint.g;
            flash.b = tint.b;
            warningFlash.color = flash;
        }
    }

    private static void SetTextColor(Text label, Color color)
    {
        if (label != null)
        {
            label.color = color;
        }
    }
}
