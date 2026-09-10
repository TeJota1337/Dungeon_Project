using UnityEngine;

// Liga/desliga as partículas de fogo da tocha de um portal, como identificação visual de que
// aquele portal está ativo (sendo usado como Spawn Point) na wave atual.
public class PortalTorch : MonoBehaviour
{
    [Tooltip("O Spawn Point (portal) que esta tocha representa - arraste aqui o MESMO Transform usado em Spawn Points na wave, no SpawnManager.")]
    public Transform portalSpawnPoint;

    [Tooltip("Sistemas de partícula da chama que ligam/desligam com o portal (ex: fogo, fumaça, faíscas).")]
    public ParticleSystem[] flameVfx;

    [Tooltip("Se true, a tocha já começa apagada até a primeira wave ativar ela.")]
    public bool startExtinguished = true;

    void OnEnable()
    {
        SpawnManager.OnWaveStarted += HandleWaveStarted;
        SpawnManager.OnWaveEnded += HandleWaveEnded;

        if (startExtinguished)
            SetLit(false);
    }

    void OnDisable()
    {
        SpawnManager.OnWaveStarted -= HandleWaveStarted;
        SpawnManager.OnWaveEnded -= HandleWaveEnded;
    }

    void HandleWaveStarted(WaveConfig wave)
    {
        bool isActive = portalSpawnPoint != null && wave.spawnPoints != null
            && System.Array.IndexOf(wave.spawnPoints, portalSpawnPoint) >= 0;
        SetLit(isActive);
    }

    void HandleWaveEnded()
    {
        SetLit(false);
    }

    void SetLit(bool lit)
    {
        if (flameVfx == null) return;

        foreach (var vfx in flameVfx)
        {
            if (vfx == null) continue;

            if (lit) vfx.Play();
            else vfx.Stop(); // por padrão deixa as partículas já emitidas terminarem sozinhas, em vez de sumir de repente
        }
    }
}
