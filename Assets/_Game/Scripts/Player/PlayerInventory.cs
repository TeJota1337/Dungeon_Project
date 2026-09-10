using System.Collections.Generic;
using UnityEngine;

// Estoque e item equipado do jogador (GDD 2, seção 8). O item com unlimitedStock=true (a pedra,
// tipicamente) é o fallback padrão: sempre disponível, nunca esgota, não ocupa compra nenhuma.
//
// Ainda não existe UI de loja/inventário (GDD 2, pendência #9) - por enquanto AddStock/EquipItem
// só são chamados pelo gancho de teste abaixo (Debug Starting Item). Quando a loja existir, ela
// chama os mesmos métodos.
public class PlayerInventory : MonoBehaviour
{
    public static PlayerInventory Instance { get; private set; }

    [Header("Item padrão (pedra) — ilimitado, sempre disponível")]
    public ItemDefinition defaultItem;

    [Header("Teste (placeholder até ter loja/inventário de verdade)")]
    [Tooltip("Se setado, esse item já nasce equipado com estoque cheio - troque aqui pra testar itens diferentes sem precisar de UI.")]
    public ItemDefinition debugStartingItem;
    public int debugStartingStock = 10;

    public ItemDefinition EquippedItem { get; private set; }

    // Exposto pra UI (ex: mini-canvas de inventário na mão) listar os especiais disponíveis e
    // o estoque de cada um, na mesma ordem que o ciclo do grip percorre.
    public IReadOnlyList<ItemDefinition> PurchasedItems => purchasedOrder;

    private readonly Dictionary<ItemDefinition, int> stock = new Dictionary<ItemDefinition, int>();

    // Ordem em que os itens comprados (não ilimitados) foram adquiridos pela primeira vez -
    // usada pelo ciclo do grip (SlingshotController), pra sempre andar na mesma ordem
    // (Dictionary não garante ordem nenhuma).
    private readonly List<ItemDefinition> purchasedOrder = new List<ItemDefinition>();
    private int cycleIndex = -1;

    void Awake()
    {
        Instance = this;
        EquippedItem = defaultItem;
    }

    void Start()
    {
        if (debugStartingItem != null)
        {
            AddStock(debugStartingItem, debugStartingStock);
            EquipItem(debugStartingItem);
        }
    }

    // Chamado pela loja ao comprar um item.
    public void AddStock(ItemDefinition item, int amount)
    {
        if (item == null || amount <= 0) return;

        if (!item.unlimitedStock && !purchasedOrder.Contains(item))
            purchasedOrder.Add(item);

        stock[item] = GetStock(item) + amount;
    }

    public int GetStock(ItemDefinition item)
    {
        if (item == null) return 0;
        if (item.unlimitedStock) return int.MaxValue;

        return stock.TryGetValue(item, out int count) ? count : 0;
    }

    // Troca o item equipado. Só troca se tiver estoque (ou for ilimitado) - senão ignora e
    // mantém o item atual equipado.
    public void EquipItem(ItemDefinition item)
    {
        if (item == null) return;
        if (GetStock(item) <= 0) return;

        EquippedItem = item;
    }

    // Chamado pelo SlingshotController ao apertar o GRIP - avança pro próximo item comprado
    // (não a pedra), ciclando na ordem de compra. Pula itens sem estoque; se nenhum comprado
    // tiver estoque, não muda nada (mantém o que já estava equipado).
    public void CycleToNextPurchased()
    {
        if (purchasedOrder.Count == 0) return;

        for (int i = 0; i < purchasedOrder.Count; i++)
        {
            cycleIndex = (cycleIndex + 1) % purchasedOrder.Count;
            ItemDefinition candidate = purchasedOrder[cycleIndex];

            if (GetStock(candidate) > 0)
            {
                EquipItem(candidate);
                return;
            }
        }
    }

    // Chamado pelo SlingshotController ao apertar o TRIGGER - sempre volta pro item padrão
    // (pedra/hit básico), independente de qual item comprado estava equipado pelo ciclo do grip.
    public void EquipDefault()
    {
        if (defaultItem != null)
            EquipItem(defaultItem);
    }

    // Chamado pelo SlingshotController ao lançar. Retorna false se não sobrou estoque -
    // nesse caso o SlingshotController não deve nem spawnar o projétil.
    public bool TryConsumeEquipped()
    {
        if (EquippedItem == null) return false;
        if (EquippedItem.unlimitedStock) return true;

        int current = GetStock(EquippedItem);
        if (current <= 0) return false;

        stock[EquippedItem] = current - 1;
        return true;
    }
}
