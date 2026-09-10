using System.Collections;
using UnityEngine;
using MoreMountains.Tools;

// Coordena o fim de jogo (vitória/derrota): congela o jogo, funde a tela em preto, teleporta o
// jogador de volta pro spawn inicial (a essa altura o GameOverCanvas já está posicionado por lá,
// desligado) e só então liga o painel e funde de volta - assim ele nunca aparece torto na cara
// do jogador, não importa pra onde ele estava olhando quando o jogo acabou.
public class GameStateManager : MonoBehaviour
{
    public static GameStateManager Instance { get; private set; }

    // Setado por GameOverUI.Restart() antes de recarregar a cena - avisa o StartMenuUI pra
    // pular a tela inicial e ir direto pro jogo (ReturnToMenu() não mexe nisso, então volta a mostrar o menu).
    public static bool SkipStartMenu;

    [Header("Referências")]
    public SpawnManager spawnManager;
    public GameOverUI gameOverUI;
    public SlingshotController slingshotController;

    [Header("Retorno ao spawn (fade + teleporte)")]
    [Tooltip("A raiz do rig do jogador (o objeto que tem o CharacterController) - é ela que é movida de volta.")]
    public Transform player;
    [Tooltip("Onde o jogador deve reaparecer ao fim de jogo - normalmente perto do GameOverCanvas.")]
    public Transform returnSpawnPoint;
    public float fadeDuration = 0.5f;

    private bool gameEnded;

    // Estatísticas da run atual, só usadas pra exibir/salvar em caso de vitória.
    public int EnemiesDefeated { get; private set; }
    public int TotalDamageDealt { get; private set; }

    void Awake()
    {
        Instance = this;
    }

    public void RegisterDamage(int amount)
    {
        TotalDamageDealt += amount;
    }

    public void RegisterEnemyDefeated()
    {
        EnemiesDefeated++;
    }

    public void TriggerDefeat()
    {
        GameAudio.Instance?.PlayDefeatSting();
        EndGame(false, "Defeat!\nThe dungeon's gold was stolen.");
    }

    public void TriggerVictory()
    {
        GameAudio.Instance?.PlayVictorySting();
        EndGame(true, "Victory!\nThe dungeon's gold is safe.");
    }

    void EndGame(bool victory, string message)
    {
        if (gameEnded) return;
        gameEnded = true;

        if (spawnManager != null)
            spawnManager.StopSpawning();

        if (slingshotController != null)
            slingshotController.enabled = false;

        // congela a gameplay já - o fade/teleporte roda em tempo real (WaitForSecondsRealtime),
        // então continua animando normalmente mesmo com o jogo pausado.
        Time.timeScale = 0f;

        StartCoroutine(EndGameSequence(victory, message));
    }

    IEnumerator EndGameSequence(bool victory, string message)
    {
        MMFadeEvent.Trigger(fadeDuration, 1f);
        yield return new WaitForSecondsRealtime(fadeDuration);

        ReturnPlayerToSpawn();

        if (gameOverUI != null)
        {
            if (victory)
            {
                // pontos = ouro que sobrou na dungeon no momento da vitória (GDD 2, pendência #13)
                int points = DungeonGoldManager.Instance != null ? DungeonGoldManager.Instance.TotalGold : 0;
                gameOverUI.ShowVictory(message, points, EnemiesDefeated, TotalDamageDealt);
            }
            else
            {
                gameOverUI.ShowDefeat(message);
            }
        }

        MMFadeEvent.Trigger(fadeDuration, 0f);
    }

    void ReturnPlayerToSpawn()
    {
        if (player == null || returnSpawnPoint == null) return;

        // com o CharacterController ligado, mexer no transform direto é ignorado/gera colisão
        // estranha - desliga, teleporta, religa.
        CharacterController controller = player.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;

        player.SetPositionAndRotation(returnSpawnPoint.position, returnSpawnPoint.rotation);

        if (controller != null) controller.enabled = true;
    }
}
