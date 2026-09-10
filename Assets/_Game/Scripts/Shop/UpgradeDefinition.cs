using UnityEngine;

// Um upgrade sorteável na loja (GDD 2, seção 9) - aumenta o dano de um ItemDefinition específico.
// Comprado com cabeças de esqueleto, aplicado via PlayerUpgrades (efeito dura só a run atual).
// A raridade decide a chance dele aparecer numa oferta (ShopRarityTable, mesmo sistema de
// redistribuição usado em EnemyAI.zones/ItemDefinition.variants).
[CreateAssetMenu(fileName = "NovoUpgrade", menuName = "Dungeon/Upgrade Definition")]
public class UpgradeDefinition : ScriptableObject
{
    [Header("Identificação")]
    public string upgradeName = "Upgrade";
    [TextArea] public string description;
    public int cost = 15;

    [Header("Raridade (chance de aparecer na loja)")]
    public ItemRarity rarity = ItemRarity.Comum;

    [Header("Efeito")]
    [Tooltip("Item cujo dano esse upgrade aumenta (ex: Bomb.asset, Dinamite.asset).")]
    public ItemDefinition targetItem;
    public int damageBonus = 5;
}
