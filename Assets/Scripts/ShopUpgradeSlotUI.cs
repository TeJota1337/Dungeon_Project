using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Uma oferta de UPGRADE sorteada por raridade (GDD 2, seção 9) - re-rolada a cada abertura da
// loja (ShopManager.RollUpgradeOffers). Comprar aplica um bônus de dano permanente pra run atual
// (PlayerUpgrades) num item específico (UpgradeDefinition.targetItem).
public class ShopUpgradeSlotUI : MonoBehaviour
{
    [Header("Referências (arraste os filhos deste slot)")]
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI descriptionText;
    public TextMeshProUGUI costText;
    public Image rarityBackground;
    public Button buyButton;

    private UpgradeDefinition currentUpgrade;
    private ShopManager shop;

    public void Setup(UpgradeDefinition upgrade, RarityPalette palette, ShopManager shopManager)
    {
        currentUpgrade = upgrade;
        shop = shopManager;

        gameObject.SetActive(upgrade != null);
        if (upgrade == null) return;

        if (nameText != null) nameText.text = upgrade.upgradeName;
        if (descriptionText != null) descriptionText.text = upgrade.description;
        if (costText != null) costText.text = upgrade.cost.ToString();

        if (rarityBackground == null)
            Debug.LogWarning($"{name}: 'Rarity Background' não está atribuído no Inspector - a cor da raridade não vai aparecer.");
        else if (palette == null)
            Debug.LogWarning("ShopManager: 'Palette' não está atribuído no Inspector - a cor da raridade não vai aparecer.");
        else
            rarityBackground.color = palette.GetColor(upgrade.rarity);

        RefreshAffordability();
    }

    // Ligue o OnClick() do Buy Button neste método pelo Inspector.
    public void OnBuyClicked()
    {
        if (currentUpgrade == null || shop == null) return;

        // some do Canvas ao comprar - Setup() religa o slot sozinho (com um novo sorteio) na
        // próxima vez que a loja abrir.
        if (shop.TryPurchaseUpgrade(currentUpgrade))
            gameObject.SetActive(false);
    }

    void RefreshAffordability()
    {
        if (buyButton == null || currentUpgrade == null) return;

        int skulls = PlayerCurrency.Instance != null ? PlayerCurrency.Instance.SkullCount : 0;
        buyButton.interactable = skulls >= currentUpgrade.cost;
    }
}
