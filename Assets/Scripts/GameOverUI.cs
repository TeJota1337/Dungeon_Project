using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

// Painel de fim de jogo pra VR: mostra/esconde o Canvas + texto que você já
// montou na cena, opcionalmente reposicionando na frente da câmera, e reinicia
// quando o botão "Restart" chama Restart() pelo OnClick().
public class GameOverUI : MonoBehaviour
{
    [Header("Referências (arraste da sua hierarquia)")]
    [Tooltip("O objeto raiz a ativar/desativar (geralmente o próprio Canvas).")]
    public GameObject panelRoot;
    [Tooltip("O texto (TMP) que mostra a mensagem de vitória/derrota.")]
    public TextMeshProUGUI messageText;

    [Header("Posicionamento em VR")]
    public bool positionInFrontOfCamera = true;
    public float distanceFromCamera = 2f;

    private Transform cam;

    void Awake()
    {
        cam = Camera.main != null ? Camera.main.transform : null;

        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    public void Show(string message)
    {
        if (messageText != null)
            messageText.text = message;

        if (panelRoot != null)
            panelRoot.SetActive(true);

        PositionInFrontOfCamera();
    }

    void PositionInFrontOfCamera()
    {
        if (!positionInFrontOfCamera || cam == null || panelRoot == null) return;

        panelRoot.transform.position = cam.position + cam.forward * distanceFromCamera;
        // mesma convenção de billboard já usada em EnemyHealthDisplay/DamageNumber
        panelRoot.transform.rotation = Quaternion.LookRotation(panelRoot.transform.position - cam.position);
    }

    void Update()
    {
        if (positionInFrontOfCamera && cam != null && panelRoot != null && panelRoot.activeSelf)
        {
            panelRoot.transform.rotation = Quaternion.LookRotation(panelRoot.transform.position - cam.position);
        }
    }

    // Chamado pelo botão "Restart" via OnClick() no Inspector
    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
