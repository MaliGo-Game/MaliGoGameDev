using UnityEngine;
using MaliGo.Characters;
using MaliGo.UI.Kit;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Rigidbody))]
public class MaliGoPlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 4.5f;
    public float acceleration = 25f;
    public float deceleration = 30f;
    public float gravity = -18f;

    [Header("Input Source")]
    public bool useKeyboardInput = true;
    public Vector2 virtualJoystickInput = Vector2.zero;

    [Header("Visual")]
    public CharacterSpriteController spriteController;
    public PlayerCharacterVisualController visualController;

    private CharacterController characterController;
    private Vector3 currentHorizontalVelocity = Vector3.zero;
    private float verticalVelocity = 0f;
    private Vector2 lastNonZeroInput = Vector2.up;

    void Awake()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
        gameObject.tag = "Player";
    }

    void Start()
    {
        characterController = GetComponent<CharacterController>();

        if (spriteController == null)
            spriteController = GetComponentInChildren<CharacterSpriteController>();

        if (visualController == null)
            visualController = GetComponentInChildren<PlayerCharacterVisualController>();
    }

    void Update()
    {
        HandleMovement();
    }

    private void HandleMovement()
    {
        Vector2 input = GetMovementInput();

        if (input.sqrMagnitude > 0.01f)
        {
            lastNonZeroInput = input.normalized;
        }

        // Screen-relative Isometric direction calculation
        // Project Camera.main's forward & right onto the horizontal XZ plane
        Vector3 camForward = Vector3.forward;
        Vector3 camRight = Vector3.right;

        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            camForward = Vector3.ProjectOnPlane(mainCam.transform.forward, Vector3.up).normalized;
            camRight = Vector3.ProjectOnPlane(mainCam.transform.right, Vector3.up).normalized;
        }

        // Isometric direction: W/Up moves toward upper screen, D/Right moves toward right screen
        Vector3 targetMoveDirection = (camRight * input.x + camForward * input.y);
        if (targetMoveDirection.sqrMagnitude > 1f)
        {
            targetMoveDirection.Normalize();
        }

        // Smooth acceleration & deceleration
        Vector3 targetVelocity = targetMoveDirection * moveSpeed;
        float rate = (input.sqrMagnitude > 0.01f) ? acceleration : deceleration;
        currentHorizontalVelocity = Vector3.MoveTowards(currentHorizontalVelocity, targetVelocity, rate * Time.deltaTime);

        // Grounding & Gravity
        if (characterController != null)
        {
            if (characterController.isGrounded)
            {
                if (verticalVelocity < 0f)
                {
                    verticalVelocity = -2f; // Keep grounded against slopes
                }
            }
            else
            {
                verticalVelocity += gravity * Time.deltaTime;
            }

            Vector3 finalMoveVector = currentHorizontalVelocity + (Vector3.up * verticalVelocity);
            characterController.Move(finalMoveVector * Time.deltaTime);
        }

        // Visual feedback for human player or legacy sprite characters.
        bool isMoving = currentHorizontalVelocity.magnitude > 0.1f && input.sqrMagnitude > 0.01f;
        if (visualController != null)
        {
            visualController.UpdateVisual(input, isMoving, currentHorizontalVelocity);
        }
        else if (spriteController != null)
        {
            spriteController.UpdateAnimation(input, isMoving);
        }
    }

    public Vector2 GetMovementInput()
    {
        Vector2 input = Vector2.zero;

        // No walking while a sheet, Mali's blocking box, the reveal or pause is open (DESIGN_SPEC 7.2).
        if (UiModal.IsAnyOpen)
        {
            return input;
        }

        if (useKeyboardInput)
        {
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) input.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) input.y -= 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) input.x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input.x += 1f;
            }
#endif
        }

        // Add virtual mobile joystick input if provided
        input += virtualJoystickInput;

        return Vector2.ClampMagnitude(input, 1f);
    }

    /// <summary>
    /// External entrypoint for mobile on-screen joystick or UI touch pad
    /// </summary>
    public void SetVirtualJoystickInput(Vector2 joystickVector)
    {
        virtualJoystickInput = Vector2.ClampMagnitude(joystickVector, 1f);
    }

    public void InteractWithBuilding(string buildingType)
    {
        Debug.Log($"Interacting with {buildingType}");
    }
}