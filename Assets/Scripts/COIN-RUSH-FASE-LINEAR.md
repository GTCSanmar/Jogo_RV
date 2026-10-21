# Coin Rush 3D — percurso expandido

Fora do Play Mode, execute **Coin Rush > Gerar Jogo Completo**. Aguarde a mensagem
`[CoinRush] Geração concluída com sucesso!`. A cena Coin Rush existente é salva automaticamente.
Depois pressione Play e clique em Jogar. Não execute os geradores separados das etapas antigas.

## Dimensões e conteúdo

O percurso anterior ia de Z=0 a Z=77: 77 unidades. O novo vai de Z=0 a Z=485:
**485 unidades, aproximadamente 6,30 vezes o comprimento anterior**.
São dez trechos de 44 unidades e nove safezones de 5 unidades. O corredor tem 6 unidades
de largura interna, paredes de 6 unidades e teto físico invisível. O percurso segue em Z,
com obstáculos altos alternados que obrigam a desviar em zigue-zague dentro do corredor.

| Trecho | Desafio |
|---|---|
| 1 | Quatro barreiras baixas, de 0,85 unidade, para pular |
| 2 | Quatro barreiras altas alternadas, exigindo desvios laterais |
| 3 | Buraco com quatro plataformas permanentes e vãos de até 1,5 unidade |
| 4 | Quatro plataformas que somem 1,1 segundo após serem pisadas |
| 5 | Três barras giratórias letais, ciclo de 4 segundos |
| 6 | Três martelos pendulares letais, amplitude de 60 graus |
| 7 | Cinco obstáculos letais que se movem lateralmente |
| 8 | Três portas letais temporizadas: 1 segundo ativas, 3 segundos abertas |
| 9 | Pulo, plataformas que somem e obstáculo lateral móvel |
| 10 | Combinação de barreira baixa, desvio alto, barra giratória e porta temporizada |

Use WASD, mouse, Shift para correr, Espaço para pular e ESC para pausar/continuar.
Nas portas temporizadas, espere a barreira vermelha desaparecer antes de cruzar.
As armadilhas param no menu de pausa. Continuar preserva o olhar e a velocidade vertical do pulo.

## Checkpoints, relógios e moedas

Os nove checkpoints só são aceitos em ordem. A primeira entrada renova o timer para 20 segundos;
ele fica parado na safezone e começa com 20 segundos ao sair. Reentrar na mesma safezone não
renova o relógio. Portões impedem retorno ao trecho anterior. A chegada exige os nove checkpoints.

Ao morrer por contato letal, queda ou tempo esgotado, Tentar novamente restaura a posição segura,
as armadilhas e as plataformas. A pontuação e as moedas voltam ao estado registrado no checkpoint,
evitando pontos infinitos ao repetir um trecho. Reiniciar/Jogar novamente recomeça a fase inteira.

São 50 moedas MAR: cinco por trecho, 10 pontos por moeda. O HUD mostra pontuação, tempo do trecho,
trecho atual, checkpoint e tempo total. O tempo total não reinicia nos checkpoints e inclui safezones
e tentativas fracassadas; exclui pausa, menu principal e telas de morte/conclusão.

## Geração e assets

O mesmo gerador substitui a hierarquia que possui o componente CoinRushGeneratedCourse.
Objetos conhecidos da arena original são removidos; adições personalizadas são preservadas.
Player, Main Camera, Coins, Canvas, EventSystem e GameManager existentes são reutilizados.
O gerador descobre a cena em Assets/Scenes e procura os prefabs reais dentro de Assets.

Prefabs usados: MAR Cion_02, WoodenPlankBarrier_14_v1 e ConcreteBarrier_05_v1.
Materiais URP derivados são criados em Assets/CoinRushGenerated, sem modificar os pacotes originais.
Prefabs ausentes geram erro com nome exato no Console e recebem modelo primitivo provisório.
Outras cenas não são modificadas.

Para atualizar apenas áudio, sem regenerar o cenário, execute **Coin Rush > Configurar Áudio**.
O gerador completo também configura os três clipes reais de Assets/Audio automaticamente:
CoinRush_MusicaFundo.wav (loop, 25%), CoinRush_ColetaMoeda.wav (PlayOneShot, 65%) e
CoinRush_Vitoria.wav (uma vez, ao vencer). A música pausa com ESC, retoma do mesmo ponto
ao continuar, para na morte/vitória/menu e recomeça em uma nova tentativa.
O AudioSource antigo de música é reutilizado; um segundo AudioSource reproduz os efeitos.
O efeito de vitória toca na tela de conclusão mesmo com Time.timeScale igual a zero.
Nenhum AudioListener adicional é criado pela configuração de áudio.

Passos: o mesmo comando Configurar Áudio configura MrKrabs_Footsteps.wav no jogador.
O gerador cria uma cópia em Assets/CoinRushGenerated/Audio/MrKrabs_Footsteps_Loop.wav,
sem o silêncio inicial/final e com bordas suavizadas, sem modificar o WAV original.
O AudioSource Footsteps Audio usa volume 40%, loop e pitch de 1,35 durante a corrida.
Os passos exigem deslocamento horizontal real e contato com o chão; param ao soltar as teclas,
pular, cair, perder foco, pausar, morrer ou concluir. Não usam o AudioSource da música.

## Verificação

- Compilação dos scripts de Editor e execução validada contra as bibliotecas da Unity 6000.3.11f1.
- Verificações numéricas: comprimento mínimo de 5x, 10 trechos, 9 checkpoints ordenados, 50 moedas,
  alcance do pulo e estimativas de travessia com corrida abaixo de 20 segundos por trecho.
- O gerador verifica quantidade de sistemas, referências, colliders, triggers, chão e espaço dos
  spawns, posições de moedas e Rigidbody dos mecanismos antes de salvar.

Ainda é necessário testar no Play Mode:

1. Execute o gerador duas vezes e confirme uma única hierarquia de percurso e sistemas sem duplicação.
2. Complete os dez trechos usando corrida e pulo; confira especialmente os vãos e os martelos.
3. Observe rotação, oscilação, translação e alternância das portas. Confira colisões letais também
   quando uma porta fica ativa enquanto o jogador encosta nela.
4. Confira pausa, renovação dos 20 segundos em cada checkpoint e rejeição de revisitas.
5. Morra depois de coletar moedas; confirme restauração da pontuação e ausência de acúmulo duplicado.
6. Teste timeout, queda, restauração de plataformas, retorno ao menu, reinício e vitória.
7. Confira que a chegada não aceita o jogador antes do nono checkpoint.

As estimativas numéricas não substituem uma travessia real com a física da Unity.
