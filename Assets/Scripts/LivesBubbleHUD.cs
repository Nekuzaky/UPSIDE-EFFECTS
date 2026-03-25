using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Mindrift.Core;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[DefaultExecutionOrder(-6400)]
public sealed class LivesBubbleHUD : MonoBehaviour
{
    [Header("Auto")]
    [SerializeField] private bool buildInEditMode = true;
    [SerializeField] private bool autoFindReferences = true;

    [Header("References")]
    [SerializeField] private RectTransform canvasRoot;
    [SerializeField] private LivesSystem livesSystem;

    [Header("Layout")]
    [SerializeField] private string rootName = "LivesBubblesHUD";
    [SerializeField] private Vector2 topRightOffset = new(-30f, -66f);
    [SerializeField] private Vector2 bubbleSize = new(30f, 30f);
    [SerializeField] private float bubbleSpacing = 10f;

    [Header("Style")]
    [SerializeField] private Sprite lifeSprite;
    [SerializeField] private bool autoAssignLifeSprite = true;
    [SerializeField] private string fallbackLifeSpriteName = "life";
    [SerializeField] private Color labelColor = new(0.85f, 0.96f, 1f, 0.92f);
    [SerializeField] private Color activeBubbleColor = new(1f, 0.22f, 0.74f, 0.96f);
    [SerializeField] private Color inactiveBubbleColor = new(0.14f, 0.2f, 0.34f, 0.42f);
    [SerializeField] private Color bubbleOutlineColor = new(0.12f, 1f, 0.96f, 0.96f);

    [Header("Animation")]
    [SerializeField] private bool pulseActiveBubbles = true;
    [SerializeField] private float pulseSpeed = 4.2f;
    [SerializeField] [Range(0f, 0.6f)] private float pulseAmplitude = 0.18f;

    [Header("Editor Preview")]
    [SerializeField] private int previewLives = 3;
    [SerializeField] private int previewMaxLives = 3;

    private RectTransform rootRect;
    private Text labelText;
    private RectTransform rowRect;
    private readonly List<Image> bubbles = new();
    private bool needsRebuild = true;
#if UNITY_EDITOR
    private bool editorRebuildQueued;
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstance()
    {
        if (FindFirstObjectByType<LivesBubbleHUD>() != null)
        {
            return;
        }

        GameObject canvas = GameObject.Find("GameplayCanvas");
        if (canvas != null)
        {
            canvas.AddComponent<LivesBubbleHUD>();
        }
    }

    private void Awake()
    {
        TryAutoAssignLifeSprite();
        needsRebuild = true;
    }

    private void OnEnable()
    {
        TryAutoAssignLifeSprite();
        needsRebuild = true;
        QueueEditorRebuild();
    }

    private void OnValidate()
    {
        previewLives = Mathf.Max(0, previewLives);
        previewMaxLives = Mathf.Max(1, previewMaxLives);
        TryAutoAssignLifeSprite();
        needsRebuild = true;
        QueueEditorRebuild();
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        editorRebuildQueued = false;
#endif
    }

    private void QueueEditorRebuild()
    {
#if UNITY_EDITOR
        if (Application.isPlaying || !buildInEditMode || editorRebuildQueued)
        {
            return;
        }

        editorRebuildQueued = true;
        EditorApplication.delayCall += DelayedEditorRebuild;
#endif
    }

#if UNITY_EDITOR
    private void DelayedEditorRebuild()
    {
        editorRebuildQueued = false;
        if (this == null || !isActiveAndEnabled || Application.isPlaying)
        {
            return;
        }

        needsRebuild = true;
        RefreshUI(Time.realtimeSinceStartup, usePreviewValues: true);
    }
#endif

    [ContextMenu("Build Lives Bubbles In Hierarchy")]
    public void BuildLivesBubblesInHierarchy()
    {
        TryInitialize();
        int lives = Application.isPlaying && livesSystem != null ? livesSystem.CurrentLives : Mathf.Max(0, previewLives);
        int maxLives = Application.isPlaying && livesSystem != null ? livesSystem.MaxLives : Mathf.Max(1, previewMaxLives);
        UpdateBubbles(lives, maxLives, Time.realtimeSinceStartup);
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            if (!buildInEditMode)
            {
                return;
            }

            RefreshUI(Time.realtimeSinceStartup, usePreviewValues: true);

            return;
        }

