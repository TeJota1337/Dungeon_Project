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
public class EnemyAI : MonoBehaviour
{
    [Header("Navegação")]
    public Transform objective;

    [Header("Vida")]
    public int minHealth = 5;
    public int maxHealth = 15;

    private int health;      // vida atual, sorteada no Awake
    private int rolledMax;   // valor máximo sorteado pra ESTE inimigo (usado no display de %)
    private EnemyHealthDisplay healthDisplay;

    [Header("Feedback de dano")]
    public Color hitFlashColor = Color.red;
    public float hitFlashDuration = 0.15f;

    [Header("Zonas de Dano")]
    public float bottomOffset = 0f;   // onde o bloco de zonas COMEÇA (base), em Y local
    public float totalHeight = 2f;    // altura total do bloco (onde TERMINA = bottomOffset + totalHeight)
    public float zoneWidth = 0.6f;    // largura/profundidade de cada zona (eixos X e Z)
    public ZoneConfig[] zones = new ZoneConfig[4];

    private NavMeshAgent agent;
    private Renderer[] renderers;
    private Color[] originalColors;
    private Coroutine flashRoutine;

    // ---------- SETUP PADRÃO (roda ao adicionar o componente) ----------

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
        renderers = GetComponentsInChildren<Renderer>();

        originalColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            originalColors[i] = renderers[i].material.color;
        }

        health = Random.Range(minHealth, maxHealth + 1);
        rolledMax = health;

        // MUDANÇA AQUI: de GetComponent para GetComponentInChildren
        healthDisplay = GetComponentInChildren<EnemyHealthDisplay>();

        if (healthDisplay != null)
            healthDisplay.Initialize(health);
    }

    void Start()
    {
        if (objective != null)
            agent.SetDestination(objective.position);
    }

    void Update()
    {
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            OnReachObjective();
        }
    }

    void OnReachObjective()
    {
        Debug.Log($"{name} chegou na dungeon!");
        Destroy(gameObject);
    }

    // ---------- DANO ----------

    public void TakeDamage(int amount, bool isCrit = false, Color? hitColor = null)
    {
        health -= amount;
        Debug.Log($"{name} tomou {amount} de dano{(isCrit ? " (CRÍTICO)" : "")}. Vida restante: {health}/{rolledMax}");

        if (healthDisplay != null)
            healthDisplay.UpdateDisplay(health);

        Color colorToUse = hitColor ?? Color.white;
        DamageNumberManager.Instance.Spawn(gameObject, transform.position, amount, isCrit, colorToUse);

        FlashRed();

        if (health <= 0)
        {
            Destroy(gameObject);
        }
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

        for (int i = 0; i < renderers.Length; i++)
            renderers[i].material.color = originalColors[i];
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

    // ---------- ZONAS: vínculo entre porcentagens + layout automático ----------

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

        // o bloco vai de "bottomOffset" até "bottomOffset + totalHeight"
        float top = bottomOffset + totalHeight;
        float currentTop = top;

        // processa em ORDEM REVERSA: o ÚLTIMO elemento do array fica no TOPO
        // (assim, com Critical como último elemento, ele nasce no topo automaticamente)
        for (int i = zones.Length - 1; i >= 0; i--)
        {
            var zone = zones[i];
            if (zone.zoneCollider == null) continue;

            float zoneHeight = Mathf.Max(zone.heightPercent * totalHeight, 0.001f);
            float centerY = currentTop - zoneHeight / 2f;

            Transform zt = zone.zoneCollider.transform;
            Vector3 localPos = zt.localPosition;
            localPos.y = centerY;
            zt.localPosition = localPos;

            if (zone.zoneCollider is BoxCollider box)
            {
                box.center = Vector3.zero;
                box.size = new Vector3(zoneWidth, zoneHeight, zoneWidth);
            }

            currentTop -= zoneHeight;
        }
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