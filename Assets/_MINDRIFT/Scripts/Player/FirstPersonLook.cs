using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Mindrift.Player
{
    public sealed class FirstPersonLook : MonoBehaviour
    {
        [Header("Look References")]
        [SerializeField] private Transform yawTransform;
        [SerializeField] private Transform pitchTransform;
        [SerializeField] private MindriftPlayerInputRouter inputRouter;

        [Header("Sensitivity")]
        [SerializeField] private float mouseSensitivityX = 2f;
        [SerializeField] private float mouseSensitivityY = 2f;
        [SerializeField] private float gamepadLookSpeedX = 190f;
        [SerializeField] private float gamepadLookSpeedY = 150f;
        [SerializeField, Range(0f, 0.5f)] private float gamepadLookDeadzone = 0.08f;
        [SerializeField] private bool invertY;
        [SerializeField] private float smoothing = 18f;

        [Header("Pitch Limits")]
        [SerializeField] private float minPitch = -85f;
        [SerializeField] private float maxPitch = 85f;

        [Header("Cursor")]
        [SerializeField] private bool lockCursorOnStart = true;
        [SerializeField] private KeyCode unlockKey = KeyCode.Escape;

        private float yaw;
        private float pitch;
        private Vector2 smoothedInput;
        private bool controllerDefaultsCached;
        private float defaultGamepadLookSpeedX;
        private float defaultGamepadLookSpeedY;

        private void Awake()
        {
            if (yawTransform == null)
            {
                yawTransform = transform;
            }

            if (pitchTransform == null)
            {
                pitchTransform = transform;
            }

            if (inputRouter == null)
            {
                inputRouter = GetComponent<MindriftPlayerInputRouter>();
            }

            Vector3 yawEuler = yawTransform.localEulerAngles;
            Vector3 pitchEuler = pitchTransform.localEulerAngles;
            yaw = yawEuler.y;
            pitch = NormalizePitch(pitchEuler.x);

            CacheControllerDefaults();
        }

        private void Start()
        {
            if (lockCursorOnStart)
            {
                SetCursorLock(true);
            }
        }

        private void Update()
        {
            bool unlockPressed = IsUnlockPressed();
            if (unlockPressed)
            {
                SetCursorLock(false);
            }

            if (Cursor.lockState != CursorLockMode.Locked && IsPointerPrimaryPressed())
            {
                SetCursorLock(true);
            }

            Vector2 rawLook;
            bool pointerInput;
            if (inputRouter != null)
            {
                rawLook = inputRouter.ReadLook();
                pointerInput = inputRouter.IsLookFromPointerDevice();
            }
            else
            {
                rawLook = ReadLookFallback(out pointerInput);
            }

            if (pointerInput && Cursor.lockState != CursorLockMode.Locked)
            {
                return;
            }

            float deltaX;
            float deltaY;
            if (pointerInput)
            {
                deltaX = rawLook.x * mouseSensitivityX;
                deltaY = rawLook.y * mouseSensitivityY * (invertY ? 1f : -1f);
            }
            else
            {
                Vector2 filtered = rawLook.sqrMagnitude <= gamepadLookDeadzone * gamepadLookDeadzone ? Vector2.zero : rawLook;
                deltaX = filtered.x * gamepadLookSpeedX * Time.deltaTime;
                deltaY = filtered.y * gamepadLookSpeedY * Time.deltaTime * (invertY ? 1f : -1f);
            }

            Vector2 targetInput = new Vector2(deltaX, deltaY);
            smoothedInput = Vector2.Lerp(smoothedInput, targetInput, 1f - Mathf.Exp(-smoothing * Time.deltaTime));

            yaw += smoothedInput.x;
            pitch = Mathf.Clamp(pitch + smoothedInput.y, minPitch, maxPitch);

            yawTransform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            pitchTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        public void SetCursorLock(bool shouldLock)
        {
            Cursor.visible = !shouldLock;
            Cursor.lockState = shouldLock ? CursorLockMode.Locked : CursorLockMode.None;
        }

        public void ApplyControllerOptions(float sensitivityMultiplier, bool invertYAxis, float deadzone)
        {
            CacheControllerDefaults();

            float safeMultiplier = Mathf.Clamp(sensitivityMultiplier, 0.4f, 2f);
            gamepadLookSpeedX = defaultGamepadLookSpeedX * safeMultiplier;
            gamepadLookSpeedY = defaultGamepadLookSpeedY * safeMultiplier;
            gamepadLookDeadzone = Mathf.Clamp(deadzone, 0f, 0.5f);
            invertY = invertYAxis;
        }

        private bool IsUnlockPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && TryMapKeyCode(unlockKey, out Key mappedKey))
            {
                var keyControl = Keyboard.current[mappedKey];
                if (keyControl != null && keyControl.wasPressedThisFrame)
                {
                    return true;
                }
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(unlockKey);
#else
            return false;
#endif
        }

        private static bool IsPointerPrimaryPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                return true;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(0);
#else
            return false;
#endif
        }

        private static bool TryMapKeyCode(KeyCode keyCode, out Key key)
        {
            switch (keyCode)
            {
                case KeyCode.Escape:
                    key = Key.Escape;
                    return true;
                case KeyCode.Tab:
                    key = Key.Tab;
                    return true;
                case KeyCode.Space:
                    key = Key.Space;
                    return true;
                case KeyCode.BackQuote:
                    key = Key.Backquote;
                    return true;
                default:
                    key = Key.None;
                    return false;
            }
        }

        private static Vector2 ReadLookFallback(out bool pointerInput)
        {
#if ENABLE_INPUT_SYSTEM
            Vector2 mouseDelta = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
            Vector2 gamepadLook = Gamepad.current != null ? Gamepad.current.rightStick.ReadValue() : Vector2.zero;

            if (gamepadLook.sqrMagnitude > 0.0005f)
            {
                pointerInput = false;
                return gamepadLook;
            }

            pointerInput = true;
            return mouseDelta;
#elif ENABLE_LEGACY_INPUT_MANAGER
            pointerInput = true;
            return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
#else
            pointerInput = true;
            return Vector2.zero;
#endif
        }

        private static float NormalizePitch(float value)
        {
            if (value > 180f)
            {
                value -= 360f;
            }

            return value;
        }

        private void CacheControllerDefaults()
        {
            if (controllerDefaultsCached)
            {
                return;
            }

            controllerDefaultsCached = true;
            defaultGamepadLookSpeedX = gamepadLookSpeedX;
            defaultGamepadLookSpeedY = gamepadLookSpeedY;
        }
    }
}
