# Escape do Laboratório — primeira versão jogável

## Gerar e executar

1. Abra este projeto na Unity **6000.3.11f1** e aguarde a importação e compilação dos scripts.
2. Fora do Play Mode, selecione **Escape do Laboratório > Gerar laboratório jogável** na barra de menus.
3. Se houver alterações não salvas em cenas abertas, a Unity oferecerá salvá-las antes da geração.
4. O gerador abre e salva **Assets/Scenes/Game.unity**. Pressione **Play** e clique na janela Game.

Cada geração cria uma cópia da Game.unity anterior em `Assets/Scenes/Backups`, com nome único. Os materiais e prefabs próprios do gerador são atualizados em `Assets/GeneratedLaboratory`. Objetos acrescentados manualmente à cena ficam na cópia de segurança; a nova cena é recriada integralmente. SampleScene e os demais assets existentes são preservados.

O gerador inclui Game.unity como primeira cena habilitada em **File > Build Profiles > Scene List**, preservando as outras entradas. Não é necessário configurar referências no Inspector ou instalar novos pacotes: o projeto já possui Input System e URP. Mantenha **Active Input Handling = Input System Package (New)** e o pipeline URP dos níveis de qualidade do projeto.

## Controles

- **WASD**: andar; **Shift**: correr.
- **Mouse**: olhar; **E**: interagir com o objeto sob a mira a até 2,7 metros.
- **Esc**: liberar o cursor; clique na janela Game para capturá-lo novamente.
- **R**, após vencer: reiniciar a cena.

## Teste manual completo

1. Na recepção, vire-se e pressione E olhando para a porta azul de saída. Ela deve permanecer sólida e mostrar uma mensagem de acesso negado.
2. Percorra as quatro salas pelos quatro corredores. Confira a passagem pelas aberturas e as colisões com paredes, bancadas e equipamentos. Teste WASD, mouse e a diferença de velocidade com Shift.
3. Procure o cartão ciano sobre a bancada à direita na sala **04 / CONTENÇÃO**, ao lado do monitor e da amostra. Para um teste direto, sua posição é `(19.85, 1.11, 18.45)`.
4. Mire no cartão e pressione E. Ele deve desaparecer, a mensagem de coleta deve aparecer e o indicador deve mudar para **ADQUIRIDO**.
5. Volte à recepção, mire na porta de saída e pressione E. Ela deve subir e liberar a passagem. A vitória só aparece depois que você atravessa a porta e avança pelo vestíbulo.
6. Confira a tela de vitória, o cursor livre e R para reiniciar. O cartão e a porta devem voltar ao estado inicial.
7. Pare o Play Mode e gere novamente: confira a cópia de segurança e que não há jogadores ou câmeras duplicados na nova cena.

## Arquivos

- `LabPlayer.cs`: CharacterController, gravidade, mouse e teclado pelo novo Input System; raycast que respeita paredes e obstáculos.
- `LabGame.cs`: inventário, mensagens, mira, instruções, vitória e reinício. Usa IMGUI, sem dependências de fontes TMP, Canvas ou EventSystem.
- `LabInteractable.cs`, `AccessCard.cs`, `ExitDoor.cs`: contrato de interação, coleta e abertura da porta.
- `EscapeZone.cs`: detecção da travessia após a autorização.
- `Editor/LaboratoryGenerator.cs`: criação de geometria, materiais URP, iluminação, prefabs e cena. A pasta Editor exclui este script do jogo compilado.

Prefabs gerados: **Jogador**, **Bancada**, **CartaoDeAcesso** e **PortaDeSaida**. A referência de estado do jogador e a referência da porta na zona de fuga são atribuídas na cena pelo gerador.

## Validação realizada fora do Editor

Os sete arquivos C# foram compilados juntos com o compilador e as bibliotecas locais da Unity 6000.3.11f1 e do Input System instalado, sem erros ou avisos. A geração de assets e os testes de Play Mode ainda devem ser executados na Unity pelo roteiro acima; essa compilação isolada não comprova iluminação, física ou renderização em execução.
