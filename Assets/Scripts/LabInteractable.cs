using UnityEngine;

namespace EscapeDoLaboratorio
{
    public abstract class LabInteractable : MonoBehaviour
    {
        public abstract string GetPrompt(LabGame game);
        public abstract void Interact(LabGame game);
    }
}
