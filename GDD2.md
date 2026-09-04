# GDD 2 — Dungeon Defender VR: Expansão Roguelite

> Documento de **expansão/visão**, construído em cima do estado descrito em `GDD.md` (raio-x da branch `roguelite` no ponto de partida). Este documento não substitui o GDD 1 — descreve o que muda, o que é adicionado e o que ainda está em aberto para transformar o jogo num roguelite de waves. A seção 12 trata separadamente de uma visão futura (multiplayer) que ainda não deve ser implementada agora, apenas considerada no design dos sistemas atuais.

## 1. Visão geral da mudança

- **Pitch atualizado**: o jogador defende o ouro de uma dungeon contra hordas (waves) de esqueletos ladrões, usando pedras e itens comprados de um goblin mercador entre as waves, num loop roguelite (upgrades resetam a cada run). Run tem fim definido: sobreviver a um número alvo de waves (padrão **18**, ajustável) é a vitória — não é *endless*.
- **Sai**: a relíquia única (`GemObjective`) como objetivo e condição de derrota.
- **Entra**: pilhas de ouro espalhadas pela dungeon, cabeças de esqueleto como moeda, loja entre waves, inventário de itens arremessáveis, upgrades por run.
- Tudo o que não é mencionado explicitamente aqui (pooling, singletons, Feel, VFX do portal, sistema de zonas de dano dos inimigos, etc.) continua valendo como está descrito no GDD 1.

## 2. Loop de jogo (novo)

1. **Tela inicial** — igual ao GDD 1 (`StartMenuUI`), libera o começo da primeira wave.
2. **Wave** — horda de esqueletos nasce nos `spawnPoints`, anda até a pilha de ouro mais próxima, rouba um valor e volta para o spawn de origem carregando o valor roubado.
3. **Defesa** — jogador usa a pedra (hit básico, ilimitado) e itens comprados (arremessáveis, limitados) para derrotar os esqueletos antes que cheguem de volta ao spawn com o ouro roubado.
4. **Fim da wave** — quando todos os spawns programados da wave já nasceram e não sobra esqueleto vivo em campo.
5. **Shop (goblin)** — ⚠️ ainda não implementado (ver seção 9). Por enquanto, uma pausa fixa (`SpawnManager.timeBetweenWaves`, default 5s) faz esse papel provisoriamente.
6. **Próxima wave** — repete a partir do passo 2, dificuldade escalando, até a wave alvo (número de waves configuradas no `SpawnManager`, ex.: 18).
7. **Fim de run — derrota**: o ouro total da dungeon (soma de todas as pilhas + o que está em trânsito sendo roubado) chega a 0, em qualquer wave.
7b. **Fim de run — vitória**: o jogador sobrevive até completar a última wave configurada (todos os spawns dela nasceram + nenhum esqueleto vivo em campo).
8. **Ranking** — critério primário: vitória alcançada ou não. Depois disso, por número de waves sobrevividas (quem não venceu) ou por algum critério de desempate entre quem venceu (ouro restante? tempo total? cabeças coletadas? — a definir). Guarda também um snapshot da build final do jogador (itens e upgrades escolhidos na run). Reaproveita a estrutura de `Leaderboard`/`LeaderboardUI` e o `VirtualKeyboard` já existentes, mas o dado salvo precisa crescer (ver seção 9).

### 2.1 Implementação das waves (`SpawnManager`)

Cada wave é uma `WaveConfig` (classe serializável, aparece como lista expansível no Inspector do `SpawnManager` — sem precisar criar asset nenhum pra isso):

- **`enemies`**: lista de `(EnemyDefinition, count)` — quantos de cada tipo nessa wave. Os tipos continuam sendo os mesmos assets `EnemyDefinition` de sempre; a wave só decide a composição.
- **`spawnPoints`**: de quais Transforms essa wave especificamente pode nascer (arrasta os objetos direto, não precisa lembrar índice de array).
- **`spawnInterval`/`spawnIntervalVariation`**: ritmo de spawn dentro dessa wave (pode variar wave a wave — waves mais tarde podem spawnar mais rápido).

Ao rodar, a wave "achata" as entradas numa lista só (ex.: 6 tipo1 + 4 tipo2 = 10 itens) e embaralha, então os tipos saem misturados entre si em vez de em blocos. O antigo sorteio ponderado por `EnemyDefinition.spawnWeight` (`SpawnManager.enemyDefinitions`/`PickDefinition()`) foi removido — quem decide "quantos de cada tipo" agora é a wave, explicitamente, não mais um sorteio.

