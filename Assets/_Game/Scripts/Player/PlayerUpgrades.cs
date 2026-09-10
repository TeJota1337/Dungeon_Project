using System.Collections.Generic;
using UnityEngine;

// Upgrades comprados na run atual (GDD 2, seção 9) - guarda o bônus de dano acumulado por
// ItemDefinition. Reseta sozinho a cada run porque é um singleton normal de cena (nada aqui
// persiste em PlayerPrefs - só o Leaderboard grava dado entre sessões).
public class PlayerUpgrades : MonoBehaviour
{
    public static PlayerUpgrades Instance { get; private set; }

    private readonly Dictionary<ItemDefinition, int> damageBonus = new Dictionary<ItemDefinition, int>();

    void Awake()
    {
        Instance = this;
    }

    // Chamado pelo ShopManager ao confirmar a compra de um upgrade.
    public void Apply(UpgradeDefinition upgrade)
    {
        if (upgrade == null || upgrade.targetItem == null) return;

        damageBonus[upgrade.targetItem] = GetDamageBonus(upgrade.targetItem) + upgrade.damageBonus;
    }

    // Consultado pelo projétil (ex: Projectile_Bomb) na hora de calcular o dano final.
    public int GetDamageBonus(ItemDefinition item)
    {
        if (item == null) return 0;
        return damageBonus.TryGetValue(item, out int bonus) ? bonus : 0;
    }
}
