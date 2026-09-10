using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Um slot do mini-inventário na mão (GDD 2, seção 8) - ícone + quantidade de UM item comprado.
// HandInventoryDisplay preenche via Setup() enquanto o painel estiver visível.
public class HandInventorySlotUI : MonoBehaviour
{
    public Image icon;
    public TextMeshProUGUI countText;
    [Tooltip("Mostra o range de dano (min-max) do projétil da primeira variante do item, já somando bônus de upgrade comprado, se houver.")]
    public TextMeshProUGUI damageText;
    [Tooltip("Objeto de destaque (borda, fundo etc.) - ligado só quando esse é o item equipado no momento. Opcional.")]
    public GameObject equippedHighlight;

    public void Setup(ItemDefinition item, int count, bool isEquipped)
    {
        gameObject.SetActive(item != null);
        if (item == null) return;

        if (icon != null) icon.sprite = item.icon;
        if (countText != null) countText.text = count.ToString();
        if (equippedHighlight != null) equippedHighlight.SetActive(isEquipped);

        if (damageText != null)
            damageText.text = GetDamageRangeText(item);
    }

    // Dano vem do Projectile_Bomb da primeira variante do item, não do ItemDefinition em si -
    // soma o bônus acumulado em PlayerUpgrades pra mostrar o dano real, não só o valor base.
    static string GetDamageRangeText(ItemDefinition item)
    {
        GameObject prefab = item.variants != null && item.variants.Length > 0 ? item.variants[0].prefab : null;
        Projectile_Bomb proj = prefab != null ? prefab.GetComponent<Projectile_Bomb>() : null;
        if (proj == null) return "";

        int bonus = PlayerUpgrades.Instance != null ? PlayerUpgrades.Instance.GetDamageBonus(item) : 0;
        return $"{proj.minDamage + bonus}-{proj.maxDamage + bonus}";
    }
}
