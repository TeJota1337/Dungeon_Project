using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Uma faixa de waves com sua própria tabela de chance de raridade pros UPGRADES (GDD 2, seção 9
// - "a sorte vai progredindo conforme a gameplay"). Ordene por From Wave crescente: ao abrir a
// loja, o ShopManager usa a tabela de maior From Wave que ainda seja <= a próxima wave - dá pra
// configurar "a partir da wave 10 sobe a chance de Épico" só trocando o asset, sem código novo.
[System.Serializable]
public class WaveRarityTable
{
    [Tooltip("Essa tabela vale a partir desta wave (inclusive).")]
    public int fromWave = 1;
    public ShopRarityTable table;
}

// Loja entre waves (GDD 2, seção 9 - "Goblin mercador", por enquanto só o Canvas). Abre quando
// uma wave termina (chamado pelo SpawnManager) e fecha quando a próxima começa. Duas seções
// independentes:
//  - Itens (Dinamite/Bomba/Espada...): SEMPRE visíveis, custo e estoque fixos - sem sorteio.
//  - Upgrades: sorteados por raridade a cada abertura, cada um bonifica o dano de um item
//    específico (ver UpgradeDefinition/PlayerUpgrades). É aqui que entra a progressão de sorte.
// Compra gasta PlayerCurrency (cabeças de esqueleto).
public class ShopManager : MonoBehaviour
{
    public static ShopManager Instance { get; private set; }

    [Header("Itens (fixos, sempre visíveis)")]
    public ItemDefinition[] availableItems;
    [Tooltip("Prefab de UM slot de item (o asset, não uma instância da cena).")]
    public ShopItemSlotUI itemSlotPrefab;
    [Tooltip("Quantas ofertas de item aparecem ao mesmo tempo - o ShopManager instancia essa quantidade sozinho no Awake, não precisa deixar nada pré-colocado na hierarquia.")]
    public int itemSlotCount = 3;
    [Tooltip("Onde os slots de item nascem - coloque um Layout Group aqui (Horizontal/Vertical/Grid) pra eles se organizarem sozinhos.")]
    public Transform itemSlotsParent;

    [Header("Upgrades (sorteados por raridade a cada visita)")]
    public UpgradeDefinition[] availableUpgrades;
    [Tooltip("Ordene por From Wave crescente. Ex.: wave 1 só Comum/Incomum, wave 10 já libera Épico...")]
    public WaveRarityTable[] progression;
    [Tooltip("Prefab de UM slot de upgrade (o asset, não uma instância da cena).")]
    public ShopUpgradeSlotUI upgradeSlotPrefab;
    [Tooltip("Quantas ofertas de upgrade aparecem ao mesmo tempo.")]
    public int upgradeSlotCount = 3;
    [Tooltip("Onde os slots de upgrade nascem - mesma ideia do Item Slots Parent.")]
    public Transform upgradeSlotsParent;
    public RarityPalette palette;

    [Header("UI (Canvas World Space perto da gema)")]
    [Tooltip("Objeto que liga/desliga ao abrir/fechar a loja - normalmente o próprio Canvas.")]
    public GameObject shopRoot;

    private ShopItemSlotUI[] itemSlots;
    private ShopUpgradeSlotUI[] upgradeSlots;

    void Awake()
    {
        Instance = this;

        if (shopRoot != null)
            shopRoot.SetActive(false);

        itemSlots = BuildSlots(itemSlotPrefab, itemSlotCount, itemSlotsParent);
        upgradeSlots = BuildSlots(upgradeSlotPrefab, upgradeSlotCount, upgradeSlotsParent);
    }

    // Instancia Count cópias do prefab do slot dentro de Parent - assim a quantidade de ofertas é
    // só um número no Inspector (fácil de testar 3+5, 5+3 etc.), sem precisar duplicar nada na mão
    // na hierarquia.
    T[] BuildSlots<T>(T prefab, int count, Transform parent) where T : Component
    {
        if (prefab == null || parent == null) return new T[0];

        var slots = new T[count];
        for (int i = 0; i < count; i++)
            slots[i] = Instantiate(prefab, parent);

        return slots;
    }

    // Chamado pelo SpawnManager quando uma wave termina. upcomingWave é o número (1-based) da
    // próxima wave - só usado pra escolher a tabela de raridade dos upgrades em progression.
    public void Open(int upcomingWave)
    {
        if (shopRoot != null)
            shopRoot.SetActive(true);
        else
            Debug.LogWarning("ShopManager: 'Shop Root' não está atribuído no Inspector - a loja não tem o que ligar/desligar (nada vai aparecer).");

        SetupItemSlots();
        RollUpgradeOffers(upcomingWave);
    }

    public void Close()
    {
        if (shopRoot != null)
            shopRoot.SetActive(false);
    }

    void SetupItemSlots()
    {
        if (itemSlots == null) return;

        for (int i = 0; i < itemSlots.Length; i++)
        {
            if (itemSlots[i] == null) continue;

            ItemDefinition item = availableItems != null && i < availableItems.Length ? availableItems[i] : null;
            itemSlots[i].Setup(item, this);
        }
    }

    ShopRarityTable GetTableForWave(int wave)
    {
        ShopRarityTable best = null;
        int bestFromWave = int.MinValue;

        if (progression != null)
        {
            foreach (var entry in progression)
            {
                if (entry.table == null) continue;
                if (entry.fromWave <= wave && entry.fromWave > bestFromWave)
                {
                    best = entry.table;
                    bestFromWave = entry.fromWave;
                }
            }
        }

        return best;
    }

    void RollUpgradeOffers(int upcomingWave)
    {
        if (upgradeSlots == null) return;

        ShopRarityTable table = GetTableForWave(upcomingWave);
        var alreadyOffered = new HashSet<UpgradeDefinition>(); // evita a mesma oferta em 2 slots na mesma visita

        for (int i = 0; i < upgradeSlots.Length; i++)
        {
            if (upgradeSlots[i] == null) continue;

            UpgradeDefinition upgrade = RollUpgrade(table, alreadyOffered);
            if (upgrade != null) alreadyOffered.Add(upgrade);

            upgradeSlots[i].Setup(upgrade, palette, this);
        }
    }

    // Sorteia uma raridade pela tabela e um upgrade dela dentro de availableUpgrades, excluindo o
    // que já saiu num slot anterior nesta mesma abertura da loja. Se não existir nenhum upgrade
    // cadastrado pra raridade sorteada (ou só sobrar o já excluído), cai pra qualquer outro do pool.
    UpgradeDefinition RollUpgrade(ShopRarityTable table, HashSet<UpgradeDefinition> exclude)
    {
        if (availableUpgrades == null || availableUpgrades.Length == 0) return null;

        ItemRarity rarity = table != null ? table.PickRarity() : ItemRarity.Comum;

        var candidates = availableUpgrades.Where(u => u != null && u.rarity == rarity && !exclude.Contains(u)).ToList();
        if (candidates.Count == 0)
            candidates = availableUpgrades.Where(u => u != null && !exclude.Contains(u)).ToList();

        return candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : null;
    }

    // Chamado pelo botão Comprar de um ShopItemSlotUI.
    public bool TryPurchaseItem(ItemDefinition item)
    {
        if (item == null) return false;
        if (PlayerCurrency.Instance == null || PlayerInventory.Instance == null) return false;

        if (!PlayerCurrency.Instance.TrySpend(item.cost)) return false;

        PlayerInventory.Instance.AddStock(item, item.stockPerPurchase);
        return true;
    }

    // Chamado pelo botão Comprar de um ShopUpgradeSlotUI.
    public bool TryPurchaseUpgrade(UpgradeDefinition upgrade)
    {
        if (upgrade == null) return false;
        if (PlayerCurrency.Instance == null || PlayerUpgrades.Instance == null) return false;

        if (!PlayerCurrency.Instance.TrySpend(upgrade.cost)) return false;

        PlayerUpgrades.Instance.Apply(upgrade);
        return true;
    }
}
