using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public sealed class CoinRushPlayerController : MonoBehaviour
{
    [SerializeField, Min(0)] private float walkSpeed = 4f;
    [SerializeField, Min(0)] private float runSpeed = 7f;
    [SerializeField] private float gravity = -25f;
    [SerializeField, Min(0)] private float jumpHeight = 1.5f;
    [SerializeField] private AudioSource footstepSource;
    [SerializeField] private CoinRushGameManager footstepGame;
    public AudioSource FootstepSource => footstepSource;

    public void ConfigureFootsteps(AudioSource source, CoinRushGameManager game)
    {
        footstepSource = source;
        footstepGame = game;
    }

    private CharacterController controller;
    private float verticalSpeed;
    public float VerticalSpeed { get => verticalSpeed; set => verticalSpeed = value; }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit.normal.y > 0.5f)
        {
            CoinRushCourseElement platform = hit.collider.GetComponent<CoinRushCourseElement>();
            if (platform != null) platform.StepOn();
        }
    }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        // detectCollisions is not serialized with the scene; set it at runtime too.
        controller.detectCollisions = true;
        Rigidbody body = GetComponent<Rigidbody>();
        if (body != null)
        {
            body.isKinematic = true;
            body.useGravity = false;
            Debug.LogWarning("Coin Rush: Rigidbody desativado para nao disputar movimento com o CharacterController. Execute Criar Jogador para remove-lo.", this);
        }
        Physics.SyncTransforms();
    }
    private void OnEnable()
    {
        verticalSpeed = 0;
        if (controller != null) controller.detectCollisions = true;
    }

    private void Update()
    {
        if (!controller.enabled) { StopFootsteps(); return; }
        Vector3 positionBeforeMove = transform.position;
        Keyboard keyboard = Keyboard.current;
        Vector2 input = Vector2.zero;
        if (keyboard != null && Application.isFocused)
        {
            input.x = (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0);
            input.y = (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0);
        }
        input = Vector2.ClampMagnitude(input, 1f);
        // Only mouse look rotates the player. A/D strafe without turning the body.
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        Vector3 direction = Vector3.ClampMagnitude(forward * input.y + right * input.x, 1f);
        float acceleration = Mathf.Min(gravity, -0.01f);
        if (Application.isFocused && keyboard != null && keyboard.spaceKey.wasPressedThisFrame
            && controller.isGrounded && verticalSpeed <= 0f && Time.deltaTime > 0f)
            verticalSpeed = Mathf.Sqrt(-2f * acceleration * jumpHeight);

        float speed = keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed)
            ? runSpeed : walkSpeed;
        // Small movement steps keep collision sweeps short even during a long frame.
        float remaining = Mathf.Min(Time.deltaTime, 0.1f);
        while (remaining > 0f)
        {
            float step = Mathf.Min(remaining, 1f / 60f);
            if (controller.isGrounded && verticalSpeed < 0) verticalSpeed = -2f;
            verticalSpeed = Mathf.Max(verticalSpeed + acceleration * step, -50f);
            CollisionFlags collisions = controller.Move((direction * speed + Vector3.up * verticalSpeed) * step);
            if ((collisions & CollisionFlags.Above) != 0 && verticalSpeed > 0) verticalSpeed = 0;
            if ((collisions & CollisionFlags.Below) != 0) verticalSpeed = -2f;
            remaining -= step;
        }
        Vector3 displacement = transform.position - positionBeforeMove;
        displacement.y = 0;
        bool movingOnGround = direction.sqrMagnitude > 0.001f && controller.isGrounded && verticalSpeed <= 0f
            && Time.deltaTime > 0f && displacement.magnitude / Time.deltaTime > 0.1f;
        bool running = keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
        UpdateFootsteps(movingOnGround, running);
    }

    private void UpdateFootsteps(bool movingOnGround, bool running)
    {
        if (footstepSource == null) return;
        if (!movingOnGround || !Application.isFocused || Time.timeScale <= 0f
            || (footstepGame != null && !footstepGame.IsRunning))
        { StopFootsteps(); return; }
        footstepSource.pitch = running ? 1.35f : 1f;
        if (!footstepSource.isPlaying && footstepSource.clip != null) footstepSource.Play();
    }

    private void StopFootsteps()
    {
        if (footstepSource != null) footstepSource.Stop();
    }

    private void OnDisable() => StopFootsteps();
    private void OnApplicationFocus(bool focused)
    {
        if (!focused) StopFootsteps();
    }
}
