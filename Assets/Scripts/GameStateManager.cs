using UnityEngine;

// Coordena o fim de jogo (vitória/derrota): congela o jogo e mostra a tela de restart.
public class GameStateManager : MonoBehaviour
{
    public static GameStateManager Instance { get; private set; }

    [Header("Referências")]
    public SpawnManager spawnManager;
    public GameOverUI gameOverUI;
    public SlingshotController slingshotController;

    private bool gameEnded;

    void Awake()
    {
        Instance = this;
    }

    public void TriggerDefeat()
    {
        GameAudio.Instance?.PlayDefeatSting();
        EndGame("Derrota!\nA pedra foi destruída.");
    }

    public void TriggerVictory()
    {
        GameAudio.Instance?.PlayVictorySting();
        EndGame("Vitória!\nVocê protegeu a pedra.");
    }

    // Chamado quando o SpawnManager termina as waves E sempre que um inimigo é
    // devolvido ao pool: vitória só conta quando as duas condições forem verdadeiras.
    public void CheckVictoryCondition()
    {
        if (gameEnded) return;
        if (spawnManager == null || !spawnManager.HasFinishedSpawning) return;
        if (EnemyAI.ActiveCount > 0) return;

        TriggerVictory();
    }

    void EndGame(string message)
    {
        if (gameEnded) return;
        gameEnded = true;

        if (spawnManager != null)
            spawnManager.StopSpawning();

        if (slingshotController != null)
            slingshotController.enabled = false;

        Time.timeScale = 0f;

        if (gameOverUI != null)
            gameOverUI.Show(message);
    }
}
