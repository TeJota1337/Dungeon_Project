using UnityEngine;
using TMPro;

public class EnemyHealthDisplay : MonoBehaviour
{
    [Header("Refer�ncias")]
    public Transform canvasRoot;      // o objeto HealthCanvas inteiro (pra habilitar/desabilitar)
    public TextMeshProUGUI healthText;

    [Header("Comportamento")]
    public bool hiddenUntilFirstHit = false; // s� aparece depois do primeiro dano
    public bool billboardToCamera = true;

    [Header("Cores por porcentagem de vida")]
    public Color fullHealthColor = Color.white;
    public Color midHealthColor = Color.yellow;
    public Color lowHealthColor = Color.red;
    [Range(0f, 1f)] public float midHealthThreshold = 0.5f;
    [Range(0f, 1f)] public float lowHealthThreshold = 0.25f;

    private Transform cam;
    private int maxHealth;
    private bool hasBeenHit;

    void Awake()
    {
        cam = Camera.main != null ? Camera.main.transform : null;

        if (canvasRoot != null && hiddenUntilFirstHit)
            canvasRoot.gameObject.SetActive(false);
    }

    public void Initialize(int startingHealth)
    {
        maxHealth = startingHealth;
        hasBeenHit = false;

        // objeto pode estar sendo reaproveitado do pool: reaplica o estado "escondido at� o primeiro hit"
        if (canvasRoot != null && hiddenUntilFirstHit)
            canvasRoot.gameObject.SetActive(false);

        // Chama apenas a atualiza��o visual, sem registrar como se fosse um dano sofrido
        UpdateVisualsOnly(startingHealth);
    }

    public void UpdateDisplay(int currentHealth)
    {
        if (!hasBeenHit && canvasRoot != null)
        {
            hasBeenHit = true;
            canvasRoot.gameObject.SetActive(true);
        }

        UpdateVisualsOnly(currentHealth);
    }

    // Novo m�todo que isola apenas a l�gica de texto e mudan�a de cores
    private void UpdateVisualsOnly(int currentHealth)
    {
        if (healthText == null) return;

        healthText.text = currentHealth.ToString();

        float percent = maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;

        if (percent <= lowHealthThreshold)
            healthText.color = lowHealthColor;
        else if (percent <= midHealthThreshold)
            healthText.color = midHealthColor;
        else
            healthText.color = fullHealthColor;
    }

    void LateUpdate()
    {
        if (billboardToCamera && cam != null && canvasRoot != null && canvasRoot.gameObject.activeSelf)
        {
            canvasRoot.forward = (canvasRoot.position - cam.position).normalized;
        }
    }
}