**Exemplo prático** (o que você descreveu): Wave 1 = `[{tipo1, 8}]`, spawn points = `[Spawn1]`. Wave 2 = `[{tipo1, 6}, {tipo2, 4}]`, spawn points = `[Spawn1]`. Wave 3 = `[{tipo1, 5}, {tipo2, 5}, {tipo3, 3}]`, spawn points = `[Spawn1, Spawn2]`.

## 3. Economia — Ouro e Moeda

- **Pilhas de ouro**: substituem o `GemObjective` único. Múltiplas pilhas espalhadas pela cena, cada uma com uma quantidade de ouro **randômica dentro de um range configurável pelo desenvolvedor** (min/max expostos, provavelmente como campos serializados, semelhante ao padrão de `EnemyDefinition`).
- **Roubo**: ao chegar numa pilha, o esqueleto rouba um valor X dela (a definir: fixo por tipo de esqueleto, ou também um range) e começa a voltar ao spawn de origem.
- **Derrota**: se a soma do ouro restante em todas as pilhas chegar a 0, a run acaba. Ouro "em trânsito" (sendo carregado por um esqueleto vivo rumo ao spawn) conta como perdido para esse total até ser recuperado — precisa decidir se ouro em trânsito já é descontado da pilha no momento do roubo (mais provável, dado que "sai da pilha") ou só quando o esqueleto sai de campo.
- **Cabeças de esqueleto**: moeda do jogador, dropada ao derrotar um esqueleto (independente de ele estar ou não carregando ouro roubado). É o que compra itens e upgrades na loja — não se confunde com o ouro da dungeon, que é o recurso que o jogador está *defendendo*, não gastando.

## 4. Ouro dropado — recuperação e comportamento dos esqueletos

Quando um esqueleto que está carregando ouro roubado é derrotado, ele dropa esse valor no chão. Regras:

- **Prioridade do jogador**: existe uma janela de delay após o drop em que só o jogador pode pegar o ouro do chão.
- **Depois do delay**, um esqueleto pode pegar o ouro dropado, mas **somente se**:
  - não estiver muito próximo do seu próprio spawn de origem (evita "recuperação instantânea" perto da saída), **e**
  - não estiver já carregando outro valor roubado.
- **Se ninguém pegar** o ouro dropado dentro de um tempo (a definir), ele se resolve assim: metade do valor volta automaticamente para a pilha de origem mais próxima, a outra metade some.

Implicação técnica: cada "drop de ouro no chão" precisa saber (a) o valor, (b) a pilha de origem para a qual metade retorna, e (c) um timer próprio. Isso é um objeto novo, candidato a pooling (`ObjectPoolManager`/`IPoolable`) já que pode ocorrer com frequência alta durante uma wave.

## 5. Esqueletos — mudanças de comportamento (`EnemyAI`)

Baseado no comportamento atual descrito no GDD 1 (NavMeshAgent, wander, avoidance, zonas de dano), mas com a máquina de estados de objetivo mudando:

- Estado antigo: `Spawn → anda até objetivo único → causa dano → morre/volta pro pool`.
- Estado novo: `Spawn → anda até a pilha de ouro mais próxima → rouba valor → anda de volta ao spawn de origem carregando o valor → sai de campo (ouro entregue, perdido pra dungeon) ou é derrotado no caminho (dropa ouro)`.
- Precisa guardar referência ao **spawn de origem** (para saber para onde voltar) e um **estado de carga** (carregando ouro ou não) — isso também deve alimentar visualmente o esqueleto de alguma forma (ainda não definido: um ícone, uma animação, o próprio saco de ouro visível?).
- Zonas de dano (Weak/Medium/Strong/Critical), wander e avoidance continuam valendo sem mudança.

## 6. Jogador — arma (mudança na seção 3 do GDD 1) — ✅ IMPLEMENTADO

- **Hit básico**: pedra, ilimitada, sem custo — o "tiro padrão" do jogador, sempre disponível.
- **Itens arremessáveis**: comprados na loja, com estoque limitado por run, selecionados um de cada vez através do inventário.
- O `SlingshotController`/`VerletRope`/medidor de força/física de pêndulo do GDD 1 seguem sendo o veículo de lançamento tanto da pedra quanto dos itens comprados — o que muda é só o que é instanciado na ponta do lançamento (`SlingshotController` não conhece mais `Projectile_Bomb` diretamente, só a interface `IThrowable` + o item equipado no `PlayerInventory`).

## 7. Itens — Sistema de dados (ScriptableObject) — ✅ IMPLEMENTADO

