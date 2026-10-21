namespace EscapeDoLaboratorio
{
    public sealed class AccessCard : LabInteractable
    {
        public override string GetPrompt(LabGame game) => "[E] Coletar cartão de acesso";
        public override void Interact(LabGame game)
        {
            game.CollectCard();
            gameObject.SetActive(false);
        }
    }
}
