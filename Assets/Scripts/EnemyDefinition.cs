using UnityEngine;

// Dados de um "tipo" de inimigo (visual, vida, velocidade, peso no sorteio).
// O SpawnManager sorteia um EnemyDefinition por spawn e o EnemyAI se configura
// a partir dele — o prefab do inimigo (rig de zonas de dano, NavMeshAgent etc.)
// continua único e compartilhado entre todos os tipos.
[CreateAssetMenu(fileName = "NovoInimigo", menuName = "Dungeon/Enemy Definition")]
public class EnemyDefinition : ScriptableObject
{
    [Header("Identificação")]
    public string enemyName = "Inimigo";

    [Header("Visual")]
    public VisualVariant[] visualVariants;
    [Tooltip("Escala do modelo (e ponto de partida da escala das zonas de dano).")]
    public float visualScale = 0.7f;
    [Tooltip("Multiplicador extra só sobre as zonas de dano, em cima do Visual Scale. 1 = zona acompanha o Visual Scale igual antes. Suba esse valor se um modelo mais alto/magro renderizar maior do que a zona calculada — o Visual Scale sozinho nem sempre é proporcional ao tamanho real do modelo.")]
    public float zoneScaleMultiplier = 1f;

    [Header("Vida")]
    public int minHealth = 10;
    public int maxHealth = 15;

    [Header("Velocidade")]
    [Tooltip("Velocidade do NavMeshAgent, sorteada entre esses dois valores a cada spawn.")]
    public float minSpeed = 3f;
    public float maxSpeed = 4.2f;

    [Header("Sorteio")]
    [Tooltip("Peso relativo desse tipo no sorteio do SpawnManager. Maior = spawna mais vezes. 1 = peso padrão.")]
    public float spawnWeight = 1f;

    // Dispara quando algum campo muda no Inspector, pra qualquer EnemyAI que esteja usando
    // este asset como "Preview Definition" recalcular as zonas na hora, sem precisar dar Play.
    public event System.Action Changed;

    void OnValidate()
    {
        Changed?.Invoke();
    }
}
