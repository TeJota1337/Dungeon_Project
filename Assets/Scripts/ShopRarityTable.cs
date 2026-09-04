using UnityEngine;

// Peso de uma raridade dentro de uma ShopRarityTable - a soma de todas sempre fica em 100%
// (mesmo padrão de redistribuição de EnemyAI.zones / ItemDefinition.variants: mexer numa
// redistribui o resto proporcionalmente).
[System.Serializable]
public class RarityWeight
{
    public ItemRarity rarity;

    [Range(0f, 100f)]
    public float chancePercent;

    [HideInInspector] public float previousPercent;
}

// Tabela de chance de raridade pra uma "leva" de ofertas da loja (GDD 2, seção 9). O
// ShopManager guarda várias, uma por faixa de wave (ShopManager.progression) - isso dá a
// progressão "raridade sobe conforme o jogo avança" sem precisar de fórmula, só trocando o
// asset usado em cada faixa.
[CreateAssetMenu(fileName = "NovaTabelaDeRaridade", menuName = "Dungeon/Shop Rarity Table")]
public class ShopRarityTable : ScriptableObject
{
    public RarityWeight[] weights = new RarityWeight[]
    {
        new RarityWeight { rarity = ItemRarity.Comum,    chancePercent = 45f },
        new RarityWeight { rarity = ItemRarity.Incomum,  chancePercent = 25f },
        new RarityWeight { rarity = ItemRarity.Raro,     chancePercent = 15f },
        new RarityWeight { rarity = ItemRarity.Epico,    chancePercent = 10f },
        new RarityWeight { rarity = ItemRarity.Lendario, chancePercent = 4f },
        new RarityWeight { rarity = ItemRarity.Mitico,   chancePercent = 1f },
    };

    // Sorteio ponderado pelo Chance Percent de cada raridade.
    public ItemRarity PickRarity()
    {
        if (weights == null || weights.Length == 0) return ItemRarity.Comum;

        float roll = Random.Range(0f, 100f);
        float cumulative = 0f;

        foreach (var w in weights)
        {
            cumulative += w.chancePercent;
            if (roll <= cumulative) return w.rarity;
        }

        return weights[weights.Length - 1].rarity;
    }

    // ---------- RARIDADES: mexer numa chance redistribui o resto pra soma ficar sempre em 100% ----------

    void OnValidate()
    {
        if (weights == null || weights.Length == 0) return;

        float prevSum = 0f;
        foreach (var w in weights) prevSum += w.previousPercent;

        if (prevSum < 0.001f)
        {
            float equalShare = 100f / weights.Length;
            foreach (var w in weights)
            {
                w.chancePercent = equalShare;
                w.previousPercent = equalShare;
            }
        }
        else
        {
            int changedIndex = -1;
            float delta = 0f;

            for (int i = 0; i < weights.Length; i++)
            {
                float diff = weights[i].chancePercent - weights[i].previousPercent;
                if (Mathf.Abs(diff) > 0.0001f)
                {
                    changedIndex = i;
                    delta = diff;
                    break;
                }
            }

            if (changedIndex >= 0)
                RedistributeDelta(changedIndex, delta);

            for (int i = 0; i < weights.Length; i++)
                weights[i].previousPercent = weights[i].chancePercent;
        }
    }

    void RedistributeDelta(int changedIndex, float delta)
    {
        float othersSum = 0f;
        for (int i = 0; i < weights.Length; i++)
            if (i != changedIndex) othersSum += weights[i].previousPercent;

        if (othersSum <= 0.0001f)
        {
            weights[changedIndex].chancePercent = weights[changedIndex].previousPercent;
            return;
        }

        float remainingDelta = -delta;
        for (int i = 0; i < weights.Length; i++)
        {
            if (i == changedIndex) continue;
            float proportion = weights[i].previousPercent / othersSum;
            weights[i].chancePercent = Mathf.Clamp(weights[i].previousPercent + remainingDelta * proportion, 0f, 100f);
        }

        weights[changedIndex].chancePercent = Mathf.Clamp(weights[changedIndex].chancePercent, 0f, 100f);

        float sum = 0f;
        foreach (var w in weights) sum += w.chancePercent;
        if (sum > 0.0001f)
        {
            for (int i = 0; i < weights.Length; i++)
                weights[i].chancePercent = weights[i].chancePercent / sum * 100f;
        }
    }
}