- Cada item (bomba, pedra, e outros arremessáveis a definir) é um `ItemDefinition` (SO), mesmo padrão do `EnemyDefinition`: `itemName`, `icon`/`description` (placeholder pra UI), `variants` (uma ou mais `ProjectileVariant` — prefab + chance %, mesmo sorteio ponderado que zonas/waves já usam), `unlimitedStock`, `cost`, `stockPerPurchase`.
- **`IThrowable`** (novo): interface mínima (`SetCollisionEnabled`, `IgnoreCollisionsWith`) que qualquer prefab de projétil precisa implementar — `Projectile_Bomb` já implementa. O `SlingshotController` só fala com essa interface, nunca com `Projectile_Bomb` diretamente.
- **`PlayerInventory`** (novo, singleton): guarda estoque por `ItemDefinition` e qual está equipado. `TryConsumeEquipped()` é chamado pelo `SlingshotController` a cada lançamento (retorna `false` se acabou o estoque — nesse caso não lança nada). Item com `unlimitedStock=true` (a pedra) nunca é consumido de verdade.
- **Chance de bomba gigante** migrou de `SlingshotController.giantBombChance` pra `Projectile_Bomb.giantChance` — a bomba sorteia sozinha se é gigante ao nascer, já que isso é comportamento específico dela, não do estilingue.
- **Ainda sem UI** (loja/inventário, pendências #8-11): pra testar itens diferentes agora, `PlayerInventory` tem um campo `Debug Starting Item` — bota o `ItemDefinition` que quiser testar ali, com estoque cheio automático ao iniciar. É só um gancho de teste, não é a solução final.

## 8. Inventário

- Jogador carrega um número limitado de itens comprados (não pedras — essas são ilimitadas e fora do inventário).
- Acesso via botão do controle, um item ativo por vez (não é possível carregar/usar todos simultaneamente).
- UI ainda não definida (radial, lista, etc.) — vale desenhar em conjunto com a loja, já que os dois manipulam os mesmos dados de estoque.

## 9. Loja (Goblin mercador) e Upgrades

- Abre entre waves, personagem aliado (goblin).
- Compra com cabeças de esqueleto.
- Duas categorias de compra:
  - **Itens novos** — adiciona um `ItemDefinition` ao inventário disponível/estoque do jogador.
  - **Upgrades** — modificam atributos de itens já possuídos ou do jogador (ex.: "+X dano nas bombas", "+10 estoque de bombas", "+X dano na pedra"). Aplicados só na run atual.
- **Upgrades resetam 100% a cada run** — sem meta-progressão entre partidas (roguelite clássico). Isso simplifica a persistência: só o leaderboard precisa gravar dado entre sessões, não o estado de upgrades.
- Estrutura de upgrade ainda em aberto: se é um SO próprio que referencia um `ItemDefinition` alvo e aplica um modificador (multiplicador/soma), ou se cada `ItemDefinition` já expõe uma lista de upgrades possíveis. Decidir no plano técnico.

## 10. Fim de run e Leaderboard (expande seção 9/10 do GDD 1)

- **Vitória**: sobreviver até completar a wave alvo (padrão 18, ajustável). **Derrota**: ouro total da dungeon chega a 0 antes disso.
- Ranking passa a registrar, além do nome (já resolvido pelo `VirtualKeyboard`):
  - se a run terminou em vitória ou derrota (critério primário de ordenação);
  - número de waves sobrevividas (critério de desempate pra quem não venceu, e teto natural pra quem venceu);
  - snapshot da build final (quais itens e upgrades o jogador tinha ao fim da run) — pensado para exibição estilo "run recap", comum em roguelites.
- `Leaderboard`/`LeaderboardUI` continuam com a mesma proposta (top 10, sempre visível no mundo, persistência local via `PlayerPrefs`/JSON), só o formato do dado salvo cresce.

## 11. O que **não** muda (herdado do GDD 1 sem alteração)

- Zonas de dano dos inimigos (Weak/Medium/Strong/Critical) e seu layout automático.
- Pooling geral (`ObjectPoolManager`/`IPoolable`) — inclusive candidato a cobrir os novos objetos (drops de ouro, cabeças de esqueleto).
- Singletons por convenção.
- Uso do Feel (fade, som, progress bar, billboard).
- Fluxo técnico de fim de jogo (fade + teleporte seguro) — só muda o gatilho (ouro chegando a 0 em vez de vida da relíquia chegando a 0).
- `EnemyVisualRandomizer` e as variantes visuais existentes (Mage, Minion, Rogue, Warrior) — ainda pendente de migração para `EnemyDefinition`, como já registrado no GDD 1.

## 12. Nota separada — Visão futura: Multiplayer

Não faz parte do escopo de implementação deste refactor, mas deve influenciar decisões de arquitetura desde já (evitar acoplar sistemas a "existe um único jogador").

- Usaria o template VR multiplayer da Unity.
- Jogadores compartilham uma **plataforma móvel** dentro da dungeon (não é mais uma arena estática defendida no lugar).
- Papéis por partida, trocáveis entre waves:
  - **Piloto** — controla o movimento (andar/girar) da plataforma.
  - **Crafter** — compra melhorias e "crafta" os itens/objetos que serão arremessados (equivalente multiplayer da loja + itens do modo single).
  - **Atirador** — de fato lança os itens/pedras contra os esqueletos.
- Implicação de arquitetura a manter em mente **já no refactor atual**: a lógica de compra/loja, a definição de itens (`ItemDefinition`) e o controle de inimigos não devem presumir um único jogador dono de tudo — separar "quem decide o que comprar" de "quem executa o arremesso" facilita a divisão de papéis quando o multiplayer for implementado.
- Mudança de escopo relevante: a movimentação da plataforma implica em level design pensado para deslocamento pela dungeon, não mais em uma arena fixa — isso é maior que só multiplayer, é uma mudança estrutural de nível que deve virar sua própria GDD (GDD 3?) quando for a vez de detalhar essa fase.

## 13. Pendências conhecidas desta expansão (decisões ainda não fechadas)

Numeradas pra referenciar em conversa/commits enquanto vamos fechando uma por uma. Risque (ou mova pra uma seção de decisões, se preferir) conforme forem resolvidas.

1. ~~Range de valor de ouro por pilha (min/max)~~ — **RESOLVIDO**: campo por instância (`GoldPile.minAmount`/`maxAmount`), sorteado no `Awake()`. Cada pilha no level design pode ter um range diferente. Default do componente: 30–80 (ponto de partida, ajustar em playtest).
2. ~~Valor roubado por esqueleto~~ — **RESOLVIDO**: por `EnemyDefinition` (`minSteal`/`maxSteal`, mesmo padrão de `minHealth`/`maxHealth`), sorteado a cada spawn. Default: 10–25. Fallback (sem definition) em `EnemyAI.minSteal`/`maxSteal`.
3. ~~Tempo de delay de prioridade do jogador sobre ouro dropado~~ — **RESOLVIDO**: `DroppedGold.playerPriorityDuration`, default **3s**. Só o jogador pode coletar (tag `"Player"`) nessa janela.
4. Distância mínima do spawn de origem para um esqueleto poder pegar ouro dropado — **PARCIALMENTE PENDENTE**: `DroppedGold` já existe (coleta do jogador + resolução automática funcionando), mas o `EnemyAI` ainda não sabe procurar/priorizar um drop próximo — essa parte da mecânica (esqueleto recuperando ouro do chão) ainda não foi implementada. Quando for, essa distância mínima entra como parâmetro de elegibilidade.
5. ~~Tempo até o ouro dropado "resolver"~~ — **RESOLVIDO**: `DroppedGold.extraTimeBeforeResolve`, default **5s** (contados DEPOIS da janela de prioridade do jogador — vida total do drop = 3s + 5s = 8s). Aos 8s: metade volta pra pilha de origem (`GoldPile.Deposit`), metade some.
6. ~~Se o ouro roubado é descontado da pilha no momento do roubo ou só quando o esqueleto sai de campo com ele~~ — **RESOLVIDO**: no momento do roubo (`GoldPile.Withdraw`). "Ouro total da dungeon" = soma de `GoldPile.CurrentAmount` de todas as pilhas (`DungeonGoldManager.TotalGold`); ouro em trânsito só volta a contar se for devolvido de verdade (`GoldPile.Deposit`).
7. ~~Indicação visual de "esqueleto carregando ouro"~~ — **RESOLVIDO (placeholder)**: `EnemyAI` tinge o modelo inteiro de dourado (`carryingTintColor`) enquanto está carregando. Funcional pra playtest, mas é claramente um placeholder — arte de verdade (saco de ouro visível, partícula etc.) fica pra depois.
8. Estrutura exata de dados dos upgrades (SO próprio vs. lista dentro do `ItemDefinition`) — não definido.
9. UI do inventário e da loja — não desenhada.
10. Lista completa de itens além da bomba (quais outros objetos arremessáveis existirão) — não definida.
11. Número de slots do inventário — não definido.
12. ~~Se o jogador pegar um drop de ouro dentro da janela de prioridade, devolve 100% ou metade?~~ — **RESOLVIDO**: 100% pra pilha de origem (`DroppedGold.Collect`).
13. ~~Pontos da vitória (hoje lêem `GemObjective`, que não existe mais)~~ — **RESOLVIDO**: viram o ouro restante na dungeon no momento da vitória (`DungeonGoldManager.TotalGold`, lido em `GameStateManager.EndGameSequence`).

---
*Este documento descreve a visão de expansão roguelite construída sobre o estado descrito em `GDD.md`. A seção 12 é uma nota de visão futura (multiplayer) e não deve ser tratada como escopo de implementação imediata.*