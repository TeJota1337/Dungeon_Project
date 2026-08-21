using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnManager : MonoBehaviour
{
    public GameObject enemyPrefab;
    public Transform[] spawnPoints;
    public Transform objective;
    public float spawnInterval = 2f;
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
            yield return new WaitForSeconds(spawnInterval);
            timer += spawnInterval;
        }
    }

    void SpawnEnemy()
    {
        Transform point = spawnPoints[Random.Range(0, spawnPoints.Length)];
        GameObject enemy = Instantiate(enemyPrefab, point.position, point.rotation);

        EnemyAI ai = enemy.GetComponent<EnemyAI>();
        ai.objective = objective;
    }
}