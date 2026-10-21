using UnityEngine;

namespace EscapeDoLaboratorio
{
    public sealed class EscapeZone : MonoBehaviour
    {
        public ExitDoor door;
        void OnTriggerEnter(Collider other)
        {
            var player = other.GetComponent<LabPlayer>();
            if (player != null && door != null && door.IsOpen) player.game.Win();
        }
    }
}
