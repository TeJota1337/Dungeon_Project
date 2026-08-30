using UnityEngine;
using TMPro;

// Mostra o FPS atual num texto TMP, atualizado a cada updateInterval segundos
// (em vez de todo frame) pra o número não tremer/piscar rápido demais pra ler.
// A cor do texto reflete o quão perto o FPS está da meta (targetFps).
public class FpsCounterDisplay : MonoBehaviour
{
    public TextMeshProUGUI fpsLabel;
    public float updateInterval = 0.5f;

    [Header("Meta de performance")]
    public float targetFps = 72f;

    [Header("Cores por qualidade do FPS")]
    public Color goodColor = Color.green;
    public Color midColor = Color.yellow;
    public Color badColor = Color.red;
    [Tooltip("% da meta a partir da qual o FPS ainda é considerado bom.")]
    [Range(0f, 1f)] public float midThreshold = 0.9f;
    [Tooltip("% da meta abaixo da qual o FPS já é considerado ruim.")]
    [Range(0f, 1f)] public float badThreshold = 0.6f;

    private float timer;
    private int frames;

    void Update()
    {
        frames++;
        timer += Time.unscaledDeltaTime;

        if (timer >= updateInterval)
        {
            float fps = frames / timer;
            UpdateDisplay(fps);

            timer = 0f;
            frames = 0;
        }
    }

    void UpdateDisplay(float fps)
    {
        if (fpsLabel == null) return;

        fpsLabel.text = Mathf.RoundToInt(fps).ToString();
        fpsLabel.color = EvaluateColor(fps);
    }

    Color EvaluateColor(float fps)
    {
        float ratio = targetFps > 0f ? fps / targetFps : 1f;

        if (ratio >= midThreshold) return goodColor;
        if (ratio >= badThreshold) return midColor;
        return badColor;
    }
}
