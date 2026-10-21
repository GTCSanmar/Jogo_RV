using UnityEngine;
using UnityEngine.InputSystem;

namespace EscapeDoLaboratorio
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class LabPlayer : MonoBehaviour
    {
        public Camera view;
        public LabGame game;
        CharacterController controller;
        float pitch, verticalSpeed;
        void Awake() { controller = GetComponent<CharacterController>(); }
        void Start() { CaptureMouse(); }
        void CaptureMouse() { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        void OnDisable() { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        void OnApplicationFocus(bool focus)
        {
            if (!focus) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        }
        void Update()
        {
            game.Prompt = "";
            if (game.Won) return;
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            else if (mouse != null && mouse.leftButton.wasPressedThisFrame) CaptureMouse();
            if (Cursor.lockState != CursorLockMode.Locked) return;
            if (mouse != null)
            {
                Vector2 look = mouse.delta.ReadValue() * 0.10f;
                transform.Rotate(0, look.x, 0);
                pitch = Mathf.Clamp(pitch - look.y, -85, 85);
                view.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            }
            Vector2 input = Vector2.zero;
            if (keyboard != null)
            {
                input.x = (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0);
                input.y = (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0);
            }
            input = Vector2.ClampMagnitude(input, 1);
            float speed = keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed) ? 6 : 3.5f;
            if (controller.isGrounded && verticalSpeed < 0) verticalSpeed = -2;
            verticalSpeed += Physics.gravity.y * Time.deltaTime;
            controller.Move(((transform.right * input.x + transform.forward * input.y) * speed + Vector3.up * verticalSpeed) * Time.deltaTime);
            if (Physics.Raycast(view.transform.position, view.transform.forward, out RaycastHit hit, 2.7f, ~0, QueryTriggerInteraction.Ignore))
            {
                var target = hit.collider.GetComponentInParent<LabInteractable>();
                if (target != null)
                {
                    game.Prompt = target.GetPrompt(game);
                    if (keyboard != null && keyboard.eKey.wasPressedThisFrame) target.Interact(game);
                }
            }
        }
    }
}
