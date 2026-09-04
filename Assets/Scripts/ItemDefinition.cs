using UnityEngine;

// Uma variante de prefab possível pra um item, com sua chance (%) de ser sorteada a cada
// lançamento - a soma sempre fica em 100% (ver OnValidate). Migrado de SlingshotController.BombOption:
// antes era um sorteio global de "qual bomba", agora é por item (cada ItemDefinition tem o seu).
[System.Serializable]
public class ProjectileVariant
{
    public GameObject prefab;

    [Range(0f, 100f)]
    public float chancePercent = 100f;

    [HideInInspector] public float previousPercent;
}

// Dados de um item lançável pelo estilingue (GDD 2, seção 7). Mesmo espírito do EnemyDefinition:
// o comportamento de combate fica no próprio prefab do projétil (ex.: Projectile_Bomb já é
// autossuficiente - dano, splash, chance de gigante etc.) - esse asset só descreve o que é
// necessário pra loja/inventário saberem lidar com o item (custo, estoque, ícone) e quais
// variantes de prefab o SlingshotController pode instanciar ao lançar.
[CreateAssetMenu(fileName = "NovoItem", menuName = "Dungeon/Item Definition")]
public class ItemDefinition : ScriptableObject
{
    [Header("Identificação")]
    public string itemName = "Item";
    [Tooltip("Placeholder até ter arte de verdade pro inventário/loja.")]
    public Sprite icon;
    [TextArea] public string description;

    [Header("Projétil")]
    [Tooltip("Variantes de prefab possíveis pra esse item (cada uma precisa implementar IThrowable - Projectile_Bomb já implementa). Normalmente só 1 elemento (100%); use mais de um só se quiser variedade dentro do mesmo item (ex: bomba normal vs especial).")]
    public ProjectileVariant[] variants = new ProjectileVariant[] { new ProjectileVariant() };

    [Header("Loja / Estoque")]
    [Tooltip("Se marcado, esse item nunca acaba (ex: a pedra padrão) - ignora estoque e custo.")]
    public bool unlimitedStock = false;
    public int cost = 10;
    [Tooltip("Quantos você recebe por compra na loja.")]
    public int stockPerPurchase = 3;

    // Sorteio ponderado pelo Chance Percent de cada variante.
    public GameObject PickPrefab()
    {
        if (variants == null || variants.Length == 0) return null;
        if (variants.Length == 1) return variants[0].prefab;

        float roll = Random.Range(0f, 100f);
        float cumulative = 0f;

        foreach (var variant in variants)
        {
            cumulative += variant.chancePercent;
            if (roll <= cumulative) return variant.prefab;
        }

        return variants[variants.Length - 1].prefab;
    }

    // ---------- VARIANTES: mexer numa chance redistribui o resto pra soma ficar sempre em 100% ----------
    // (mesmo padrão de EnemyAI.zones / do antigo SlingshotController.bombOptions)

    void OnValidate()
    {
        if (variants == null || variants.Length == 0) return;

        float prevSum = 0f;
        foreach (var v in variants) prevSum += v.previousPercent;

        if (prevSum < 0.001f)
        {
            float equalShare = 100f / variants.Length;
            foreach (var v in variants)
            {
                v.chancePercent = equalShare;
                v.previousPercent = equalShare;
            }
        }
        else
        {
            int changedIndex = -1;
            float delta = 0f;

            for (int i = 0; i < variants.Length; i++)
            {
                float diff = variants[i].chancePercent - variants[i].previousPercent;
                if (Mathf.Abs(diff) > 0.0001f)
                {
                    changedIndex = i;
                    delta = diff;
                    break;
                }
            }

            if (changedIndex >= 0)
                RedistributeDelta(changedIndex, delta);

            for (int i = 0; i < variants.Length; i++)
                variants[i].previousPercent = variants[i].chancePercent;
        }
    }

    void RedistributeDelta(int changedIndex, float delta)
    {
        float othersSum = 0f;
        for (int i = 0; i < variants.Length; i++)
            if (i != changedIndex) othersSum += variants[i].previousPercent;

        if (othersSum <= 0.0001f)
        {
            variants[changedIndex].chancePercent = variants[changedIndex].previousPercent;
            return;
        }

        float remainingDelta = -delta;
        for (int i = 0; i < variants.Length; i++)
        {
            if (i == changedIndex) continue;
            float proportion = variants[i].previousPercent / othersSum;
            variants[i].chancePercent = Mathf.Clamp(variants[i].previousPercent + remainingDelta * proportion, 0f, 100f);
        }

        variants[changedIndex].chancePercent = Mathf.Clamp(variants[changedIndex].chancePercent, 0f, 100f);

        float sum = 0f;
        foreach (var v in variants) sum += v.chancePercent;
        if (sum > 0.0001f)
        {
            for (int i = 0; i < variants.Length; i++)
                variants[i].chancePercent = variants[i].chancePercent / sum * 100f;
        }
    }
}
