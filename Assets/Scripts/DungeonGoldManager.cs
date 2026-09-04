using System.Collections.Generic;
using UnityEngine;

// Substitui o GemObjective (branch main) como condição de derrota nesta expansão roguelite:
// a run acaba quando a soma do ouro de TODAS as GoldPile da dungeon chega a 0.
// Ouro "em trânsito" (sendo carregado por um esqueleto vivo) já saiu da pilha no momento do
// roubo (ver GoldPile.Withdraw) e não conta mais pra esse total - só volta a contar se for
// devolvido de verdade via GoldPile.Deposit (drop resolvendo sozinho, ou o jogador recuperando).
public class DungeonGoldManager : MonoBehaviour
{
    public static DungeonGoldManager Instance { get; private set; }

    public int TotalGold { get; private set; }

    private readonly List<GoldPile> piles = new List<GoldPile>();
    private bool defeatTriggered;

    void Awake()
    {
        Instance = this;

        // acha as pilhas direto na cena em vez de depender de auto-registro
        piles.AddRange(FindObjectsByType<GoldPile>(FindObjectsSortMode.None));
        foreach (var pile in piles)
            pile.Changed += OnPileChanged;
    }

    // A soma inicial só pode rodar em Start(): o Unity garante que TODOS os Awake() da cena já
    // rodaram antes de qualquer Start(), mas NÃO garante ordem entre Awake() de objetos
    // diferentes - se somássemos em Awake(), uma GoldPile cujo Awake ainda não rodou entraria
    // na conta como 0, podendo até disparar derrota falsa logo no início.
    void Start()
    {
        RecalculateTotal();
    }

    void OnDestroy()
    {
        foreach (var pile in piles)
        {
            if (pile != null) pile.Changed -= OnPileChanged;
        }
    }

    void OnPileChanged(int current, int initial)
    {
        RecalculateTotal();
    }

    // Usado pelo EnemyAI (esqueleto ladrão) pra escolher onde roubar - ignora pilhas vazias.
    public GoldPile FindNearestPile(Vector3 fromPosition)
    {
        GoldPile nearest = null;
        float bestSqrDist = float.MaxValue;

        foreach (var pile in piles)
        {
            if (pile == null || pile.IsEmpty) continue;

            float sqrDist = (pile.transform.position - fromPosition).sqrMagnitude;
            if (sqrDist < bestSqrDist)
            {
                bestSqrDist = sqrDist;
                nearest = pile;
            }
        }

        return nearest;
    }

    void RecalculateTotal()
    {
        int total = 0;
        foreach (var pile in piles)
            total += pile.CurrentAmount;

        TotalGold = total;

        if (TotalGold <= 0 && !defeatTriggered)
        {
            defeatTriggered = true;
            GameStateManager.Instance?.TriggerDefeat();
        }
    }
}
