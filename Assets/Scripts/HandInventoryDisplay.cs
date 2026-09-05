using UnityEngine;

// Mini-canvas na mão mostrando os projéteis especiais comprados + estoque de cada um (GDD 2,
// seção 8), com o equipado no momento destacado. Não faz gaze detection sozinho - o projeto já
// tem isso pros affordances do controle (Unity.VRTemplate.CalloutGazeController, em
// Assets/VRTemplateAssets/Scripts). Ligue os eventos Facing Entered/Facing Exited dele a
// Show()/Hide() aqui, mesmo padrão que Assets/VRTemplateAssets/Scripts/Callout.cs já usa.
public class HandInventoryDisplay : MonoBehaviour
{
    [Tooltip("Um slot por item comprado exibido simultaneamente - se tiver mais itens comprados do que slots, os excedentes não aparecem.")]
    public HandInventorySlotUI[] slots;

    void OnEnable()
    {
        Refresh(); // evita 1 frame com conteúdo velho antes do primeiro Update
    }

    // Ligue ao "Facing Entered" do CalloutGazeController.
    public void Show()
    {
        gameObject.SetActive(true);
    }

    // Ligue ao "Facing Exited" do CalloutGazeController.
    public void Hide()
    {
        gameObject.SetActive(false);
    }

    void Update()
    {
        // só roda enquanto este objeto está ativo (ou seja, só enquanto o jogador olha pra mão) -
        // atualiza ao vivo pra refletir na hora se o jogador cicla o grip nesse meio tempo.
        Refresh();
    }

    void Refresh()
    {
        if (PlayerInventory.Instance == null || slots == null) return;

        var purchased = PlayerInventory.Instance.PurchasedItems;
        ItemDefinition equipped = PlayerInventory.Instance.EquippedItem;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;

            ItemDefinition item = i < purchased.Count ? purchased[i] : null;
            int count = PlayerInventory.Instance.GetStock(item);
            slots[i].Setup(item, count, item != null && item == equipped);
        }
    }
}
