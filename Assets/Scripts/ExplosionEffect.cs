using UnityEngine;

public class ExplosionEffect : MonoBehaviour
{
    void Start()
    {
        // calcula a duração total baseada no maior Particle System filho,
        // pra destruir o objeto só depois que tudo terminou de tocar
        ParticleSystem[] allSystems = GetComponentsInChildren<ParticleSystem>();
        float maxDuration = 0f;

        foreach (var ps in allSystems)
        {
            float total = ps.main.duration + ps.main.startLifetime.constantMax;
            if (total > maxDuration) maxDuration = total;
        }

        Destroy(gameObject, maxDuration + 0.2f); // margem de segurança
    }
}