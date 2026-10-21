using UnityEngine;

namespace EscapeDoLaboratorio
{
    public sealed class ExitDoor : LabInteractable
    {
        public bool IsOpen { get; private set; }
        Vector3 closedPosition;
        void Awake() { closedPosition = transform.localPosition; }
        public override string GetPrompt(LabGame game) => IsOpen ? "Saída liberada: atravesse a porta" : game.HasCard ? "[E] Destrancar saída" : "[E] Saída bloqueada: requer cartão";
        public override void Interact(LabGame game)
        {
            if (!game.HasCard) { game.Notify("Acesso negado. Encontre o cartão em uma das salas."); return; }
            if (IsOpen) return;
            IsOpen = true;
            foreach (var collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
            game.Notify("Acesso autorizado. Atravesse a saída para escapar!");
        }
        void Update()
        {
            if (IsOpen) transform.localPosition = Vector3.MoveTowards(transform.localPosition, closedPosition + Vector3.up * 3.1f, 2 * Time.deltaTime);
        }
    }
}
