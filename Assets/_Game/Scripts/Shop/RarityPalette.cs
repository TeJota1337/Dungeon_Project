using UnityEngine;

[System.Serializable]
public class RarityColorEntry
{
    public ItemRarity rarity;
    public Color color = Color.white;
}

// Cor de exibição de cada raridade (fundo do slot na loja) - separado de ShopRarityTable de
// propósito: cor é estética e fixa, enquanto a tabela de chance muda por faixa de wave.
[CreateAssetMenu(fileName = "RarityPalette", menuName = "Dungeon/Rarity Palette")]
public class RarityPalette : ScriptableObject
{
    public RarityColorEntry[] colors = new RarityColorEntry[]
    {
        new RarityColorEntry { rarity = ItemRarity.Comum,    color = new Color(0.75f, 0.75f, 0.75f) },
        new RarityColorEntry { rarity = ItemRarity.Incomum,  color = new Color(0.30f, 0.85f, 0.30f) },
        new RarityColorEntry { rarity = ItemRarity.Raro,     color = new Color(0.30f, 0.55f, 1.00f) },
        new RarityColorEntry { rarity = ItemRarity.Epico,    color = new Color(0.65f, 0.30f, 0.90f) },
        new RarityColorEntry { rarity = ItemRarity.Lendario, color = new Color(1.00f, 0.60f, 0.10f) },
        new RarityColorEntry { rarity = ItemRarity.Mitico,   color = new Color(1.00f, 0.20f, 0.35f) },
    };

    public Color GetColor(ItemRarity rarity)
    {
        foreach (var entry in colors)
            if (entry.rarity == rarity) return entry.color;

        return Color.white;
    }
}
