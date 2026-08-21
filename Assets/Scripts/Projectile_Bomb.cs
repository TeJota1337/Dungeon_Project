using UnityEngine;

public class Projectile_Bomb : MonoBehaviour
{
    [Header("Timing")]
    public float destroyAfterSeconds = 5f;

    [Header("Dano base")]
    public int minDamage = 5;
    public int maxDamage = 15;

    [Header("Bomba Gigante")]
    public bool isGiant = false;
    public float giantScaleMultiplier = 3f;
    public float giantDamageMultiplier = 2.5f;
    public float giantExplosionRadius = 2f;

    private Collider bombCollider;

    void Awake()
    {
        bombCollider = GetComponent<Collider>();
    }

    void Start()
    {
        Destroy(gameObject, destroyAfterSeconds);

        if (isGiant)
        {
            transform.localScale *= giantScaleMultiplier;
        }
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

    void OnCollisionEnter(Collision collision)
    {
        EnemyAI directHitEnemy = collision.gameObject.GetComponentInParent<EnemyAI>();

        if (isGiant && giantExplosionRadius > 0f)
        {
            Explode();
        }
        else if (directHitEnemy != null)
        {
            ApplyDamage(directHitEnemy, collision.collider);
        }

        Destroy(gameObject);
    }

    void ApplyDamage(EnemyAI enemy, Collider hitCollider)
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
        if (isGiant) baseDamage = Mathf.RoundToInt(baseDamage * giantDamageMultiplier);

        int finalDamage = Mathf.RoundToInt(baseDamage * multiplier);

        enemy.TakeDamage(finalDamage, isCrit, hitColor);
    }

    void Explode()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, giantExplosionRadius);
        var hitEnemies = new System.Collections.Generic.HashSet<EnemyAI>();

        foreach (var hit in hits)
        {
            EnemyAI enemy = hit.GetComponentInParent<EnemyAI>();
            if (enemy != null && !hitEnemies.Contains(enemy))
            {
                hitEnemies.Add(enemy);
                ApplyDamage(enemy, hit);
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        if (isGiant && giantExplosionRadius > 0f)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            Gizmos.DrawSphere(transform.position, giantExplosionRadius);
        }
    }
}