using System.Collections;
using UnityEngine;

public class GemObjective : MonoBehaviour
{
    public static GemObjective Instance { get; private set; }

    [Header("Vida")]
    // Cálculo do valor padrão: com spawnInterval~2s e gameDuration=120s, o SpawnManager
    // spawna ~60 inimigos (120/2). A vida média de um inimigo é (minHealth+maxHealth)/2 = 10,
    // então o dano total teórico máximo (se NENHUM inimigo fosse tocado) seria ~600.
    // 150 representa a pedra aguentando ~25% desse dano máximo antes de estourar -
    // ou seja, o jogador precisa neutralizar a maior parte do dano pra sobreviver.
    // É só um ponto de partida: ajuste em playtest conforme a sensação de dificuldade.
    public int maxHealth = 150;

    [Header("Feedback de dano")]
    public Color hitFlashColor = Color.red;
    public float hitFlashDuration = 0.15f;

    [Header("Barra de vida (Feel)")]
    public GemHealthBar healthBar;

    private int currentHealth;
    private Renderer[] renderers;
    private Color[] originalColors;
    private Coroutine flashRoutine;

    void Awake()
    {
        Instance = this;
        currentHealth = maxHealth;

        renderers = GetComponentsInChildren<Renderer>();
        originalColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            originalColors[i] = renderers[i].material.color;

        healthBar?.UpdateHealth(currentHealth, maxHealth);
    }

    public void TakeDamage(int amount)
    {
        if (currentHealth <= 0) return; // já destruída, ignora dano tardio

        currentHealth = Mathf.Max(0, currentHealth - amount);
        healthBar?.UpdateHealth(currentHealth, maxHealth);

        if (DamageNumberManager.Instance != null)
            DamageNumberManager.Instance.Spawn(gameObject, transform.position, amount, false, Color.white);

        GameAudio.Instance?.PlayGemDamage(transform.position);

        FlashRed();

        if (currentHealth <= 0 && GameStateManager.Instance != null)
        {
            GameStateManager.Instance.TriggerDefeat();
        }
    }

    void FlashRed()
    {
        if (flashRoutine != null)
            StopCoroutine(flashRoutine);

        flashRoutine = StartCoroutine(FlashRoutine());
    }

    IEnumerator FlashRoutine()
    {
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].material.color = hitFlashColor;

        yield return new WaitForSeconds(hitFlashDuration);

        for (int i = 0; i < renderers.Length; i++)
            renderers[i].material.color = originalColors[i];
    }
}
