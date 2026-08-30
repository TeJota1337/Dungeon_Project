using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

// Painel de fim de jogo pra VR. Fica fixo no mundo (posicionado por você na cena, desligado) -
// esse script só liga/desliga, nunca reposiciona ou vira de frente pra câmera (isso deixava o
// painel torto quando o jogador não estava olhando pro lugar certo no momento do fim de jogo).
//
// Na derrota, mostra só a mensagem + botão de restart. Na vitória, além disso libera o campo de
// nome pro jogador entrar no ranking; o CanvasScore (placar) fica fixo no mundo o jogo inteiro
// por conta própria (ver LeaderboardUI) - aqui só mandamos ele se atualizar depois que o nome é
// confirmado. A run só entra no ranking (PlayerPrefs, via Leaderboard) quando o jogador confirma
// o nome pelo teclado virtual (evento Confirmed do VirtualKeyboard, não o onEndEdit nativo do
// TMP_InputField - esse dispara sozinho toda vez que o campo perde o foco, o que acontecia a
// cada clique numa tecla do teclado virtual e salvava o nome pela metade).
public class GameOverUI : MonoBehaviour
{
    [Header("Painel comum (mensagem + botão Restart)")]
    [Tooltip("O objeto raiz a ativar/desativar (o GameOverCanvas, já posicionado por você no mundo).")]
    public GameObject panelRoot;
    public TextMeshProUGUI messageText;

    [Header("Nome do jogador (só aparece na vitória)")]
    public TMP_InputField nameInputField;
    [Tooltip("O teclado virtual da cena - abre sozinho quando o campo de nome é selecionado.")]
    public VirtualKeyboard virtualKeyboard;

    [Header("Ranking (placar fixo no mundo - só recebe o refresh daqui)")]
    public LeaderboardUI leaderboardUI;

    private bool isVictory;
    private int pendingPoints, pendingEnemiesDefeated, pendingDamageDealt;

    void Awake()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);

        if (nameInputField != null)
        {
            nameInputField.gameObject.SetActive(false);
            nameInputField.onSelect.AddListener(OnNameFieldSelected);
        }

        if (virtualKeyboard != null)
            virtualKeyboard.Confirmed += OnNameConfirmed;
    }

    // Chamado quando o jogador seleciona o campo (clique/raycast) - abre o teclado virtual
    // em vez de deixar o TMP_InputField tentar abrir o teclado nativo do Android sozinho.
    void OnNameFieldSelected(string currentText)
    {
        virtualKeyboard?.Open(nameInputField);
    }

    public void ShowDefeat(string message)
    {
        isVictory = false;

        if (messageText != null)
            messageText.text = message;

        if (panelRoot != null)
            panelRoot.SetActive(true);

        if (nameInputField != null)
            nameInputField.gameObject.SetActive(false);

        virtualKeyboard?.Close();
    }

    public void ShowVictory(string message, int points, int enemiesDefeated, int damageDealt)
    {
        isVictory = true;
        pendingPoints = points;
        pendingEnemiesDefeated = enemiesDefeated;
        pendingDamageDealt = damageDealt;

        if (messageText != null)
            messageText.text = message;

        if (panelRoot != null)
            panelRoot.SetActive(true);

        if (nameInputField != null)
        {
            nameInputField.text = string.Empty;
            nameInputField.gameObject.SetActive(true);
        }
    }

    // Chamado pelo VirtualKeyboard quando o botão "Confirmar" é clicado (não pelo onEndEdit
    // nativo do InputField) - só salva/entra no ranking na vitória.
    void OnNameConfirmed(string typedName)
    {
        if (!isVictory || string.IsNullOrWhiteSpace(typedName)) return;

        var updatedRanking = Leaderboard.AddEntry(typedName.Trim(), pendingPoints, pendingEnemiesDefeated, pendingDamageDealt);
        leaderboardUI?.Refresh(updatedRanking);
    }

    // Chamado pelo botão "Restart" via OnClick() no Inspector
    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
