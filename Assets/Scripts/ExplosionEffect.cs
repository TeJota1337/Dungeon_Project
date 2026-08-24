using UnityEngine;

public class ExplosionEffect : MonoBehaviour, IPoolable
{
    private ParticleSystem[] allSystems;
    private float maxDuration;
    private PooledObject pooledObject;

    void Awake()
    {
        // calcula a dura��o total baseada no maior Particle System filho,
        // pra devolver o objeto ao pool s� depois que tudo terminou de tocar
        allSystems = GetComponentsInChildren<ParticleSystem>();

        foreach (var ps in allSystems)
        {
            float total = ps.main.duration + ps.main.startLifetime.constantMax;
            if (total > maxDuration) maxDuration = total;
        }
    }

    public void OnSpawnFromPool()
    {
        foreach (var ps in allSystems)
        {
            ps.Clear(false);
            ps.Play(false);
        }

        CancelInvoke(nameof(ReturnToPool));
        Invoke(nameof(ReturnToPool), maxDuration + 0.2f); // margem de seguran�a
    }

    public void OnReturnToPool()
    {
        CancelInvoke(nameof(ReturnToPool));
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
