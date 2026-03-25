using System;
using UnityEngine;
using Mindrift.World;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Mindrift.Player
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonMotor : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private Transform movementReference;
        [SerializeField] private MindriftPlayerInputRouter inputRouter;
        [SerializeField] private float moveSpeed = 8f;
        [SerializeField] private float acceleration = 30f;
        [SerializeField, Range(0f, 1f)] private float airControl = 0.4f;

        [Header("Jump + Gravity")]
        [SerializeField] private float jumpVelocity = 11f;
        [SerializeField] private float gravity = -22f;
        [SerializeField] private float groundedSnapVelocity = -4f;
        [SerializeField] private float coyoteTime = 0.1f;
        [SerializeField] private float jumpBufferTime = 0.12f;

        [Header("Debug")]
        [SerializeField] private bool showDebug;

        private CharacterController characterController;
        private Vector3 planarVelocity;
        private float verticalVelocity;
        private float coyoteTimer;
        private float jumpBufferTimer;
        private bool wasGrounded;

        private MovingPlatform currentGroundPlatform;

        public bool IsGrounded => characterController != null && characterController.isGrounded;
        public Vector3 Velocity => planarVelocity + Vector3.up * verticalVelocity;
        public CharacterController CharacterController => characterController;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            if (movementReference == null)
            {
                movementReference = transform;
            }

            if (inputRouter == null)
            {
                inputRouter = GetComponent<MindriftPlayerInputRouter>();
            }
        }

        private void Update()
        {
            if (characterController == null)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            UpdateTimers(deltaTime);
            ApplyMovingPlatformDelta();
            HandleMovement(deltaTime);
            HandleJumpAndGravity(deltaTime);
            CommitMovement(deltaTime);
            currentGroundPlatform = null;
        }

        public void ResetVelocity()
        {
            planarVelocity = Vector3.zero;
            verticalVelocity = 0f;
            coyoteTimer = 0f;
            jumpBufferTimer = 0f;
        }

        public void TeleportTo(Vector3 worldPosition)
        {
            if (characterController == null)
            {
                transform.position = worldPosition;
                return;
            }

            bool wasEnabled = characterController.enabled;
            characterController.enabled = false;
            transform.position = worldPosition;
            characterController.enabled = wasEnabled;
            ResetVelocity();
        }

        private void UpdateTimers(float deltaTime)
        {
            bool jumpPressed = inputRouter != null ? inputRouter.WasJumpPressedThisFrame() : ReadJumpFallback();
            if (jumpPressed)
            {
                jumpBufferTimer = jumpBufferTime;
            }
            else
            {
                jumpBufferTimer -= deltaTime;
            }

            if (characterController.isGrounded)
            {
                coyoteTimer = coyoteTime;
            }
            else
            {
                coyoteTimer -= deltaTime;
            }
        }

        private void HandleMovement(float deltaTime)
        {
            Vector2 inputVector = inputRouter != null ? inputRouter.ReadMove() : ReadMoveFallback();
            float inputX = inputVector.x;
            float inputZ = inputVector.y;

            Vector3 rawInput = Vector3.ClampMagnitude(new Vector3(inputX, 0f, inputZ), 1f);
            Vector3 referenceForward = movementReference.forward;
            Vector3 referenceRight = movementReference.right;
            referenceForward.y = 0f;
            referenceRight.y = 0f;
            referenceForward.Normalize();
            referenceRight.Normalize();

            Vector3 desiredPlanarVelocity = (referenceForward * rawInput.z + referenceRight * rawInput.x) * moveSpeed;
            float currentAcceleration = characterController.isGrounded ? acceleration : acceleration * airControl;
            planarVelocity = Vector3.MoveTowards(planarVelocity, desiredPlanarVelocity, currentAcceleration * deltaTime);
        }

        private void HandleJumpAndGravity(float deltaTime)
        {
            bool grounded = characterController.isGrounded;

            if (grounded && verticalVelocity < groundedSnapVelocity)
            {
                verticalVelocity = groundedSnapVelocity;
            }

            bool canJump = coyoteTimer > 0f && jumpBufferTimer > 0f;
            if (canJump)
            {
                verticalVelocity = jumpVelocity;
                jumpBufferTimer = 0f;
                coyoteTimer = 0f;
            }

            verticalVelocity += gravity * deltaTime;
        }

        private void CommitMovement(float deltaTime)
        {
            Vector3 frameMotion = (planarVelocity + Vector3.up * verticalVelocity) * deltaTime;
            CollisionFlags flags = characterController.Move(frameMotion);

            bool groundedNow = (flags & CollisionFlags.Below) != 0 || characterController.isGrounded;
            if (groundedNow && !wasGrounded && showDebug)
            {
                Debug.Log("[MINDRIFT] Player grounded.");
            }

            wasGrounded = groundedNow;
        }

        private void ApplyMovingPlatformDelta()
        {
            if (currentGroundPlatform == null || !characterController.isGrounded)
            {
                return;
            }

            Vector3 delta = currentGroundPlatform.FrameDelta;
            if (delta.sqrMagnitude > 0f)
            {
                characterController.Move(delta);
            }
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (hit.normal.y < 0.55f)
            {
                return;
            }

            MovingPlatform platform = hit.collider.GetComponentInParent<MovingPlatform>();
            if (platform != null)
            {
                currentGroundPlatform = platform;
            }
        }

        private static Vector2 ReadMoveFallback()
        {
#if ENABLE_INPUT_SYSTEM
            Vector2 move = Vector2.zero;

            if (Keyboard.current != null)
            {
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) move.x -= 1f;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) move.x += 1f;
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) move.y -= 1f;
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) move.y += 1f;
            }

            if (Gamepad.current != null)
            {
                move += Gamepad.current.leftStick.ReadValue();
            }

            return Vector2.ClampMagnitude(move, 1f);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#else
            return Vector2.zero;
