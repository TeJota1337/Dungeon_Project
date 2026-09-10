using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Uma oferta FIXA da loja (Dinamite, Bomba, Espada...) - sempre visível, sem sorteio (GDD 2,
// seção 9). O ShopManager preenche via Setup() toda vez que a loja abre; o item exibido em si
// não muda de uma visita pra outra, só a possibilidade de comprar (depende do saldo).
public class ShopItemSlotUI : MonoBehaviour
{
    [Header("Referências (arraste os filhos deste slot)")]
    public Image icon;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI costText;
    public TextMeshProUGUI stockText;
    public Button buyButton;

    private ItemDefinition currentItem;
    private ShopManager shop;

    public void Setup(ItemDefinition item, ShopManager shopManager)
    {
        currentItem = item;
        shop = shopManager;

        gameObject.SetActive(item != null);
        if (item == null) return;

        if (icon != null) icon.sprite = item.icon;
        if (nameText != null) nameText.text = item.itemName;
        if (costText != null) costText.text = item.cost.ToString();
        if (stockText != null)
        {
            stockText.text = string.IsNullOrEmpty(item.stockLabel)
                ? item.stockPerPurchase.ToString()
                : $"{item.stockPerPurchase} {item.stockLabel}";
        }

        RefreshAffordability();
    }

    // Ligue o OnClick() do Buy Button neste método pelo Inspector.
    public void OnBuyClicked()
    {
        if (currentItem == null || shop == null) return;

        // some do Canvas ao comprar - Setup() religa o slot sozinho na próxima vez que a loja abrir.
        if (shop.TryPurchaseItem(currentItem))
            gameObject.SetActive(false);
    }

    void RefreshAffordability()
    {
        if (buyButton == null || currentItem == null) return;

        int skulls = PlayerCurrency.Instance != null ? PlayerCurrency.Instance.SkullCount : 0;
        buyButton.interactable = skulls >= currentItem.cost;
    }
}
