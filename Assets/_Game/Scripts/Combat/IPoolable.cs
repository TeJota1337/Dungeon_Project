// Implementado por qualquer componente que precise resetar seu próprio estado
// quando o GameObject é reaproveitado por um ObjectPoolManager, já que Awake()/Start()
// só rodam uma vez na vida do objeto e não disparam de novo a cada reuso do pool.
public interface IPoolable
{
    void OnSpawnFromPool();
    void OnReturnToPool();
}
