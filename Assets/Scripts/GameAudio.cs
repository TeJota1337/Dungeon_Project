using UnityEngine;
using MoreMountains.Tools;

// Ponto central pra tocar os sons do jogo via MMSoundManager (Feel). Cada evento
// de gameplay chama um método aqui (mesmo padrão de PlayerHaptics/DamageNumberManager),
// e os clipes de áudio são arrastados uma vez só, aqui.
public class GameAudio : MonoBehaviour
{
    public static GameAudio Instance { get; private set; }

    [Header("Estilingue")]
    public AudioClip slingshotLaunch;
    public AudioClip slingshotBreak;

    [Header("Bomba")]
    public AudioClip bombExplosion;
    public AudioClip bombGiantExplosion;

    [Header("Inimigos")]
    public AudioClip enemyHit;
    public AudioClip enemyDeath;
    public AudioClip enemyReachedObjective;

    [Header("Pedra")]
    public AudioClip gemDamage;

    [Header("Fim de jogo (não-espacial: toca igual não importa pra onde você olha)")]
    public AudioClip victorySting;
    public AudioClip defeatSting;

    [Range(0f, 1f)]
    public float sfxVolume = 1f;

    void Awake()
    {
        Instance = this;
    }

    void PlayWorld(AudioClip clip, Vector3 position)
    {
        if (clip == null) return;

        MMSoundManager.Instance.PlaySound(clip, MMSoundManager.MMSoundManagerTracks.Sfx, position,
            volume: sfxVolume, spatialBlend: 1f);
    }

    void PlayUI(AudioClip clip)
    {
        if (clip == null) return;

        MMSoundManager.Instance.PlaySound(clip, MMSoundManager.MMSoundManagerTracks.UI, Vector3.zero,
            volume: sfxVolume, spatialBlend: 0f);
    }

    public void PlaySlingshotLaunch(Vector3 pos) => PlayWorld(slingshotLaunch, pos);
    public void PlaySlingshotBreak(Vector3 pos) => PlayWorld(slingshotBreak, pos);
    public void PlayBombExplosion(Vector3 pos, bool giant) => PlayWorld(giant ? bombGiantExplosion : bombExplosion, pos);
    public void PlayEnemyHit(Vector3 pos) => PlayWorld(enemyHit, pos);
    public void PlayEnemyDeath(Vector3 pos) => PlayWorld(enemyDeath, pos);
    public void PlayEnemyReachedObjective(Vector3 pos) => PlayWorld(enemyReachedObjective, pos);
    public void PlayGemDamage(Vector3 pos) => PlayWorld(gemDamage, pos);
    public void PlayVictorySting() => PlayUI(victorySting);
    public void PlayDefeatSting() => PlayUI(defeatSting);
}
