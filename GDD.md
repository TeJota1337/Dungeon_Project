# GDD — Dungeon Defender VR (nome provisório)

> Documento de referência do estado ATUAL do jogo (branch `roguelite`, ponto de partida). Serve de base pra qualquer expansão/mudança de direção — não é um documento de visão, é um raio-x do que já existe e funciona.

## 1. Visão geral

- **Gênero**: tower-defense / wave-defense em primeira pessoa, VR.
- **Plataforma**: Android XR / Meta Quest, via OpenXR.
- **Engine**: Unity 6000.5.7f1, URP 17.5.0, XR Interaction Toolkit 3.5.1, novo Input System, pacote XR Hands.
- **Pitch de uma linha**: o jogador defende uma relíquia mágica de ondas de inimigos usando um estilingue de mão, atirando bombas.
- **Sessão**: uma partida dura `gameDuration` segundos de spawns (padrão 120s) + o tempo pra limpar os inimigos restantes em campo.

## 2. Loop de jogo

1. **Tela inicial** (`StartMenuUI`) — painel fixo com botão "Iniciar". Só libera o `SpawnManager` quando o jogador clica; nada começa sozinho.
2. **Onda de spawns** (`SpawnManager.BeginGame()`) — inimigos nascem em pontos aleatórios da cena num ritmo com variação, por `gameDuration` segundos.
3. **Defesa** — jogador usa o estilingue pra estourar bombas nos inimigos antes que cheguem na relíquia.
4. **Fim da sessão**:
   - **Derrota**: a relíquia (`GemObjective`) chega a 0 de vida.
   - **Vitória**: os spawns acabaram E não sobrou nenhum inimigo vivo em campo (`GameStateManager.CheckVictoryCondition`).
5. **Fade + teleporte** do jogador de volta ao ponto de spawn, painel de fim de jogo (`GameOverUI`).
6. **Vitória**: campo pra digitar nome (teclado virtual, feito pra VR) e entrar no ranking dos 10 melhores (salvo local via `PlayerPrefs`/JSON, `Leaderboard`). O placar (`LeaderboardUI`) fica sempre visível no mundo, o jogo inteiro, não só na tela de fim de jogo.
7. Botões de **Restart** (pula a tela inicial, `GameStateManager.SkipStartMenu`) e **Voltar ao Menu** (mostra a tela inicial de novo).

## 3. Mecânica do jogador — Estilingue (`SlingshotController`)

Interação de duas mãos, controlada por Input Actions (hoje: trigger e grip da mão direita testados em paralelo pra decidir qual fica).

- **Estilingue sempre visível**, preso/seguindo a mão esquerda — não precisa de botão pra "sacar" ele.
- **Puxar a bomba**: com a mão direita dentro da zona de pegada (`SlingshotZoneDetector`, trigger collider), segurar trigger/grip spawna uma bomba e começa a desenhar a trajetória simulada (`LineRenderer`, com gravidade + `Physics.Linecast` pra prever colisão).
- **Elástico com física real** (`VerletRope`): duas "cordinhas" simuladas por Verlet integration (sem Rigidbody/PhysX) ligando os joints da base aos joints do "treco" (a parte que a mão puxa) — ficam com folga/balanço natural em repouso e esticam de verdade ao puxar.
- **Medidor de força visual**: o elástico muda de cor (verde → amarelo → vermelho) conforme a força do puxão, usando `MaterialPropertyBlock` (não depende do material aceitar cor de vértice).
- **Treco com física de pêndulo** (`SlingshotPouch`): quando solto, cai por gravidade até as duas amarras esticarem — não "teleporta" pro lugar de repouso.
- **Dobra do modelo (blend shape)**: a base e o treco têm um blend shape `BEND` que ativa proporcional à força do puxão; os joints acompanham a deformação via leitura de vértice (`BlendShapeAnchor`, usa `SkinnedMeshRenderer.BakeMesh`).
- **Soltar**: calcula velocidade de lançamento a partir do vetor de puxada entre as duas mãos (`pullForceCurve`), aplica spin aleatório proporcional à força.
- **Arrebentar**: puxar além de `breakThreshold` arrisca (`maxBreakChance`) a bomba cair sem força em vez de lançar.
- **Bomba gigante**: chance pequena (`giantBombChance`) de qualquer lançamento virar bomba gigante (maior, dano em área em vez de acerto direto).
- **Feedback**: háptico (`PlayerHaptics` — pulso contínuo puxando, pulso ao arrebentar/lançar/acertar) e sonoro (`GameAudio`, via `MMSoundManager` do Feel).

## 4. Bombas (`Projectile_Bomb`)

