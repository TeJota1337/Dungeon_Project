using System.Collections;
using UnityEngine;

public class SpawnManager : MonoBehaviour
{
    public GameObject enemyPrefab;
    public Transform[] spawnPoints;
    public Transform objective;
    public float spawnInterval = 2f;
    [Tooltip("Variação (pra mais ou pra menos) aplicada sobre o Spawn Interval a cada spawn, pra não cair sempre no mesmo ritmo.")]
    public float spawnIntervalVariation = 0.5f;
    public float gameDuration = 120f;

    private float timer;

    void Start()
    {
        StartCoroutine(SpawnRoutine());
    }

    IEnumerator SpawnRoutine()
    {
        while (timer < gameDuration)
        {
            SpawnEnemy();

            float interval = Mathf.Max(0.1f, spawnInterval + Random.Range(-spawnIntervalVariation, spawnIntervalVariation));
            yield return new WaitForSeconds(interval);
            timer += interval;
        }

        if (GameStateManager.Instance != null)
            GameStateManager.Instance.TriggerVictory();
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
        ai.Init(objective);
    }
}