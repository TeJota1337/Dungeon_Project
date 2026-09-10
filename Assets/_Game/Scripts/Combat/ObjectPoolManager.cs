using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

// Coloque este componente em um GameObject vazio na cena (ex: "ObjectPoolManager").
// Reaproveita instâncias em vez de Instantiate/Destroy pra evitar picos de GC em VR.
public class ObjectPoolManager : MonoBehaviour
{
    public static ObjectPoolManager Instance { get; private set; }

    [System.Serializable]
    public class PoolConfig
    {
        public GameObject prefab;
        public int defaultCapacity = 20;
        public int maxSize = 200;
    }

    [Header("Pools pré-aquecidos (opcional)")]
    [Tooltip("Prefabs listados aqui já nascem com capacidade reservada. Qualquer outro prefab usado via Get() cria um pool automaticamente na primeira vez, sem precisar estar nessa lista.")]
    public PoolConfig[] pools;

    private readonly Dictionary<GameObject, Pool> poolsByPrefab = new Dictionary<GameObject, Pool>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (pools != null)
        {
            foreach (var config in pools)
            {
                if (config.prefab != null)
                    poolsByPrefab[config.prefab] = new Pool(config);
            }
        }
    }

    public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (!poolsByPrefab.TryGetValue(prefab, out Pool pool))
        {
            pool = new Pool(new PoolConfig { prefab = prefab, defaultCapacity = 10, maxSize = 200 });
            poolsByPrefab[prefab] = pool;
        }

        return pool.Get(position, rotation);
    }

    public class Pool
    {
        private readonly ObjectPool<GameObject> pool;
        private readonly GameObject prefab;

        public Pool(PoolConfig config)
        {
            prefab = config.prefab;
            pool = new ObjectPool<GameObject>(
                createFunc: CreateInstance,
                actionOnDestroy: obj => Object.Destroy(obj),
                collectionCheck: true,
                defaultCapacity: config.defaultCapacity,
                maxSize: config.maxSize
            );
        }

        private GameObject CreateInstance()
        {
            GameObject obj = Object.Instantiate(prefab);

            PooledObject pooled = obj.GetComponent<PooledObject>();
            if (pooled == null)
                pooled = obj.AddComponent<PooledObject>();
            pooled.SetPool(this);

            obj.SetActive(false);
            return obj;
        }

        public GameObject Get(Vector3 position, Quaternion rotation)
        {
            GameObject obj = pool.Get();

            // posiciona ANTES de ativar: evita que um NavMeshAgent (ou qualquer outro
            // componente sensível a OnEnable) reaja à posição antiga deixada pelo uso anterior
            obj.transform.SetPositionAndRotation(position, rotation);
            obj.SetActive(true);

            var poolables = obj.GetComponentsInChildren<IPoolable>(true);
            for (int i = 0; i < poolables.Length; i++)
                poolables[i].OnSpawnFromPool();

            return obj;
        }

        public void Release(GameObject obj)
        {
            var poolables = obj.GetComponentsInChildren<IPoolable>(true);
            for (int i = 0; i < poolables.Length; i++)
                poolables[i].OnReturnToPool();

            obj.SetActive(false);
            pool.Release(obj);
        }
    }
}
