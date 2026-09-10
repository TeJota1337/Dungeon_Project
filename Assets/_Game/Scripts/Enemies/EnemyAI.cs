using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public enum ZoneType { Weak, Medium, Strong, Critical }

[System.Serializable]
public class ZoneConfig
{
    public ZoneType type;
    public Collider zoneCollider;

    [Range(0f, 1f)]
    public float heightPercent = 0.25f;

    public float damageMultiplier = 1f;
    public Color gizmoColor = Color.white;

    [HideInInspector] public float previousPercent;
}

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI : MonoBehaviour, IPoolable
{
    // Sequência de objetivo do esqueleto ladrão (GDD 2, seção 5): procura a GoldPile mais
    // próxima, rouba, volta pro spawn de onde nasceu. Morrer no meio do caminho de volta
    // dropa o ouro carregado (ver TakeDamage).
    enum State { SeekingPile, Returning }

    [Header("Roubo (fallback se Init() não vier de um EnemyDefinition)")]
    public int minSteal = 10;
    public int maxSteal = 25;

    [Header("Recompensa (fallback se Init() não vier de um EnemyDefinition)")]
    public int minSkullReward = 1;
    public int maxSkullReward = 2;

    [Header("Ouro dropado ao morrer carregando um roubo")]
    public GameObject droppedGoldPrefab;

    [Header("Visual ao carregar ouro (placeholder — GDD 2, pendência #7)")]
    public Color carryingTintColor = new Color(1f, 0.85f, 0.2f);

    [Header("Vida")]
    public int minHealth = 5;
    public int maxHealth = 15;

    private int health;      // vida atual, sorteada no Awake
    private int rolledMax;   // valor m�ximo sorteado pra ESTE inimigo (usado no display de %)
    private EnemyHealthDisplay healthDisplay;

    [Header("Feedback de dano")]
    public Color hitFlashColor = Color.red;
    public float hitFlashDuration = 0.15f;

    [Header("Zonas de Dano")]
    [Tooltip("Onde o bloco de zonas COMEÇA (base), em Y local. NÃO escala com o inimigo — fica ancorado perto dos pés, e o bloco cresce pra CIMA a partir daqui.")]
    public float bottomOffset = 0f;
    public float totalHeight = 2f;    // altura BASE do bloco (sem escala), escalada por appliedZoneScale a partir do bottomOffset
    public float zoneWidth = 0.6f;    // largura/profundidade BASE de cada zona (eixos X e Z)
    public ZoneConfig[] zones = new ZoneConfig[4];

    [Header("Preview no Editor (não afeta o sorteio real do SpawnManager)")]
    [Tooltip("Atribui um EnemyDefinition aqui só pra pré-visualizar como as zonas ficam com o Visual Scale/Zone Scale Multiplier dele, direto na Scene View, sem precisar dar Play.")]
    public EnemyDefinition previewDefinition;

    [Header("Varia��o de Movimento")]
    [Tooltip("Multiplicadores sorteados sobre a velocidade base do NavMeshAgent do prefab, pra nem todo inimigo andar igual.")]
    public float minSpeedMultiplier = 0.85f;
    public float maxSpeedMultiplier = 1.2f;
    [Tooltip("A cada intervalo (sorteado entre esses dois valores), o inimigo mira num ponto levemente deslocado do objetivo em vez do centro exato, criando uma caminhada menos linear.")]
    public float minWanderInterval = 1.5f;
    public float maxWanderInterval = 3.5f;
    public float wanderRadius = 0.75f;
    [Tooltip("Dist�ncia do objetivo a partir da qual o desvio de wander � desligado, pra garantir uma chegada limpa (sem ficar orbitando o objetivo).")]
    public float wanderMinDistanceFromObjective = 2f;

    [Header("Evitar Sobreposi��o entre Inimigos (NavMeshAgent Avoidance)")]
    [Tooltip("Sorteado por inimigo pra evitar que dois agentes de mesma prioridade 'empatem' tentando se desviar um do outro. Requer que o NavMeshAgent do prefab tenha um Obstacle Avoidance Type diferente de 'No Avoidance'.")]
    public int minAvoidancePriority = 10;
    public int maxAvoidancePriority = 90;

    private NavMeshAgent agent;
    private Renderer[] renderers;
    private Color[] originalColors;
    private Coroutine flashRoutine;
    private PooledObject pooledObject;
    private float baseAgentSpeed;
    private float wanderTimer;
    private float nextWanderInterval;

    private EnemyDefinition currentDefinition;
    private float appliedZoneScale = 1f; // Visual Scale * Zone Scale Multiplier do EnemyDefinition ativo (spawn real) ou de preview (Editor)
    private EnemyDefinition subscribedPreviewDefinition;

    private State state;
    private Transform homeSpawn;   // pra onde voltar depois de roubar (GDD 2, pendência #4 ainda depende disso)
    private GoldPile targetPile;
    private int carriedGold;

    // ---------- SETUP PADR�O (roda ao adicionar o componente) ----------

    void Reset()
    {
        zones = new ZoneConfig[]
        {
            new ZoneConfig { type = ZoneType.Weak,     heightPercent = 0.30f, damageMultiplier = 0.5f, gizmoColor = new Color(0.3f, 0.8f, 1f) },
            new ZoneConfig { type = ZoneType.Medium,   heightPercent = 0.30f, damageMultiplier = 1f,   gizmoColor = new Color(1f, 1f, 0.2f) },
            new ZoneConfig { type = ZoneType.Strong,   heightPercent = 0.25f, damageMultiplier = 1.5f, gizmoColor = new Color(1f, 0.6f, 0.1f) },
            new ZoneConfig { type = ZoneType.Critical, heightPercent = 0.15f, damageMultiplier = 3f,   gizmoColor = new Color(1f, 0.2f, 0.2f) },
        };
    }

    // ---------- CICLO DE VIDA ----------

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        healthDisplay = GetComponentInChildren<EnemyHealthDisplay>();
        baseAgentSpeed = agent.speed;
    }

    void OnEnable()
    {
        SyncPreviewSubscription();
        ApplyPreviewScaleIfEditing();
        RecalculateZoneLayout();
    }

    void OnDisable()
    {
        if (subscribedPreviewDefinition != null)
            subscribedPreviewDefinition.Changed -= HandlePreviewDefinitionChanged;

        subscribedPreviewDefinition = null;
    }

    // ---------- POOLING: reinicializa tudo que era feito em Awake/Start,
    // já que esses só rodam uma vez na vida do GameObject e não disparam de novo a cada reuso ----------

    // Quantos inimigos est�o ativos agora (spawnados e ainda n�o devolvidos ao pool).
    // Usado pra decidir vit�ria: s� conta depois que o SpawnManager parar de spawnar.
    public static int ActiveCount { get; set; }

    public void OnSpawnFromPool()
    {
        ActiveCount++;

        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            flashRoutine = null;
        }

        // vida/velocidade/visual dependem do EnemyDefinition, que só chega em Init() -
        // essa parte roda aqui pq n�o depende de qual tipo de inimigo este spawn � (v. IPoolable)
        agent.avoidancePriority = Random.Range(minAvoidancePriority, maxAvoidancePriority + 1);

        wanderTimer = 0f;
        nextWanderInterval = Random.Range(minWanderInterval, maxWanderInterval);

        carriedGold = 0;
    }

    // Aplica os dados do tipo sorteado pelo SpawnManager (ou os valores padr�o do
    // pr�prio prefab, se null) - visual, vida, velocidade e escala das zonas de dano.
    void ApplyDefinition()
    {
        EnemyVisualRandomizer visualRandomizer = GetComponent<EnemyVisualRandomizer>();
        renderers = visualRandomizer != null
            ? visualRandomizer.Initialize(currentDefinition)
            : GetComponentsInChildren<Renderer>();

        originalColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            originalColors[i] = renderers[i].material.color;
        }

        int rollMinHealth = currentDefinition != null ? currentDefinition.minHealth : minHealth;
        int rollMaxHealth = currentDefinition != null ? currentDefinition.maxHealth : maxHealth;
        health = Random.Range(rollMinHealth, rollMaxHealth + 1);
        rolledMax = health;

        if (healthDisplay != null)
            healthDisplay.Initialize(health);

        agent.speed = currentDefinition != null
            ? Random.Range(currentDefinition.minSpeed, currentDefinition.maxSpeed)
            : baseAgentSpeed * Random.Range(minSpeedMultiplier, maxSpeedMultiplier);

        // reescala as zonas de dano junto com o visual, pra o hitbox n�o ficar
        // desalinhado quando o EnemyDefinition muda o tamanho do modelo.
        // zoneScaleMultiplier existe pq Visual Scale sozinho nem sempre � proporcional
        // ao tamanho real renderizado (modelos com propor��es diferentes) - d� pra compensar por tipo.
        appliedZoneScale = currentDefinition != null
            ? currentDefinition.visualScale * currentDefinition.zoneScaleMultiplier
            : 1f;
        RecalculateZoneLayout();

        // garante que um objeto reciclado do pool n�o nasce "tingido de ouro" de uma vida anterior
        UpdateCarryingVisual();
    }

    public void OnReturnToPool()
    {
        ActiveCount--;

        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            flashRoutine = null;
        }

        // vit�ria agora � decidida diretamente pelo SpawnManager (RunWaves), que espera
        // ActiveCount == 0 ao fim da �ltima wave - n�o precisa mais consultar daqui.
    }

    // Chamado explicitamente por quem spawna o inimigo (SpawnManager), depois de tirá-lo do pool.
    // spawnPoint é de onde ele nasceu (e pra onde volta depois de roubar); definition pode ser
    // null (usa os valores padrão do próprio prefab: minHealth/maxHealth/minSteal/maxSteal etc.)
    public void Init(Transform spawnPoint, EnemyDefinition definition = null)
    {
        homeSpawn = spawnPoint;
        currentDefinition = definition;

        ApplyDefinition();

        state = State.SeekingPile;
        PickNewTargetPile();
    }

    void PickNewTargetPile()
    {
        targetPile = DungeonGoldManager.Instance != null
            ? DungeonGoldManager.Instance.FindNearestPile(transform.position)
            : null;

        if (targetPile != null)
            agent.SetDestination(targetPile.transform.position);
    }

    void Update()
    {
        // nenhuma pilha disponível agora (raro - normalmente significa que o total já zerou e a
        // derrota já disparou via DungeonGoldManager); tenta de novo até algo aparecer ou o jogo
        // congelar por causa do fim de jogo.
        if (state == State.SeekingPile && targetPile == null)
        {
            PickNewTargetPile();
            return;
        }

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            if (state == State.SeekingPile) OnReachPile();
            else OnReachSpawn();
            return;
        }

        Vector3 currentTarget = state == State.SeekingPile ? targetPile.transform.position : homeSpawn.position;
        UpdateWander(currentTarget);
    }

    void UpdateWander(Vector3 target)
    {
        wanderTimer += Time.deltaTime;
        if (wanderTimer < nextWanderInterval) return;

        wanderTimer = 0f;
        nextWanderInterval = Random.Range(minWanderInterval, maxWanderInterval);

        float distanceToTarget = Vector3.Distance(transform.position, target);

        if (distanceToTarget > wanderMinDistanceFromObjective)
        {
            Vector2 randomOffset = Random.insideUnitCircle * wanderRadius;
            Vector3 wanderTarget = target + new Vector3(randomOffset.x, 0f, randomOffset.y);
            agent.SetDestination(wanderTarget);
        }
        else
        {
            // perto o suficiente do alvo: mira exato, sem desvio, pra garantir chegada limpa
            agent.SetDestination(target);
        }
    }

    // Chegou na pilha: rouba (GoldPile.Withdraw já garante que nunca fica negativa) e parte
    // pro caminho de volta. Se outro esqueleto esvaziou a pilha bem na hora, procura outra.
    void OnReachPile()
    {
        int rollMin = currentDefinition != null ? currentDefinition.minSteal : minSteal;
        int rollMax = currentDefinition != null ? currentDefinition.maxSteal : maxSteal;
        int wanted = Random.Range(rollMin, rollMax + 1);

        carriedGold = targetPile.Withdraw(wanted);

        if (carriedGold > 0)
        {
            UpdateCarryingVisual();
            state = State.Returning;
            agent.SetDestination(homeSpawn.position);
        }
        else
        {
            PickNewTargetPile();
        }
    }

    // Chegou de volta no spawn de origem carregando ouro: entrega (o ouro já saiu da pilha no
    // momento do roubo, então "entregar" só precisa fazer o esqueleto sumir de campo).
    void OnReachSpawn()
    {
        GameAudio.Instance?.PlayEnemyReachedObjective(transform.position);
        ReturnToPool();
    }

    void ReturnToPool()
    {
        if (pooledObject == null)
            pooledObject = GetComponent<PooledObject>();

        if (pooledObject != null)
            pooledObject.ReturnToPool();
        else
            Destroy(gameObject);
    }

    // ---------- DANO ----------

    public void TakeDamage(int amount, bool isCrit = false, Color? hitColor = null)
    {
        if (health <= 0) return; // já morreu neste ciclo - evita processar 2 mortes (2 projéteis acertando no mesmo frame)

        health -= amount;
        GameStateManager.Instance?.RegisterDamage(amount);
#if UNITY_EDITOR
        Debug.Log($"{name} tomou {amount} de dano{(isCrit ? " (CR�TICO)" : "")}. Vida restante: {health}/{rolledMax}");
#endif

        if (healthDisplay != null)
            healthDisplay.UpdateDisplay(health);

        Color colorToUse = hitColor ?? Color.white;
        DamageNumberManager.Instance.Spawn(gameObject, transform.position, amount, isCrit, colorToUse);

        FlashRed();

        if (health <= 0)
        {
            GameStateManager.Instance?.RegisterEnemyDefeated();
            GameAudio.Instance?.PlayEnemyDeath(transform.position);

            AwardSkullReward();

            if (carriedGold > 0)
                SpawnDroppedGold();

            ReturnToPool();
        }
        else
        {
            GameAudio.Instance?.PlayEnemyHit(transform.position);
        }
    }

    // Cabeças de esqueleto (moeda do jogador pra loja - GDD 2, seção 3) - dropa ao morrer,
    // independente de estar ou não carregando ouro roubado no momento.
    void AwardSkullReward()
    {
        int rollMin = currentDefinition != null ? currentDefinition.minSkullReward : minSkullReward;
        int rollMax = currentDefinition != null ? currentDefinition.maxSkullReward : maxSkullReward;

        PlayerCurrency.Instance?.Add(Random.Range(rollMin, rollMax + 1));
    }

    // Morreu carregando um roubo (state == Returning): larga o ouro no chão em vez de sumir com
    // ele (GDD 2, seção 4 — o drop tem sua própria janela de prioridade/timer, ver DroppedGold).
    void SpawnDroppedGold()
    {
        if (droppedGoldPrefab == null)
        {
            Debug.LogWarning($"{name} morreu carregando {carriedGold} de ouro mas não tem 'Dropped Gold Prefab' atribuído no Inspector — ouro perdido sem dropar.");
            return;
        }

        GameObject drop = ObjectPoolManager.Instance.Get(droppedGoldPrefab, transform.position, Quaternion.identity);
        DroppedGold droppedGold = drop.GetComponent<DroppedGold>();
        droppedGold?.Setup(carriedGold, targetPile);

        carriedGold = 0;
    }

    void FlashRed()
    {
        if (flashRoutine != null)
            StopCoroutine(flashRoutine);

        flashRoutine = StartCoroutine(FlashRoutine());
    }

    IEnumerator FlashRoutine()
    {
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].material.color = hitFlashColor;

        yield return new WaitForSeconds(hitFlashDuration);

        UpdateCarryingVisual();
    }

    // Placeholder de "tá carregando ouro" (GDD 2, pendência #7 — ainda não fechada): tinge o
    // modelo inteiro de amarelo/dourado enquanto carriedGold > 0. Também serve como o estado de
    // "cor de repouso" pro FlashRoutine restaurar depois do flash de dano.
    void UpdateCarryingVisual()
    {
        if (renderers == null) return;

        for (int i = 0; i < renderers.Length; i++)
            renderers[i].material.color = carriedGold > 0 ? carryingTintColor : originalColors[i];
    }

    // ---------- ZONAS: consulta usada pela bomba ----------

    public ZoneConfig GetZoneByCollider(Collider hitCollider)
    {
        if (zones == null) return null;

        foreach (var zone in zones)
        {
            if (zone.zoneCollider == hitCollider)
                return zone;
        }
        return null;
    }

    // ---------- ZONAS: v�nculo entre porcentagens + layout autom�tico ----------

    void OnValidate()
    {
        if (zones == null || zones.Length == 0) return;

        float prevSum = 0f;
        foreach (var z in zones) prevSum += z.previousPercent;

        if (prevSum < 0.001f)
        {
            foreach (var z in zones) z.previousPercent = z.heightPercent;
        }
        else
        {
            int changedIndex = -1;
            float delta = 0f;

            for (int i = 0; i < zones.Length; i++)
            {
                float diff = zones[i].heightPercent - zones[i].previousPercent;
                if (Mathf.Abs(diff) > 0.0001f)
                {
                    changedIndex = i;
                    delta = diff;
                    break;
                }
            }

            if (changedIndex >= 0)
                RedistributeDelta(changedIndex, delta);

            for (int i = 0; i < zones.Length; i++)
                zones[i].previousPercent = zones[i].heightPercent;
        }

        SyncPreviewSubscription();
        ApplyPreviewScaleIfEditing();
        RecalculateZoneLayout();
    }

    void RedistributeDelta(int changedIndex, float delta)
    {
        float othersSum = 0f;
        for (int i = 0; i < zones.Length; i++)
            if (i != changedIndex) othersSum += zones[i].previousPercent;

        if (othersSum <= 0.0001f)
        {
            zones[changedIndex].heightPercent = zones[changedIndex].previousPercent;
            return;
        }

        float remainingDelta = -delta;
        for (int i = 0; i < zones.Length; i++)
        {
            if (i == changedIndex) continue;
            float proportion = zones[i].previousPercent / othersSum;
            zones[i].heightPercent = Mathf.Clamp01(zones[i].previousPercent + remainingDelta * proportion);
        }

        zones[changedIndex].heightPercent = Mathf.Clamp01(zones[changedIndex].heightPercent);

        float sum = 0f;
        foreach (var z in zones) sum += z.heightPercent;
        if (sum > 0.0001f)
        {
            for (int i = 0; i < zones.Length; i++)
                zones[i].heightPercent /= sum;
        }
    }

    void RecalculateZoneLayout()
    {
        if (zones == null) return;

        // bottomOffset fica FIXO (ancorado perto dos p�s) - s� altura/largura escalam,
        // ent�o o bloco sempre cresce pra CIMA a partir da base, nunca "flutuando" longe do ch�o.
        float scaledHeight = totalHeight * appliedZoneScale;
        float scaledWidth = zoneWidth * appliedZoneScale;

        float top = bottomOffset + scaledHeight;
        float currentTop = top;

        // processa em ORDEM REVERSA: o �LTIMO elemento do array fica no TOPO
        // (assim, com Critical como �ltimo elemento, ele nasce no topo automaticamente)
        for (int i = zones.Length - 1; i >= 0; i--)
        {
            var zone = zones[i];
            if (zone.zoneCollider == null) continue;

            float zoneHeight = Mathf.Max(zone.heightPercent * scaledHeight, 0.001f);
            float centerY = currentTop - zoneHeight / 2f;

            Transform zt = zone.zoneCollider.transform;
            Vector3 localPos = zt.localPosition;
            localPos.y = centerY;
            zt.localPosition = localPos;

            if (zone.zoneCollider is BoxCollider box)
            {
                box.center = Vector3.zero;
                box.size = new Vector3(scaledWidth, zoneHeight, scaledWidth);
            }

            currentTop -= zoneHeight;
        }
    }

    // ---------- PREVIEW NO EDITOR: liga o Preview Definition ao recalculo em tempo real ----------

    void SyncPreviewSubscription()
    {
        if (subscribedPreviewDefinition == previewDefinition) return;

        if (subscribedPreviewDefinition != null)
            subscribedPreviewDefinition.Changed -= HandlePreviewDefinitionChanged;

        subscribedPreviewDefinition = previewDefinition;

        if (subscribedPreviewDefinition != null)
            subscribedPreviewDefinition.Changed += HandlePreviewDefinitionChanged;
    }

    void HandlePreviewDefinitionChanged()
    {
        ApplyPreviewScaleIfEditing();
        RecalculateZoneLayout();
    }

    // Só mexe em appliedZoneScale fora do Play Mode - durante o jogo, quem manda nessa escala
    // é o EnemyDefinition sorteado de verdade pelo SpawnManager (via ApplyDefinition()).
    void ApplyPreviewScaleIfEditing()
    {
        if (Application.isPlaying) return;

        appliedZoneScale = previewDefinition != null
            ? previewDefinition.visualScale * previewDefinition.zoneScaleMultiplier
            : 1f;
    }

    // ---------- GIZMOS ----------

    void OnDrawGizmos()
    {
        if (zones == null) return;

        foreach (var zone in zones)
        {
            if (zone.zoneCollider == null) continue;
            if (!(zone.zoneCollider is BoxCollider box)) continue;

            Gizmos.color = zone.gizmoColor;
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = box.transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.center, box.size);
            Gizmos.matrix = oldMatrix;
        }
    }
}