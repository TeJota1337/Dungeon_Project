using UnityEngine;
using TMPro;

public class DamageNumber : MonoBehaviour
{
    [Header("Timing")]
    public float lifetime = 1f;
    public float popDuration = 0.15f;
    public float stackWindow = 0.4f;

    [Header("Movimento (Float)")]
    public float floatSpeed = 1f;
    public float horizontalDriftRange = 0.05f;

    [Header("Pop (escala)")]
    public float baseScale = 1f;
    public float maxScaleBoost = 1.5f;
    public AnimationCurve popCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Size = Damage")]
    public float minDamageForScale = 0f;
    public float maxDamageForScale = 50f;
    public float minSizeFactor = 0.5f;
    public float maxSizeFactor = 2.5f;

    [Header("Escala por Distância do Jogador")]
    public bool scaleWithDistance = true;
    public float referenceDistance = 2f;
    public float minDistanceScale = 0.4f;
    public float maxDistanceScale = 2.5f;

    [Header("Fade")]
    [Range(0f, 1f)]
    public float fadeStartPercent = 0.4f;
    public AnimationCurve fadeCurve = AnimationCurve.Linear(0, 1, 1, 0);

    [Header("Crítico")]
    public float critScaleMultiplier = 1.6f;
    public string critPrefix = "CRÍTICO!\n";

    [Header("Fonte")]
    public TMP_FontAsset font;
    public float fontSize = 72f;
    public FontStyles fontStyle = FontStyles.Bold;
    public TextAlignmentOptions alignment = TextAlignmentOptions.Center;
    public float characterSpacing = 0f;
    public float lineSpacing = 0f;
    public float wordSpacing = 0f;

    [Header("Fonte - Outline")]
    public bool useOutline = true;
    [Range(0f, 1f)]
    public float outlineWidth = 0.2f;
    public Color outlineColor = Color.black;

    [Header("Fonte - Sombra/Glow (via Material)")]
    public bool useUnderlay = false;
    public Color underlayColor = new Color(0, 0, 0, 0.5f);
    public Vector2 underlayOffset = new Vector2(0.1f, -0.1f);
    public float underlaySoftness = 0.1f;

    private TextMeshProUGUI text;
    private Transform cam;
    private float timer;
    private float targetScale;
    private Vector3 randomDrift;

    private int currentAmount;
    private bool isCrit;
    private Color currentColor;
    private float timeSinceLastHit;

    public bool CanStack => timeSinceLastHit <= stackWindow;

    void Awake()
    {
        text = GetComponentInChildren<TextMeshProUGUI>();
        cam = Camera.main != null ? Camera.main.transform : null;

        ApplyFontSettings();
    }

    void ApplyFontSettings()
    {
        if (font != null)
            text.font = font;

        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.characterSpacing = characterSpacing;
        text.lineSpacing = lineSpacing;
        text.wordSpacing = wordSpacing;

        Material mat = text.fontMaterial;

        if (useOutline)
        {
            mat.EnableKeyword("OUTLINE_ON");
            mat.SetFloat(ShaderUtilities.ID_OutlineWidth, outlineWidth);
            mat.SetColor(ShaderUtilities.ID_OutlineColor, outlineColor);
        }
        else
        {
            mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0f);
        }

        if (useUnderlay)
        {
            mat.EnableKeyword("UNDERLAY_ON");
            mat.SetColor(ShaderUtilities.ID_UnderlayColor, underlayColor);
            mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, underlayOffset.x);
            mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, underlayOffset.y);
            mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, underlaySoftness);
        }

        text.fontMaterial = mat;
    }

    public void Setup(int amount, bool crit, Color color)
    {
        currentAmount = amount;
        isCrit = crit;
        currentColor = color;
        timeSinceLastHit = 0f;
        timer = 0f;

        UpdateVisual();

        randomDrift = new Vector3(
            Random.Range(-horizontalDriftRange, horizontalDriftRange),
            0f,
            Random.Range(-horizontalDriftRange, horizontalDriftRange)
        );

        transform.localScale = Vector3.zero;
    }

    public void AddDamage(int amount, bool crit, Color color)
    {
        currentAmount += amount;
        isCrit = isCrit || crit;
        currentColor = color; // usa a cor do hit mais recente
        timeSinceLastHit = 0f;
        timer = 0f;

        UpdateVisual();
        transform.localScale = Vector3.one * (targetScale * 0.7f * GetDistanceScaleFactor());
    }

    void UpdateVisual()
    {
        float t = Mathf.InverseLerp(minDamageForScale, maxDamageForScale, currentAmount);
        float sizeFactor = Mathf.Lerp(minSizeFactor, maxSizeFactor, t);
        targetScale = baseScale * sizeFactor;

        // cor sempre vem da zona atingida, sem alternar entre "normal" e "crítico"
        Color c = currentColor;
        c.a = 1f; // garante alpha cheio ao (re)aplicar, o fade cuida do resto depois
        text.color = c;

        text.text = isCrit ? $"{critPrefix}{currentAmount}" : currentAmount.ToString();

        if (isCrit) targetScale *= critScaleMultiplier;
    }

    float GetDistanceScaleFactor()
    {
        if (!scaleWithDistance || cam == null) return 1f;

        float distance = Vector3.Distance(transform.position, cam.position);
        float factor = distance / referenceDistance;
        return Mathf.Clamp(factor, minDistanceScale, maxDistanceScale);
    }

    void Update()
    {
        timer += Time.deltaTime;
        timeSinceLastHit += Time.deltaTime;

        float distanceScale = GetDistanceScaleFactor();

        if (timer < popDuration)
        {
            float t = timer / popDuration;
            float curveValue = popCurve.Evaluate(t);
            float overshoot = Mathf.Sin(t * Mathf.PI) * (targetScale * (maxScaleBoost - 1f));
            transform.localScale = Vector3.one * ((targetScale * curveValue + overshoot) * distanceScale);
        }
        else
        {
            transform.localScale = Vector3.one * (targetScale * distanceScale);
        }

        transform.position += (Vector3.up * floatSpeed + randomDrift) * Time.deltaTime;

        float fadeStart = lifetime * fadeStartPercent;
        if (timer > fadeStart)
        {
            float fadeT = (timer - fadeStart) / (lifetime - fadeStart);
            float alpha = fadeCurve.Evaluate(fadeT);
            Color c = text.color;
            c.a = alpha;
            text.color = c;
        }

        if (cam != null)
        {
            transform.forward = (transform.position - cam.position).normalized;
        }

        if (timer >= lifetime)
        {
            Destroy(gameObject);
        }
    }
}