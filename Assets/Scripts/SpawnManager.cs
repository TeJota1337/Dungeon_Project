using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Um tipo de inimigo + quantos dessa wave são desse tipo. Uma WaveConfig soma várias dessas
// entradas ("6 do tipo1 + 4 do tipo2") e embaralha a ordem de spawn entre elas, pra não sair
// em blocos separados por tipo.
[System.Serializable]
public class WaveEnemyEntry
{
    public EnemyDefinition enemyType;
    public int count = 1;
}

// Uma wave: quais tipos (e quantos de cada), de quais spawn points, e o ritmo entre spawns
// dentro dela. Tudo pelo Inspector - arraste os EnemyDefinition e os spawn points direto aqui.
[System.Serializable]
public class WaveConfig
{
    public string waveName = "Wave";
    public WaveEnemyEntry[] enemies;
    [Tooltip("De quais pontos os inimigos desta wave podem nascer.")]
    public Transform[] spawnPoints;
    public float spawnInterval = 2f;
    [Tooltip("Variação (pra mais ou pra menos) aplicada sobre o Spawn Interval a cada spawn, pra não cair sempre no mesmo ritmo.")]
    public float spawnIntervalVariation = 0.5f;
}

public class SpawnManager : MonoBehaviour
{
    public static SpawnManager Instance { get; private set; }

    public GameObject enemyPrefab;

    [Tooltip("As waves da run, em ordem. A run termina em vitória ao completar a última (GDD 2, seção 2).")]
    public WaveConfig[] waves;

    [Tooltip("Pausa entre uma wave ficar limpa (sem inimigos) e a próxima começar. Futuramente é aqui que a loja do goblin abre (GDD 2, seção 9) - por enquanto é só um respiro fixo.")]
    public float timeBetweenWaves = 5f;

    // -1 = ainda não começou nenhuma wave. Exposto pra UI (ex: "Wave 3/18").
    public int CurrentWaveIndex { get; private set; } = -1;
    public int TotalWaves => waves != null ? waves.Length : 0;
    public bool HasFinishedAllWaves { get; private set; }

    // Contagem regressiva de quanto falta pra loja fechar/a próxima wave começar - exposto pra UI
    // (ex: ShopTimerDisplay). Só é != 0 durante a pausa entre waves.
    public float ShopTimeRemaining { get; private set; }

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
        StartCoroutine(RunWaves());
    }

    IEnumerator RunWaves()
    {
        for (int i = 0; i < waves.Length; i++)
        {
            CurrentWaveIndex = i;
            yield return StartCoroutine(RunWave(waves[i]));

            // espera o campo ficar vazio antes de considerar a wave "limpa" - só então
            // decide se acabou a run (última wave) ou libera a próxima
            yield return new WaitUntil(() => EnemyAI.ActiveCount == 0);

            bool isLastWave = i == waves.Length - 1;
            if (isLastWave)
            {
                HasFinishedAllWaves = true;
                GameStateManager.Instance?.TriggerVictory();
                yield break;
            }

            // loja abre no intervalo entre waves (GDD 2, seção 9) - upcomingWave é 1-based
            // (CurrentWaveIndex ainda está na wave que acabou de terminar). Log explícito se
            // não existir ShopManager na cena, pra não falhar em silêncio (Instance?. mascarava isso).
            if (ShopManager.Instance != null)
                ShopManager.Instance.Open(CurrentWaveIndex + 2);
            else
                Debug.LogWarning("SpawnManager: ShopManager.Instance é null - a loja não vai abrir. Confirme que existe um GameObject com o componente ShopManager na cena.");

            // contagem regressiva manual (em vez de um WaitForSeconds só) pra ShopTimeRemaining
            // dar pra UI mostrar o tempo restante ao vivo (ex: ShopTimerDisplay).
            ShopTimeRemaining = timeBetweenWaves;
            while (ShopTimeRemaining > 0f)
            {
                yield return null;
                ShopTimeRemaining -= Time.deltaTime;
            }
            ShopTimeRemaining = 0f;

            ShopManager.Instance?.Close();
        }
    }

    IEnumerator RunWave(WaveConfig wave)
    {
        List<EnemyDefinition> spawnList = BuildSpawnList(wave);

        foreach (EnemyDefinition definition in spawnList)
        {
            SpawnEnemy(wave, definition);

            float interval = Mathf.Max(0.1f, wave.spawnInterval + Random.Range(-wave.spawnIntervalVariation, wave.spawnIntervalVariation));
            yield return new WaitForSeconds(interval);
        }
    }

    // Achata "N entradas de (tipo, quantidade)" numa lista só e embaralha - assim a wave spawna
    // os tipos misturados entre si, em vez de um bloco inteiro de cada tipo em sequência.
    List<EnemyDefinition> BuildSpawnList(WaveConfig wave)
    {
        var list = new List<EnemyDefinition>();

        if (wave.enemies != null)
        {
            foreach (var entry in wave.enemies)
            {
                for (int i = 0; i < entry.count; i++)
                    list.Add(entry.enemyType);
            }
        }

        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }

        return list;
    }

    // Chamado pelo GameStateManager ao terminar o jogo. Setar enabled=false NÃO para uma
    // coroutine já rodando (é um gotcha do Unity) - por isso o StopAllCoroutines() explícito.
    public void StopSpawning()
    {
        StopAllCoroutines();
    }

    void SpawnEnemy(WaveConfig wave, EnemyDefinition definition)
    {
        if (wave.spawnPoints == null || wave.spawnPoints.Length == 0)
        {
            Debug.LogWarning($"SpawnManager: a wave '{wave.waveName}' não tem nenhum Spawn Point configurado - pulei esse spawn.");
            return;
        }

        Transform point = wave.spawnPoints[Random.Range(0, wave.spawnPoints.Length)];
        GameObject enemy = ObjectPoolManager.Instance.Get(enemyPrefab, point.position, point.rotation);

        EnemyAI ai = enemy.GetComponent<EnemyAI>();
        ai.Init(point, definition); // point = spawn de origem, pra onde o esqueleto volta depois de roubar
    }
}
