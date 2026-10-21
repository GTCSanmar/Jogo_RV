using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace EscapeDoLaboratorio
{
    public sealed class LabGame : MonoBehaviour
    {
        public bool HasCard { get; private set; }
        public bool Won { get; private set; }
        public string Prompt { get; set; } = "";
        string message = "Encontre o cartão de acesso e alcance a saída.";
        float messageUntil = 8;

        public void Notify(string value) { message = value; messageUntil = Time.time + 4; }
        public void CollectCard() { HasCard = true; Notify("Cartão de acesso coletado! Procure a saída na recepção."); }
        public void Win()
        {
            if (!HasCard || Won) return;
            Won = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        void Update()
        {
            if (Won && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
        void OnGUI()
        {
            float scale = Mathf.Min(Screen.width / 960f, Screen.height / 540f);
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
            float w = Screen.width / scale, h = Screen.height / scale;
            GUI.skin.label.fontSize = 18;
            GUI.skin.box.fontSize = 18;
            GUI.Box(new Rect(18, 18, 580, 76), "ESCAPE DO LABORATÓRIO\nWASD: mover | Mouse: olhar | Shift: correr | E: interagir");
            GUI.Box(new Rect(w - 255, 18, 237, 42), HasCard ? "Cartão: ADQUIRIDO" : "Cartão: não encontrado");
            if (Won)
            {
                GUI.Box(new Rect(w / 2 - 240, h / 2 - 90, 480, 180), "VOCÊ ESCAPOU!\n\nO laboratório ficou para trás.\n\nPressione R para jogar novamente.");
                return;
            }
            GUI.Label(new Rect(w / 2 - 6, h / 2 - 13, 24, 26), "+");
            if (!string.IsNullOrEmpty(Prompt)) GUI.Box(new Rect(w / 2 - 220, h / 2 + 35, 440, 38), Prompt);
            if (Time.time < messageUntil) GUI.Box(new Rect(w / 2 - 360, h - 65, 720, 42), message);
            if (Cursor.lockState != CursorLockMode.Locked)
                GUI.Box(new Rect(w / 2 - 240, h - 115, 480, 38), "Clique para capturar o mouse | Esc: liberar mouse");
        }
    }
}
