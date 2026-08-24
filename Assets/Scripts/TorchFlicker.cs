using UnityEngine;

public class TorchFlicker : MonoBehaviour
{
    [Header("Intensidade")]
    public float baseIntensity = 4f;
    public float intensityVariation = 1.5f; // o quanto pode oscilar pra cima/baixo
    public float flickerSpeed = 8f; // velocidade da oscilação (Perlin Noise)

    [Header("Range (opcional, sutil)")]
    public bool varyRange = true;
    public float baseRange = 3.5f;
    public float rangeVariation = 0.3f;

    [Header("Cor (opcional)")]
    public bool varyColor = false;
    public Color colorA = new Color(1f, 0.55f, 0.15f); // laranja
    public Color colorB = new Color(1f, 0.75f, 0.3f);  // amarelo mais claro

    private Light torchLight;
    private float noiseOffset;

    void Awake()
    {
        torchLight = GetComponent<Light>();
        noiseOffset = Random.Range(0f, 100f); // cada tocha tremula de forma diferente
    }

    void Update()
    {
        float noise = Mathf.PerlinNoise(Time.time * flickerSpeed, noiseOffset);
        // PerlinNoise retorna 0 a 1; centralizamos em torno de 0 pra oscilar pra cima/baixo
        float offset = (noise - 0.5f) * 2f;

        torchLight.intensity = baseIntensity + offset * intensityVariation;

        if (varyRange)
        {
            float rangeNoise = Mathf.PerlinNoise(Time.time * flickerSpeed * 0.7f, noiseOffset + 50f);
            torchLight.range = baseRange + (rangeNoise - 0.5f) * 2f * rangeVariation;
        }

        if (varyColor)
        {
            float colorNoise = Mathf.PerlinNoise(Time.time * flickerSpeed * 0.5f, noiseOffset + 100f);
            torchLight.color = Color.Lerp(colorA, colorB, colorNoise);
        }
    }
}