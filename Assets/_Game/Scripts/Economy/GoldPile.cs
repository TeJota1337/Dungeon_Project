using UnityEngine;

// Uma pilha de ouro na dungeon - os esqueletos roubam dela, o jogador a defende.
// A quantidade inicial é sorteada num range configurável por pilha (GDD 2, pendência #1:
// resolvida como "min/max por instância", já que cada pilha pode ter um tamanho diferente
// dependendo de onde fica no level design - perto do spawn = pilha menor, no fundo = maior etc.).
public class GoldPile : MonoBehaviour
{
    [Header("Quantidade (sorteada no spawn)")]
    public int minAmount = 30;
    public int maxAmount = 80;

    public int CurrentAmount { get; private set; }
    public int InitialAmount { get; private set; } // valor sorteado no spawn - referência fixa pra UI (ex: "quanto já foi roubado desta pilha")

    public bool IsEmpty => CurrentAmount <= 0;

    // Disparado sempre que a quantidade muda (roubo ou devolução) - (current, initial).
    public event System.Action<int, int> Changed;

    void Awake()
    {
        InitialAmount = Random.Range(minAmount, maxAmount + 1);
        CurrentAmount = InitialAmount;
    }

    // Chamado pelo esqueleto ao roubar (GDD 2, pendência #6: desconta AQUI, no momento do roubo,
    // não quando ele sai de campo). Retorna o valor REALMENTE retirado - pode ser menos que o
    // pedido se a pilha tiver menos disponível; nunca fica negativa.
    public int Withdraw(int amount)
    {
        int taken = Mathf.Min(amount, CurrentAmount);
        if (taken <= 0) return 0;

        CurrentAmount -= taken;
        Changed?.Invoke(CurrentAmount, InitialAmount);
        return taken;
    }

    // Chamado quando ouro volta pra essa pilha - ex: metade de um drop de esqueleto morto
    // resolvendo sozinho, ou o jogador recuperando um drop a tempo.
    public void Deposit(int amount)
    {
        if (amount <= 0) return;

        CurrentAmount += amount;
        Changed?.Invoke(CurrentAmount, InitialAmount);
    }
}
