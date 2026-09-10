using UnityEngine;

public class PooledObject : MonoBehaviour
{
    private ObjectPoolManager.Pool sourcePool;

    public void SetPool(ObjectPoolManager.Pool pool)
    {
        sourcePool = pool;
    }

    public void ReturnToPool()
    {
        if (sourcePool != null)
            sourcePool.Release(gameObject);
        else
            Destroy(gameObject); // não veio de um pool (ex: objeto colocado direto na cena)
    }
}
