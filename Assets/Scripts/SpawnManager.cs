using System.Collections;
using UnityEngine;

public class SpawnManager : MonoBehaviour
{
    public static SpawnManager Instance { get; private set; }

    public GameObject enemyPrefab;
    [Tooltip("Tipos de inimigo sorteados a cada spawn (peso relativo em EnemyDefinition.spawnWeight). Deixe vazio pra usar os valores padrão do prefab.")]
    public EnemyDefinition[] enemyDefinitions;
    public Transform[] spawnPoints;
    public Transform objective;
    public float spawnInterval = 2f;
    [Tooltip("Variação (pra mais ou pra menos) aplicada sobre o Spawn Interval a cada spawn, pra não cair sempre no mesmo ritmo.")]
    public float spawnIntervalVariation = 0.5f;
    public float gameDuration = 120f;

    private float startTime;

    // ---------- TIMER: funções pra UI (world space canvas fica por sua conta) ----------

    public float TimeElapsed => Time.time - startTime;
    public float TimeRemaining => Mathf.Max(0f, gameDuration - TimeElapsed);
    public bool HasFinishedSpawning => TimeElapsed >= gameDuration;

    public string GetFormattedTimeRemaining()
    {
        float remaining = TimeRemaining;
        int minutes = Mathf.FloorToInt(remaining / 60f);
        int seconds = Mathf.FloorToInt(remaining % 60f);
        return $"{minutes:00}:{seconds:00}";
    }

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        EnemyAI.ActiveCount = 0; // garante estado limpo se a cena recarregou (restart)
    }

    // Chamado pelo StartMenuUI quando o jogador clica "Iniciar" - não começa mais sozinho no Start().
    public void BeginGame()
    {
        startTime = Time.time;
        StartCoroutine(SpawnRoutine());
    }

    IEnumerator SpawnRoutine()
    {
        while (TimeElapsed < gameDuration)
        {
            SpawnEnemy();

            float interval = Mathf.Max(0.1f, spawnInterval + Random.Range(-spawnIntervalVariation, spawnIntervalVariation));
            yield return new WaitForSeconds(interval);
        }

        // n�o dispara vit�ria direto: spawns acabaram, mas os inimigos que ainda
        // est�o na cena precisam ser derrotados primeiro
        if (GameStateManager.Instance != null)
            GameStateManager.Instance.CheckVictoryCondition();
    }

    // Chamado pelo GameStateManager ao terminar o jogo. Setar enabled=false NÃO para uma
    // coroutine já rodando (é um gotcha do Unity) - por isso o StopAllCoroutines() explícito.
    public void StopSpawning()
    {
        StopAllCoroutines();
    }

    void SpawnEnemy()
    {
        Transform point = spawnPoints[Random.Range(0, spawnPoints.Length)];
        GameObject enemy = ObjectPoolManager.Instance.Get(enemyPrefab, point.position, point.rotation);

        EnemyAI ai = enemy.GetComponent<EnemyAI>();
        ai.Init(objective, PickDefinition());
    }

    // Sorteio ponderado por EnemyDefinition.spawnWeight (peso 1 = padrão; maior spawna mais).
    EnemyDefinition PickDefinition()
    {
        if (enemyDefinitions == null || enemyDefinitions.Length == 0) return null;

        float totalWeight = 0f;
        foreach (var def in enemyDefinitions)
            totalWeight += Mathf.Max(0f, def.spawnWeight);

        if (totalWeight <= 0f) return enemyDefinitions[Random.Range(0, enemyDefinitions.Length)];

        float roll = Random.Range(0f, totalWeight);
        float cumulative = 0f;

        foreach (var def in enemyDefinitions)
        {
            cumulative += Mathf.Max(0f, def.spawnWeight);
            if (roll <= cumulative) return def;
        }

        return enemyDefinitions[enemyDefinitions.Length - 1];
    }
}