#endif
        }

        private static bool ReadJumpFallback()
        {
#if ENABLE_INPUT_SYSTEM
            bool keyboardJump = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
            bool gamepadJump = Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame;
            return keyboardJump || gamepadJump;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetButtonDown("Jump");
#else
            return false;
#endif
        }
    }

    public class MindriftPlayerInputRouter : MonoBehaviour
    {
        [Header("Input Source")]
#if ENABLE_INPUT_SYSTEM
        [SerializeField] private PlayerInput playerInput;
        [SerializeField] private InputActionAsset fallbackActions;
#endif
        [SerializeField] private string actionMapName = "Player";
        [SerializeField] private string moveActionName = "Move";
        [SerializeField] private string lookActionName = "Look";
        [SerializeField] private string jumpActionName = "Jump";

#if ENABLE_INPUT_SYSTEM
        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction jumpAction;
        private InputActionMap resolvedMap;
        private bool mapEnabledByRouter;
#endif

        private void Awake()
        {
#if ENABLE_INPUT_SYSTEM
            if (playerInput == null)
            {
                playerInput = GetComponent<PlayerInput>();
            }
#endif
        }

        private void OnEnable()
        {
#if ENABLE_INPUT_SYSTEM
            ResolveActions();
#endif
        }

        private void OnDisable()
        {
#if ENABLE_INPUT_SYSTEM
            if (mapEnabledByRouter && resolvedMap != null && resolvedMap.enabled)
            {
                resolvedMap.Disable();
            }

            mapEnabledByRouter = false;
#endif
        }

        public Vector2 ReadMove()
        {
#if ENABLE_INPUT_SYSTEM
            if (moveAction != null)
            {
                return moveAction.ReadValue<Vector2>();
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#else
            return Vector2.zero;
#endif
        }

        public Vector2 ReadLook()
        {
#if ENABLE_INPUT_SYSTEM
            if (lookAction != null)
            {
                return lookAction.ReadValue<Vector2>();
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
#else
            return Vector2.zero;
#endif
        }

        public bool WasJumpPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            if (jumpAction != null)
            {
                return jumpAction.WasPressedThisFrame();
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetButtonDown("Jump");
#else
            return false;
#endif
        }

        public bool IsLookFromPointerDevice()
        {
#if ENABLE_INPUT_SYSTEM
            return lookAction != null && lookAction.activeControl != null && lookAction.activeControl.device is Pointer;
#else
            return true;
#endif
        }

#if ENABLE_INPUT_SYSTEM
        public PlayerInput GetPlayerInput()
        {
            return playerInput;
        }

        private void ResolveActions()
        {
            mapEnabledByRouter = false;
            resolvedMap = null;
            moveAction = null;
            lookAction = null;
            jumpAction = null;

            InputActionMap map = null;
            if (playerInput != null && playerInput.actions != null)
            {
                map = playerInput.actions.FindActionMap(actionMapName, false);
            }

            if (map == null && fallbackActions != null)
            {
                map = fallbackActions.FindActionMap(actionMapName, false);
            }

            if (map == null)
            {
                return;
            }

            resolvedMap = map;
            moveAction = map.FindAction(moveActionName, false);
            lookAction = map.FindAction(lookActionName, false);
            jumpAction = map.FindAction(jumpActionName, false);

            if (!map.enabled)
            {
                map.Enable();
                mapEnabledByRouter = true;
            }
        }
#endif
    }

    [Obsolete("Use MindriftPlayerInputRouter instead.")]
    public sealed class UpsidePlayerInputRouter : MindriftPlayerInputRouter
    {
    }
}
