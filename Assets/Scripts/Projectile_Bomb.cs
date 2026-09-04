using UnityEngine;

public class Projectile_Bomb : MonoBehaviour, IPoolable, IThrowable
{
    [Header("Timing")]
    public float destroyAfterSeconds = 5f;

    [Header("Dano base")]
    public int minDamage = 5;
    public int maxDamage = 15;

    [Header("Bomba Gigante")]
    [Tooltip("Chance de QUALQUER lançamento virar gigante - sorteada pela própria bomba a cada spawn (antes ficava no SlingshotController, mas isso é comportamento específico de bomba, não do estilingue).")]
    [Range(0f, 1f)]
    public float giantChance = 0.05f;
    [SerializeField] private bool isGiantSerialized = false;
    public float giantScaleMultiplier = 3f;
    public float giantDamageMultiplier = 2.5f;
    public float giantExplosionRadius = 2f;

    [Header("Dano em Área (quase-acerto)")]
    [Tooltip("Se a bomba (não-gigante) colidir sem acertar um inimigo diretamente, inimigos dentro desse raio da explosão tomam dano reduzido.")]
    public float splashRadius = 1.5f;
    [Range(0f, 1f)]
    public float splashDamageMultiplier = 0.5f;

    [Header("VFX de Explos�o")]
    public GameObject explosionPrefab;
    public float explosionScaleMultiplier = 1f; // multiplicado extra se for gigante

    private Collider bombCollider;
    private Rigidbody bombRigidbody;
    private PooledObject pooledObject;
    private Vector3 baseScale;
    private bool hasExploded; // trava contra explodir duas vezes
    private ItemDefinition sourceItem; // qual ItemDefinition lançou essa instância (upgrades de dano da loja)

    // setar isGiant já aplica a escala correspondente na hora, então funciona
    // independente da ordem entre "vir do pool" e "SlingshotController decidir se é gigante"
    public bool isGiant
    {
        get => isGiantSerialized;
        set
        {
            isGiantSerialized = value;
            transform.localScale = value ? baseScale * giantScaleMultiplier : baseScale;
        }
    }

    void Awake()
    {
        bombCollider = GetComponent<Collider>();
        bombRigidbody = GetComponent<Rigidbody>();
        baseScale = transform.localScale;
    }

    // ---------- POOLING: reinicializa tudo que era feito em Start,
    // j� que s� roda uma vez na vida do GameObject e n�o dispara de novo a cada reuso ----------

    public void OnSpawnFromPool()
    {
        hasExploded = false;

        // sorteia se ESSE lançamento é gigante (o setter de isGiant já aplica a escala certa)
        isGiant = Random.value < giantChance;

        if (bombCollider != null)
            bombCollider.enabled = true;

        if (bombRigidbody != null)
        {
            bombRigidbody.linearVelocity = Vector3.zero;
            bombRigidbody.angularVelocity = Vector3.zero;
        }

        // cancela um timeout pendente de um uso anterior antes de agendar o novo
        CancelInvoke(nameof(ExplodeFromTimeout));
        Invoke(nameof(ExplodeFromTimeout), destroyAfterSeconds);
    }

    public void OnReturnToPool()
    {
        CancelInvoke(nameof(ExplodeFromTimeout));
    }

    public void SetCollisionEnabled(bool enabled)
    {
        if (bombCollider != null)
            bombCollider.enabled = enabled;
    }

    public void IgnoreCollisionsWith(Collider[] collidersToIgnore)
    {
        if (bombCollider == null || collidersToIgnore == null) return;

        foreach (var col in collidersToIgnore)
        {
            if (col != null)
                Physics.IgnoreCollision(bombCollider, col, true);
        }
    }

    public void SetSourceItem(ItemDefinition item)
    {
        sourceItem = item;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (hasExploded) return;

        EnemyAI directHitEnemy = collision.gameObject.GetComponentInParent<EnemyAI>();
        bool dealtDamage = false;

        if (isGiant && giantExplosionRadius > 0f)
        {
            dealtDamage = DamageEnemiesInRadius(giantExplosionRadius, 1f);
        }
        else if (directHitEnemy != null)
        {
            ApplyDamage(directHitEnemy, collision.collider, 1f);
            dealtDamage = true;
        }
        else if (splashRadius > 0f)
        {
            // n�o acertou um inimigo diretamente: quem estiver perto o suficiente do ponto de impacto toma dano reduzido
            dealtDamage = DamageEnemiesInRadius(splashRadius, splashDamageMultiplier);
        }

        // um pulso s� por bomba (n�o um por inimigo atingido), pra n�o spamar o motor h�ptico em AoE
        if (dealtDamage)
            PlayerHaptics.Instance?.HitConfirm();

        SpawnExplosionVFX();
        GameAudio.Instance?.PlayBombExplosion(transform.position, isGiant);
        hasExploded = true;
        ReturnToPool();
    }

    void ExplodeFromTimeout()
    {
        if (hasExploded) return; // j� explodiu por colis�o antes do timeout disparar

        SpawnExplosionVFX();
        GameAudio.Instance?.PlayBombExplosion(transform.position, isGiant);
        hasExploded = true;
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

    void SpawnExplosionVFX()
    {
        if (explosionPrefab == null) return;

        GameObject vfx = ObjectPoolManager.Instance.Get(explosionPrefab, transform.position, Quaternion.identity);

        float scale = explosionScaleMultiplier * (isGiant ? giantScaleMultiplier : 1f);
        vfx.transform.localScale = Vector3.one * scale;
    }

    void ApplyDamage(EnemyAI enemy, Collider hitCollider, float extraMultiplier)
    {
        ZoneConfig zone = enemy.GetZoneByCollider(hitCollider);

        if (zone == null)
        {
            Debug.LogWarning($"Bomba acertou '{hitCollider.name}' mas nenhuma zona corresponde a esse collider.");
        }

        float multiplier = zone != null ? zone.damageMultiplier : 1f;
        Color hitColor = zone != null ? zone.gizmoColor : Color.white;
        bool isCrit = zone != null && zone.type == ZoneType.Critical;

        int baseDamage = Random.Range(minDamage, maxDamage + 1);
        if (PlayerUpgrades.Instance != null) baseDamage += PlayerUpgrades.Instance.GetDamageBonus(sourceItem);
        if (isGiant) baseDamage = Mathf.RoundToInt(baseDamage * giantDamageMultiplier);

        int finalDamage = Mathf.RoundToInt(baseDamage * multiplier * extraMultiplier);

        enemy.TakeDamage(finalDamage, isCrit, hitColor);
    }

    bool DamageEnemiesInRadius(float radius, float extraMultiplier)
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, radius);
        var hitEnemies = new System.Collections.Generic.HashSet<EnemyAI>();

        foreach (var hit in hits)
        {
            EnemyAI enemy = hit.GetComponentInParent<EnemyAI>();
            if (enemy != null && !hitEnemies.Contains(enemy))
            {
                hitEnemies.Add(enemy);
                ApplyDamage(enemy, hit, extraMultiplier);
            }
        }

        return hitEnemies.Count > 0;
    }

    void OnDrawGizmosSelected()
    {
        if (isGiant && giantExplosionRadius > 0f)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            Gizmos.DrawSphere(transform.position, giantExplosionRadius);
        }
        else if (splashRadius > 0f)
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.25f);
            Gizmos.DrawSphere(transform.position, splashRadius);
        }
    }
}