using UnityEngine;

// Tela inicial: fica visível até o jogador clicar "Iniciar" (StartGame(), via OnClick), que
// esconde essa tela e libera o SpawnManager pra começar as waves - ele não começa mais sozinho.
// Se GameStateManager.SkipStartMenu estiver true (setado pelo GameOverUI.Restart()), essa tela
// nem aparece - pula direto pro jogo, sem passar pelo menu de novo.
public class StartMenuUI : MonoBehaviour
{
    public GameObject panelRoot;

    void Start()
    {
        if (GameStateManager.SkipStartMenu)
        {
            GameStateManager.SkipStartMenu = false;
            StartGame();
            return;
        }

        if (panelRoot != null)
            panelRoot.SetActive(true);
    }

    // Chamado pelo botão "Iniciar" via OnClick() no Inspector
    public void StartGame()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);

        SpawnManager.Instance?.BeginGame();
    }
}