- **Acerto direto**: identifica a zona de dano atingida no inimigo e aplica o multiplicador dela.
- **Quase-acerto (splash)**: se não acertar um inimigo direto, tudo dentro de `splashRadius` do ponto de impacto toma dano reduzido (`splashDamageMultiplier`).
- **Bomba gigante**: `Physics.OverlapSphere` num raio maior (`giantExplosionRadius`), dano em todos os inimigos pegos, com multiplicador de dano próprio (`giantDamageMultiplier`).
- Dano final = dano base aleatório (`minDamage`–`maxDamage`) × multiplicador da zona × multiplicador extra (gigante/splash).
- VFX de explosão + luz (`ExplosionEffect`, `ExplosionLightFlash`) e som próprios pra gigante vs. normal.
- Some do pool sozinha após `destroyAfterSeconds` mesmo sem colidir com nada.

## 5. Sistema de zonas de dano (`EnemyAI`)

- Cada inimigo tem 4 zonas fixas: **Weak, Medium, Strong, Critical**, cada uma com seu próprio `BoxCollider` e `damageMultiplier`.
- Layout automático: editar a % de altura de uma zona no Inspector redistribui proporcionalmente as outras pra manter 100%; o **último** elemento do array sempre nasce no topo (por isso `Critical` é o último — vira automaticamente a "cabeça").
- Acerto em `Critical` é sempre marcado como crítico (view de dano diferenciada).
- As zonas reescalam junto com o `visualScale`/`zoneScaleMultiplier` do tipo de inimigo (ver seção 6), pra o hitbox não ficar desalinhado num modelo maior/menor.

## 6. Inimigos

### `EnemyAI` (comportamento)
- `NavMeshAgent` caminha até o `objective` (a relíquia).
- **Wander**: a cada intervalo aleatório, mira num ponto levemente deslocado do objetivo em vez do centro exato (caminhada menos robótica); desliga perto do alvo pra garantir chegada limpa.
- **Avoidance**: prioridade de desvio sorteada por inimigo, pra evitar "empate" entre dois agentes tentando se desviar um do outro.
- Ao chegar no objetivo: causa dano na relíquia igual à vida ATUAL do inimigo (quanto mais dano o jogador já causou, menos ele "rouba" ao chegar) e volta pro pool.
- Ao morrer: registra estatística (`GameStateManager.RegisterEnemyDefeated`), som, volta pro pool.

### `EnemyDefinition` (ScriptableObject — sistema de tipos)
Cada "tipo" de inimigo é um asset de dados, sorteado pelo `SpawnManager` a cada spawn (com peso, `spawnWeight`) — o prefab físico (rig de zonas, NavMeshAgent) continua único e compartilhado. Campos: nome, variantes visuais + escala, vida mín/máx, velocidade mín/máx, multiplicador extra de escala das zonas. Tem preview ao vivo no Editor (campo `Preview Definition` no `EnemyAI`, recalcula zonas sem precisar dar Play).

**Status atual**: o sistema está implementado e funcionando, mas **nenhum asset `EnemyDefinition` foi criado ainda** — o jogo hoje roda nos valores padrão do próprio prefab (fallback). Criar os tipos (ex.: um "padrão" migrando os 4 visuais existentes, depois variações tipo tanque/rápido) é passo pendente.

### `EnemyVisualRandomizer` (visual)
Sorteia um modelo entre variantes (`VisualVariant`: prefab + Avatar) por spawn, com Animator próprio + clipe de caminhada aleatório entre um pool compartilhado. Variantes hoje disponíveis (herdadas do prefab, ainda não migradas pra um `EnemyDefinition`): **Mage, Minion, Rogue, Warrior**.

## 7. Spawn (`SpawnManager`)

- `spawnPoints`: array de Transforms — já suporta quantos pontos o nível quiser, é só adicionar na lista.
- `SpawnRoutine`: enquanto o tempo decorrido < `gameDuration`, spawna um inimigo e espera um intervalo (`spawnInterval` ± `spawnIntervalVariation`).
- Só começa quando `BeginGame()` é chamado (pelo `StartMenuUI`) — não é mais automático no `Start()`.
- `HasFinishedSpawning` + `EnemyAI.ActiveCount` juntos decidem a condição de vitória.

## 8. Objetivo — a Relíquia (`GemObjective`)

- Vida própria (`maxHealth`, padrão 150 — dimensionado pra ~25% do dano teórico máximo que os spawns causariam se nenhum inimigo fosse impedido).
- Barra de vida (`GemHealthBar`, usa `MMProgressBar` do Feel) colorida por % de vida (verde/amarelo/vermelho), sempre de frente pra câmera.
- Flash vermelho + número de dano flutuante ao ser atingida.
- Flutua/gira sozinha (`GemBob` + `MMAutoRotate` do Feel) — só estética.
- Vida chegando a 0 dispara derrota.

## 9. Fluxo de fim de jogo (`GameStateManager`)

- Congela o jogo (`Time.timeScale = 0`), funde a tela pra preto (`MMFadeEvent`, Feel), teleporta o jogador de volta ao spawn (desliga/religa o `CharacterController` pra evitar colisão estranha no teletransporte), funde de volta e só então mostra o painel — nunca aparece torto, não importa pra onde o jogador estava olhando.
- Acompanha estatísticas da run: inimigos derrotados, dano total causado — mostradas na vitória e salvas no ranking.

