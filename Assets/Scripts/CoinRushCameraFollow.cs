using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using System.Collections.Generic;

[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class CoinRushCameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField, Min(0)] private float eyeHeight = 1.6f;
    [SerializeField, Min(0)] private float mouseSensitivity = 0.12f;
    private CharacterController character;
    [SerializeField] private Transform characterVisual;
    private readonly Dictionary<Renderer, ShadowCastingMode> hiddenRenderers = new Dictionary<Renderer, ShadowCastingMode>();
    public void SetCharacterVisual(Transform visual)
    {
        RestoreBody();
        characterVisual = visual;
        if (Application.isPlaying && target != null) HideBody();
    }
    private float yaw;
    private float pitch;
    private bool initialized;
    private bool ownsCursor;
    public Vector2 LookAngles => new Vector2(yaw, pitch);
    public void RestoreLook(Vector2 angles)
    {
        yaw = angles.x;
        pitch = Mathf.Clamp(angles.y, -85, 85);
        if (target != null) target.rotation = Quaternion.Euler(0, yaw, 0);
        SnapToTarget();
    }

    public void SetTarget(Transform value)
    {
        RestoreBody();
        target = value;
        initialized = false;
        SnapToTarget();
        if (Application.isPlaying && isActiveAndEnabled && target != null) CaptureCursor();
    }

    private void OnEnable()
    {
        initialized = false;
        SnapToTarget();
        if (Application.isPlaying && target != null) CaptureCursor();
    }

    public void SnapToTarget()
    {
        if (target == null) return;
        if (!initialized)
        {
            yaw = target.eulerAngles.y;
            pitch = 0f;
            character = target.GetComponent<CharacterController>();
            initialized = true;
        }
        if (Application.isPlaying) HideBody();
        ApplyFirstPersonView();
    }

    private void Update()
    {
        if (target == null || !Application.isFocused) return;
        Mouse mouse = Mouse.current;
        if (mouse == null) return;
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            // Allows returning to the Game view after the Editor releases the cursor.
            if (mouse.leftButton.wasPressedThisFrame) CaptureCursor();
            return;
        }
        Vector2 delta = mouse.delta.ReadValue();
        // Mouse delta already represents movement this frame; do not multiply by deltaTime.
        yaw = Mathf.Repeat(yaw + delta.x * mouseSensitivity, 360f);
        pitch = Mathf.Clamp(pitch - delta.y * mouseSensitivity, -85f, 85f);
        // Runs before movement: mouse yaw turns the body, pitch belongs only to the camera.
        target.rotation = Quaternion.Euler(0, yaw, 0);
        transform.rotation = Quaternion.Euler(pitch, yaw, 0);
    }

    private void LateUpdate()
    {
        if (target == null) return;
        ApplyFirstPersonView();
    }

    private void ApplyFirstPersonView()
    {
        // The capsule pivot is at its center, not at its feet.
        float baseY = character != null ? character.center.y - character.height * 0.5f : -1f;
        Vector3 eyes = target.TransformPoint(new Vector3(0, baseY + eyeHeight, 0));
        transform.SetPositionAndRotation(eyes, Quaternion.Euler(pitch, target.eulerAngles.y, 0));
    }

    private void HideBody()
    {
        HideRenderer(target.GetComponent<Renderer>());
        if (characterVisual != null)
            foreach (Renderer renderer in characterVisual.GetComponentsInChildren<Renderer>(true)) HideRenderer(renderer);
    }

    private void HideRenderer(Renderer renderer)
    {
        if (renderer == null || hiddenRenderers.ContainsKey(renderer)) return;
        hiddenRenderers.Add(renderer, renderer.shadowCastingMode);
        renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
    }

    private void RestoreBody()
    {
        foreach (var entry in hiddenRenderers)
            if (entry.Key != null) entry.Key.shadowCastingMode = entry.Value;
        hiddenRenderers.Clear();
    }

    private void CaptureCursor()
    {
        if (!Application.isFocused) return;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        ownsCursor = true;
    }

    private void ReleaseCursor()
    {
        if (!ownsCursor) return;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        ownsCursor = false;
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused) ReleaseCursor();
        else if (isActiveAndEnabled && target != null && Application.isPlaying) CaptureCursor();
    }

    private void OnDisable()
    {
        ReleaseCursor();
        if (!Application.isPlaying) RestoreBody();
    }
    private void OnDestroy()
    {
        ReleaseCursor();
        RestoreBody();
    }
}
