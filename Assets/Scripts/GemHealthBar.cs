using UnityEngine;
using UnityEngine.UI;
using MoreMountains.Tools;

// Liga um MMProgressBar (Feel) já configurado na cena à vida da GemObjective.
// Arraste aqui a barra (ex: copiada da cena demo Assets/Feel/FeelDemos/MMProgressBar).
public class GemHealthBar : MonoBehaviour
{
    public MMProgressBar progressBar;

    [Header("Cores por porcentagem de vida")]
    public Color fullHealthColor = Color.green;
    public Color midHealthColor = Color.yellow;
    public Color lowHealthColor = Color.red;
    [Range(0f, 1f)] public float midHealthThreshold = 0.5f;
    [Range(0f, 1f)] public float lowHealthThreshold = 0.25f;

    [Header("Billboard")]
    [Tooltip("O MMBillboard do Feel vira baseado na ROTAÇÃO da câmera, não na posição relativa - não acompanha bem um jogador VR andando. Esse aqui usa a mesma lógica já testada em EnemyHealthDisplay/DamageNumber.")]
    public bool billboardToCamera = true;

    private Image foregroundImage;
    private Transform cam;

    void Awake()
    {
        cam = Camera.main != null ? Camera.main.transform : null;
    }

    void LateUpdate()
    {
        if (billboardToCamera && cam != null && progressBar != null)
        {
            progressBar.transform.forward = (progressBar.transform.position - cam.position).normalized;
        }
    }

    public void UpdateHealth(int current, int max)
    {
        if (progressBar == null) return;

        if (foregroundImage == null && progressBar.ForegroundBar != null)
            foregroundImage = progressBar.ForegroundBar.GetComponent<Image>();

        float percent = max > 0 ? (float)current / max : 0f;
        Color targetColor = percent <= lowHealthThreshold ? lowHealthColor
            : percent <= midHealthThreshold ? midHealthColor
            : fullHealthColor;

        // seta a cor ANTES do UpdateBar: o MMProgressBar guarda essa cor como "cor base"
        // pro efeito de bump (ele pisca pra BumpColor e volta pra essa cor)
        if (foregroundImage != null)
            foregroundImage.color = targetColor;

        progressBar.UpdateBar(current, 0, max);
    }
}
