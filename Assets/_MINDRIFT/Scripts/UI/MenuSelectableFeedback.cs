using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Mindrift.UI
{
    public sealed class MenuSelectableFeedback : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private Graphic targetGraphic;
        [SerializeField] private float selectedScale = 1.035f;
        [SerializeField] private float pressedScale = 0.97f;
        [SerializeField] private float lerpSpeed = 18f;
        [SerializeField] private Color selectedTint = new Color(0.2f, 0.95f, 1f, 0.96f);
        [SerializeField] private float glitchPulse = 0.06f;

        private RectTransform cachedRectTransform;
        private Vector3 baseScale = Vector3.one;
        private Color baseColor = Color.white;
        private bool isSelected;
        private bool isPointerInside;
        private bool isPressed;

        private void Awake()
        {
            cachedRectTransform = transform as RectTransform;
            if (cachedRectTransform != null)
            {
                baseScale = cachedRectTransform.localScale;
            }

            if (targetGraphic == null)
            {
                targetGraphic = GetComponent<Graphic>();
            }

            if (targetGraphic != null)
            {
                baseColor = targetGraphic.color;
            }
        }

        private void OnEnable()
        {
            if (cachedRectTransform != null)
            {
                cachedRectTransform.localScale = baseScale;
            }

            if (targetGraphic != null)
            {
                targetGraphic.color = baseColor;
            }
        }

        private void Update()
        {
            if (cachedRectTransform == null)
            {
                return;
            }

            float targetScale = 1f;
            if (isPressed)
            {
                targetScale = pressedScale;
            }
            else if (isSelected || isPointerInside)
            {
                targetScale = selectedScale;
            }

            Vector3 desired = baseScale * targetScale;
            cachedRectTransform.localScale = Vector3.Lerp(cachedRectTransform.localScale, desired, Time.unscaledDeltaTime * lerpSpeed);

            if (targetGraphic == null)
            {
                return;
            }

            if (isSelected || isPointerInside)
            {
                float pulse = 1f + Mathf.Sin(Time.unscaledTime * 12f) * glitchPulse;
                Color glow = selectedTint * pulse;
                glow.a = Mathf.Clamp01(selectedTint.a);
                targetGraphic.color = Color.Lerp(targetGraphic.color, glow, Time.unscaledDeltaTime * lerpSpeed);
            }
            else
            {
                targetGraphic.color = Color.Lerp(targetGraphic.color, baseColor, Time.unscaledDeltaTime * lerpSpeed);
            }
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
    }
}
