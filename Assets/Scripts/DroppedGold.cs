using UnityEngine;

// Ouro dropado por um esqueleto morto carregando um roubo (GDD 2, seção 4). Fica no chão por um
// tempo: primeiro só o jogador pode pegar (prioridade), depois um esqueleto elegível também
// poderia (ver nota abaixo); se ninguém pegar, metade volta pra pilha de origem e a outra
// metade some.
//
// GDD 2, pendência #4 (esqueleto só recupera se estiver longe do próprio spawn e não estiver
// já carregando outro roubo) AINDA NÃO está implementada aqui — só a coleta do jogador e a
// resolução automática. Falta o EnemyAI saber procurar/priorizar um DroppedGold próximo; até lá,
// esse ouro só volta por essas duas vias (jogador ou timeout).
public class DroppedGold : MonoBehaviour, IPoolable
{
    [Header("Tempo (GDD 2, pendências #3 e #5)")]
    [Tooltip("Enquanto isso, só o jogador pode pegar.")]
    public float playerPriorityDuration = 3f;
    [Tooltip("Depois da prioridade do jogador, quanto tempo A MAIS até resolver sozinho (metade volta pra pilha, metade some).")]
    public float extraTimeBeforeResolve = 5f;

    [Header("Detecção do jogador")]
    public string playerTag = "Player";

    private int amount;
    private GoldPile originPile;
    private float timer;
    private bool collected;
    private PooledObject pooledObject;

    public void Setup(int goldAmount, GoldPile origin)
    {
        amount = goldAmount;
        originPile = origin;
    }

    public void OnSpawnFromPool()
    {
        timer = 0f;
        collected = false;
    }

    public void OnReturnToPool()
    {
    }

    void Update()
    {
        if (collected) return;

        timer += Time.deltaTime;

        if (timer >= playerPriorityDuration + extraTimeBeforeResolve)
            Resolve();
    }

    void OnTriggerEnter(Collider other)
    {
        if (collected) return;
        if (!other.CompareTag(playerTag)) return;

        Collect();
    }

    // Jogador recuperou a tempo: devolve 100% pra pilha de origem (GDD 2, pendência #12).
    void Collect()
    {
        collected = true;

        if (originPile != null)
            originPile.Deposit(amount);

        ReturnToPool();
    }

    // Ninguém pegou a tempo: metade volta pra pilha de origem, metade some (GDD 2, pendência #5).
    void Resolve()
    {
        collected = true;

        if (originPile != null)
            originPile.Deposit(amount / 2);

        ReturnToPool();
    }

    void ReturnToPool()
    {
        if (pooledObject == null)
            pooledObject = GetComponent<PooledObject>();

        if (pooledObject != null)
            pooledObject.ReturnToPool();
        else
            Destroy(gameObject);
    }
}