## 10. UI / Meta

- **`StartMenuUI`**: painel de início, só um botão "Iniciar" por enquanto. `GameStateManager.SkipStartMenu` deixa pular direto pro jogo (usado pelo Restart).
- **`GameOverUI`**: painel fixo no mundo (não billboard — evita ficar torto). Derrota mostra só mensagem + Restart. Vitória libera campo de nome pro ranking.
- **`VirtualKeyboard`**: teclado clicável pra digitar em VR (o teclado nativo do Android não aparece dentro de app OpenXR imersivo). Só confirma o texto no botão "Confirmar" explícito, não a cada tecla.
- **`Leaderboard` / `LeaderboardUI`**: top 10 por pontos, persistido local (`PlayerPrefs`, JSON). Placar fica sempre visível no mundo.
- **`GameTimerDisplay`**: tempo restante de spawns.
- **`FpsCounterDisplay`**: FPS atual, colorido pela proximidade da meta (padrão 72fps).

Textos já em inglês: mensagens de vitória/derrota (`"Victory! You protected the relic."` / `"Defeat! The relic was destroyed."`). **O resto do jogo (menus, labels) ainda está por traduzir** — pendente, pedido explicitamente pelo usuário visando alcance internacional.

## 11. Feedback systems

- **`DamageNumberManager`/`DamageNumber`**: números de dano flutuantes, empilham (`stackWindow`) em vez de spawnar um popup por hit; tamanho escala com o dano e com distância da câmera; crítico tem prefixo e escala própria.
- **`EnemyHealthDisplay`**: vida em texto acima do inimigo, cor por % restante, billboard pra câmera.
- **Háptico** (`PlayerHaptics`): pulso contínuo puxando o estilingue, pulsos únicos pra lançar/arrebentar/confirmar acerto.
- **Áudio** (`GameAudio`, via `MMSoundManager`): sons de estilingue, bomba (normal/gigante), inimigo (hit/morte/chegada), dano na relíquia, stings de vitória/derrota (não-espaciais).
- **Atmosfera**: `TorchFlicker` (tochas com intensidade/range/cor variando por Perlin Noise, cada uma com offset próprio).

## 12. VFX — Portal (Shader Graph)

Shader `SH_Portal` (URP Unlit, Transparent, Additive) construído do zero: vórtice girando (UV rotacionado + distorção por profundidade), noise animado colorindo o redemoinho, brilho de borda (rim), transparência com borda "esfarrapada" (orgânica, não um círculo perfeito) misturando noise na máscara de raio. Já existem 3 variantes de material (`Shader Graphs_SH_Portal`, `1`, `2` — cores diferentes). **Pendente do plano original**: partículas de rastro, névoa na base, luz pulsante — só o núcleo (vórtice) foi finalizado até agora.

## 13. Infraestrutura técnica

- **Pooling** (`ObjectPoolManager` / `PooledObject` / `IPoolable`): reaproveita instâncias (inimigos, bombas, números de dano, VFX de explosão) em vez de Instantiate/Destroy, pra evitar picos de GC em VR. Qualquer componente que precise resetar estado a cada reuso implementa `IPoolable` (`OnSpawnFromPool`/`OnReturnToPool`), já que `Awake`/`Start` só rodam uma vez na vida do objeto.
- **Singletons por convenção**: `SpawnManager.Instance`, `GameStateManager.Instance`, `GemObjective.Instance`, `GameAudio.Instance`, `PlayerHaptics.Instance`, `DamageNumberManager.Instance`, `ObjectPoolManager.Instance` — todos setados em `Awake()`.
- **Feel (More Mountains)**: usado pra fade de tela (`MMFadeEvent`), som (`MMSoundManager`), barra de progresso (`MMProgressBar`), flutuação/rotação (`MMAutoRotate`), billboard próprio reimplementado onde o do Feel não servia bem em VR.

## 14. Cenas

- **`Gameplay.unity`** — cena principal jogável.
- **`LD.unity`** / **`BasicScene.unity`** — cenas de trabalho de level design/layout.
- **`SampleScene.unity`** / **`VFX.unity`** — sobras de template, sem uso ativo.

## 15. Pendências conhecidas (não é bug, é trabalho ainda não feito)

- Nenhum asset `EnemyDefinition` criado ainda — sistema pronto, mas sem dados (roda no fallback do prefab).
- Tradução completa do jogo pra inglês — só as mensagens de vitória/derrota estão em inglês hoje.
- Shader toon baixado da Unity — ainda não integrado em nenhum asset da cena.
- VFX do portal: falta partícula de rastro, névoa e luz (só o vórtice central está pronto).
- `CLAUDE.md` (guia técnico pra IA) está desatualizado em relação a tudo isso — vale uma atualização separada quando o pó baixar.

---
*Esse documento descreve o estado da branch `roguelite` logo após ela ser criada a partir da `main` — é a base sobre a qual as novas ideias (GDD de expansão, à parte) vão ser construídas.*