        RefreshUI(Time.unscaledTime, usePreviewValues: false);
    }

    private void RefreshUI(float time, bool usePreviewValues)
    {
        TryAutoAssignLifeSprite();

        if (needsRebuild)
        {
            TryInitialize();
            needsRebuild = false;
        }

        if (autoFindReferences && livesSystem == null)
        {
            livesSystem = FindFirstObjectByType<LivesSystem>();
        }

        int currentLives = usePreviewValues ? previewLives : (livesSystem != null ? livesSystem.CurrentLives : 0);
        int max = usePreviewValues ? previewMaxLives : (livesSystem != null ? livesSystem.MaxLives : Mathf.Max(1, previewMaxLives));
        UpdateBubbles(currentLives, max, time);
    }

    private void TryInitialize()
    {
        if (!Application.isPlaying && !buildInEditMode)
        {
            return;
        }

        if (canvasRoot == null)
        {
            canvasRoot = transform as RectTransform;
        }

        if (canvasRoot == null)
        {
            return;
        }

        if (autoFindReferences && livesSystem == null)
        {
            livesSystem = FindFirstObjectByType<LivesSystem>();
        }

        EnsureHierarchy();
    }

    private void EnsureHierarchy()
    {
        if (canvasRoot == null)
        {
            return;
        }

        Transform existing = canvasRoot.Find(rootName);
        GameObject rootGo;
        if (existing == null)
        {
            rootGo = new GameObject(rootName, typeof(RectTransform));
            rootRect = rootGo.GetComponent<RectTransform>();
            rootRect.SetParent(canvasRoot, false);
        }
        else
        {
            rootGo = existing.gameObject;
            rootRect = rootGo.GetComponent<RectTransform>();
        }

        rootRect.anchorMin = new Vector2(1f, 1f);
        rootRect.anchorMax = new Vector2(1f, 1f);
        rootRect.pivot = new Vector2(1f, 1f);
        rootRect.anchoredPosition = topRightOffset;
        rootRect.sizeDelta = new Vector2(340f, 92f);

        EnsureLabel();
        EnsureRow();
    }

    private void EnsureLabel()
    {
        Transform existing = rootRect.Find("LivesLabel");
        GameObject go;
        if (existing == null)
        {
            go = new GameObject("LivesLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(rootRect, false);
        }
        else
        {
            go = existing.gameObject;
        }

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(0f, 0f);
        rt.sizeDelta = new Vector2(0f, 26f);

        labelText = go.GetComponent<Text>();
        labelText.text = "LIVES";
        labelText.alignment = TextAnchor.MiddleRight;
        labelText.font = ResolveBuiltinFont();
        labelText.fontSize = 16;
        labelText.fontStyle = FontStyle.Bold;
        labelText.color = labelColor;
        labelText.raycastTarget = false;

        Outline outline = labelText.GetComponent<Outline>();
        if (outline == null)
        {
            outline = labelText.gameObject.AddComponent<Outline>();
        }

        outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
        outline.effectDistance = new Vector2(2f, -2f);
    }

    private void EnsureRow()
    {
        Transform existing = rootRect.Find("LivesRow");
        GameObject go;
        if (existing == null)
        {
            go = new GameObject("LivesRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            go.transform.SetParent(rootRect, false);
        }
        else
        {
            go = existing.gameObject;
        }

        rowRect = go.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0f, 1f);
        rowRect.anchorMax = new Vector2(1f, 1f);
        rowRect.pivot = new Vector2(1f, 1f);
        rowRect.anchoredPosition = new Vector2(0f, -28f);
        rowRect.sizeDelta = new Vector2(0f, bubbleSize.y + 4f);

        HorizontalLayoutGroup layout = go.GetComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleRight;
        layout.spacing = bubbleSpacing;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.reverseArrangement = false;
        layout.padding = new RectOffset(0, 0, 0, 0);
    }

    private void UpdateBubbles(int currentLives, int maxLives, float time)
    {
        if (rowRect == null)
        {
            return;
        }

        currentLives = Mathf.Max(0, currentLives);
        maxLives = Mathf.Max(1, maxLives);

        EnsureBubbleCount(maxLives);

        for (int i = 0; i < bubbles.Count; i++)
        {
            Image bubble = bubbles[i];
            if (bubble == null)
            {
                continue;
            }

            ApplyLifeSprite(bubble);

            bool isAlive = i < currentLives;
            Color baseColor = isAlive ? activeBubbleColor : inactiveBubbleColor;

            if (isAlive && pulseActiveBubbles)
            {
                float pulse = 1f + Mathf.Sin(time * pulseSpeed + i * 0.8f) * pulseAmplitude;
                Color pulsed = baseColor * pulse;
                pulsed.a = baseColor.a;
                bubble.color = pulsed;
            }
            else
            {
                bubble.color = baseColor;
            }

            Outline outline = bubble.GetComponent<Outline>();
            if (outline != null)
            {
                Color ring = bubbleOutlineColor;
                ring.a = isAlive ? bubbleOutlineColor.a : bubbleOutlineColor.a * 0.45f;
                outline.effectColor = ring;
            }
        }
    }

    private void EnsureBubbleCount(int maxLives)
    {
        while (bubbles.Count < maxLives)
        {
            CreateBubble(bubbles.Count);
        }

        while (bubbles.Count > maxLives)
        {
            Image bubble = bubbles[bubbles.Count - 1];
            bubbles.RemoveAt(bubbles.Count - 1);
            if (bubble != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(bubble.gameObject);
                }
                else
                {
                    DestroyImmediate(bubble.gameObject);
                }
            }
        }
    }

    private void CreateBubble(int index)
    {
        GameObject go = new($"Bubble_{index + 1}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(rowRect, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = bubbleSize;

        Image image = go.GetComponent<Image>();
        image.raycastTarget = false;
        image.type = Image.Type.Simple;
        ApplyLifeSprite(image);

        Outline outline = go.GetComponent<Outline>();
        if (outline == null)
        {
            outline = go.AddComponent<Outline>();
        }

        outline.effectColor = bubbleOutlineColor;
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        Shadow glow = go.GetComponent<Shadow>();
        if (glow == null)
        {
            glow = go.AddComponent<Shadow>();
        }

        glow.effectColor = new Color(0.1f, 0.95f, 1f, 0.35f);
        glow.effectDistance = new Vector2(0f, -1f);
        glow.useGraphicAlpha = true;

        bubbles.Add(image);
    }

    private void ApplyLifeSprite(Image image)
    {
        if (image == null)
        {
            return;
        }

        Sprite resolvedSprite = ResolveLifeSprite();
        image.sprite = resolvedSprite;
        image.preserveAspect = resolvedSprite != null;
    }

    private Sprite ResolveLifeSprite()
    {
        if (lifeSprite != null)
        {
            return lifeSprite;
        }

        if (string.IsNullOrWhiteSpace(fallbackLifeSpriteName))
        {
            return null;
        }

        Sprite[] loadedSprites = Resources.FindObjectsOfTypeAll<Sprite>();
        for (int i = 0; i < loadedSprites.Length; i++)
        {
            Sprite candidate = loadedSprites[i];
            if (candidate != null && string.Equals(candidate.name, fallbackLifeSpriteName, StringComparison.OrdinalIgnoreCase))
            {
                lifeSprite = candidate;
                return lifeSprite;
            }
        }

        return null;
    }

    private void TryAutoAssignLifeSprite()
    {
        if (!autoAssignLifeSprite || lifeSprite != null)
        {
            return;
        }

#if UNITY_EDITOR
        const string PreferredPath = "Assets/Textures/life.png";
        lifeSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PreferredPath);

        if (lifeSprite == null && !string.IsNullOrWhiteSpace(fallbackLifeSpriteName))
        {
            string[] guids = AssetDatabase.FindAssets($"{fallbackLifeSpriteName} t:Sprite");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                lifeSprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
        }

        if (lifeSprite != null)
        {
            EditorUtility.SetDirty(this);
        }
#else
        lifeSprite = ResolveLifeSprite();
#endif
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
