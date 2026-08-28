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
    [Tooltip("Escala do modelo E das zonas de dano — mantém o hitbox alinhado com o tamanho do inimigo.")]
    public float visualScale = 0.7f;

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
}
