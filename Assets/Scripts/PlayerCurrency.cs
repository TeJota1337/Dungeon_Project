using UnityEngine;

// Cabeças de esqueleto - moeda do jogador pra loja (GDD 2, seções 3 e 9). Separado do ouro da
// dungeon (DungeonGoldManager): ouro é o recurso que o jogador DEFENDE (perder tudo = derrota),
// cabeça é o que ele GANHA ao derrotar esqueletos e GASTA na loja - os dois nunca se misturam.
public class PlayerCurrency : MonoBehaviour
{
    public static PlayerCurrency Instance { get; private set; }

    public int SkullCount { get; private set; }

    public event System.Action<int> Changed;

    void Awake()
    {
        Instance = this;
    }

    public void Add(int amount)
    {
        if (amount <= 0) return;

        SkullCount += amount;
        Changed?.Invoke(SkullCount);
    }

    // Chamado pelo ShopManager ao confirmar uma compra. Não gasta nada (retorna false) se não
    // tiver cabeças suficientes.
    public bool TrySpend(int amount)
    {
        if (amount <= 0) return true;
        if (SkullCount < amount) return false;

        SkullCount -= amount;
        Changed?.Invoke(SkullCount);
        return true;
    }
}
