using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Mindrift.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class MainMenuButtonFx : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private Graphic targetGraphic;
        [SerializeField] private Text label;
        [SerializeField] private float selectedScale = 1.03f;
        [SerializeField] private float pressedScale = 0.98f;
        [SerializeField] private float lerpSpeed = 20f;
        [SerializeField] private Color idleFill = new Color(0.03f, 0.05f, 0.08f, 0.88f);
        [SerializeField] private Color selectedFill = new Color(0.07f, 0.12f, 0.18f, 0.96f);
        [SerializeField] private Color pressedFill = new Color(0.16f, 0.28f, 0.36f, 0.98f);

        private RectTransform rectTransform;
        private Vector3 baseScale;
        private Outline borderOutline;
        private Shadow glowShadow;
        private Text cyanGhost;
        private Text magentaGhost;
        private RectTransform labelRect;
        private bool isSelected;
        private bool isPointerInside;
        private bool isPressed;

        private void Awake()
        {
            rectTransform = transform as RectTransform;
            if (rectTransform != null)
            {
                baseScale = rectTransform.localScale;
            }

            if (targetGraphic == null)
            {
                targetGraphic = GetComponent<Graphic>();
            }

            if (label == null)
            {
                label = GetComponentInChildren<Text>(true);
            }

            if (targetGraphic != null)
            {
                borderOutline = targetGraphic.GetComponent<Outline>();
                if (borderOutline == null)
                {
                    borderOutline = targetGraphic.gameObject.AddComponent<Outline>();
                }

                glowShadow = targetGraphic.GetComponent<Shadow>();
                if (glowShadow == null)
                {
                    glowShadow = targetGraphic.gameObject.AddComponent<Shadow>();
                }
            }

            if (label != null)
            {
                EnsureGhosts();
            }
        }

        private void OnEnable()
        {
            if (rectTransform != null)
            {
                rectTransform.localScale = baseScale;
            }

            ApplyVisualState(force: true);
        }

        private void LateUpdate()
        {
            ApplyVisualState(force: false);
        }

        public void OnSelect(BaseEventData eventData)
        {
            isSelected = true;
        }

        public void OnDeselect(BaseEventData eventData)
        {
            isSelected = false;
            isPressed = false;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isPointerInside = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isPointerInside = false;
            isPressed = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            isPressed = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            isPressed = false;
        }

        private void ApplyVisualState(bool force)
        {
            float delta = force ? 1f : Time.unscaledDeltaTime * lerpSpeed;
            bool highlighted = isSelected || isPointerInside;

            if (rectTransform != null)
            {
                float scale = isPressed ? pressedScale : (highlighted ? selectedScale : 1f);
                rectTransform.localScale = Vector3.Lerp(rectTransform.localScale, baseScale * scale, delta);
            }

            if (targetGraphic != null)
            {
                Color targetColor = isPressed ? pressedFill : (highlighted ? selectedFill : idleFill);
                targetGraphic.color = Color.Lerp(targetGraphic.color, targetColor, delta);
            }

            if (borderOutline != null)
            {
                borderOutline.effectColor = highlighted ? new Color(0.12f, 1f, 0.98f, 0.82f) : new Color(0.12f, 1f, 0.98f, 0.5f);
                borderOutline.effectDistance = new Vector2(1f, -1f);
                borderOutline.useGraphicAlpha = true;
            }

            if (glowShadow != null)
            {
                glowShadow.effectColor = highlighted ? new Color(0.12f, 1f, 0.98f, 0.28f) : new Color(0.12f, 1f, 0.98f, 0f);
                glowShadow.effectDistance = highlighted ? new Vector2(0f, 0f) : Vector2.zero;
                glowShadow.useGraphicAlpha = true;
            }

            UpdateGhosts(highlighted);
        }

        private void EnsureGhosts()
        {
            if (label == null || label.transform.parent == null)
            {
                return;
            }

            labelRect = label.rectTransform;
            cyanGhost = GetOrCreateGhost("RGBCyanGhost", new Color(0.12f, 1f, 0.98f, 0f));
            magentaGhost = GetOrCreateGhost("RGBMagentaGhost", new Color(1f, 0.18f, 0.72f, 0f));
        }

        private Text GetOrCreateGhost(string objectName, Color color)
        {
            Transform existing = label.transform.parent.Find(objectName);
            Text ghost;
            if (existing == null)
            {
                GameObject ghostObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                ghostObject.transform.SetParent(label.transform.parent, false);
                ghostObject.transform.SetSiblingIndex(label.transform.GetSiblingIndex());
                ghost = ghostObject.GetComponent<Text>();
            }
            else
            {
                ghost = existing.GetComponent<Text>();
            }

            ghost.font = label.font;
            ghost.fontSize = label.fontSize;
            ghost.fontStyle = label.fontStyle;
            ghost.alignment = label.alignment;
            ghost.raycastTarget = false;
            ghost.color = color;
            ghost.text = label.text;
            return ghost;
        }

        private void UpdateGhosts(bool highlighted)
        {
            if (label == null || labelRect == null)
            {
                return;
            }

            if (cyanGhost == null || magentaGhost == null)
            {
                EnsureGhosts();
            }

            SyncGhost(cyanGhost, new Vector2(1f, -1f), highlighted ? 0.3f : 0f);
            SyncGhost(magentaGhost, new Vector2(-1f, 1f), highlighted ? 0.28f : 0f);
        }

        private void SyncGhost(Text ghost, Vector2 offset, float alpha)
        {
            if (ghost == null || labelRect == null)
            {
                return;
            }

            RectTransform ghostRect = ghost.rectTransform;
            ghostRect.anchorMin = labelRect.anchorMin;
            ghostRect.anchorMax = labelRect.anchorMax;
            ghostRect.pivot = labelRect.pivot;
            ghostRect.sizeDelta = labelRect.sizeDelta;
            ghostRect.anchoredPosition = labelRect.anchoredPosition + offset;

            ghost.font = label.font;
            ghost.fontSize = label.fontSize;
            ghost.fontStyle = label.fontStyle;
            ghost.alignment = label.alignment;
            ghost.text = label.text;

            Color color = ghost.color;
            color.a = alpha;
            ghost.color = color;
        }
    }
}
