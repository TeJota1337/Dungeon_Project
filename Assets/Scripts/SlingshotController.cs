using UnityEngine;
using UnityEngine.InputSystem;

public class SlingshotController : MonoBehaviour
{
    [Header("Referências dos controles")]
    public Transform leftHandTransform;   // mão que segura o estilingue
    public Transform rightHandTransform;  // mão que puxa/lança
    public Transform bombSpawnPoint;      // ponto visual de onde a bomba nasce/mira

    [Header("Bomba Gigante")]
    [Range(0f, 1f)]
    public float giantBombChance = 0.05f;

    [Header("Colisão a ignorar (corpo do jogador)")]
    public Collider[] playerColliders;

    [Header("Ajuste de posição")]
    public Vector3 slingshotOffset = new Vector3(0, -0.1f, 0);

    [Header("Input")]
    public InputActionReference leftTriggerAction;
    public InputActionReference rightTriggerAction;

    [Header("Prefabs")]
    public GameObject slingshotPrefab;
    public GameObject bombPrefab;

    [Header("Trajetória")]
    public LineRenderer trajectoryLine;
    public float launchForceMultiplier = 16f;
    public float minPullDistance = 0.05f;
    public float maxPullDistance = 0.5f;
    public AnimationCurve pullForceCurve = AnimationCurve.EaseInOut(0, 0, 1, 1); // resposta do puxão (0=solto, 1=máximo)
    public float maxSimulationTime = 3f;
    public int trajectoryResolution = 60;
    public LayerMask trajectoryCollisionMask = ~0;

    [Header("Rotação da Bomba")]
    public float minSpin = 90f;   // graus/segundo no lançamento mais fraco
    public float maxSpin = 720f;  // graus/segundo no lançamento mais forte

    [Header("Arrebentar (puxar demais)")]
    [Range(0f, 1f)]
    public float breakThreshold = 0.85f;  // % do maxPullDistance a partir do qual existe risco de arrebentar
    [Range(0f, 1f)]
    public float maxBreakChance = 0.6f;   // chance de arrebentar bem no limite máximo

    [Header("Feedback visual")]
    public Color readyColor = Color.green;
    public Color idleColor = Color.white;

    // --- estado interno ---
    private GameObject currentSlingshot;
    private GameObject currentBomb;
    private Renderer slingshotRenderer;
    private Collider slingshotTriggerZone;

    private bool isRightHandInZone;
    private bool isAiming;

    void OnEnable()
    {
        leftTriggerAction.action.Enable();
        rightTriggerAction.action.Enable();

        leftTriggerAction.action.started += OnLeftTriggerPressed;
        leftTriggerAction.action.canceled += OnLeftTriggerReleased;

        rightTriggerAction.action.started += OnRightTriggerPressed;
        rightTriggerAction.action.canceled += OnRightTriggerReleased;
    }

    void OnDisable()
    {
        leftTriggerAction.action.started -= OnLeftTriggerPressed;
        leftTriggerAction.action.canceled -= OnLeftTriggerReleased;

        rightTriggerAction.action.started -= OnRightTriggerPressed;
        rightTriggerAction.action.canceled -= OnRightTriggerReleased;
    }

    // ---------- MÃO ESQUERDA (estilingue) ----------

    void OnLeftTriggerPressed(InputAction.CallbackContext ctx)
    {
        SpawnSlingshot();
    }

    void OnLeftTriggerReleased(InputAction.CallbackContext ctx)
    {
        CancelEverything();
    }

    void SpawnSlingshot()
    {
        if (currentSlingshot != null) return;

        currentSlingshot = Instantiate(slingshotPrefab, leftHandTransform.position, leftHandTransform.rotation, leftHandTransform);
        slingshotRenderer = currentSlingshot.GetComponentInChildren<Renderer>();
        slingshotTriggerZone = currentSlingshot.GetComponentInChildren<Collider>();

        SlingshotZoneDetector detector = currentSlingshot.AddComponent<SlingshotZoneDetector>();
        detector.onHandEnter = () => isRightHandInZone = true;
        detector.onHandExit = () => isRightHandInZone = false;

        SetSlingshotColor(idleColor);
    }

    void CancelEverything()
    {
        if (currentBomb != null) Destroy(currentBomb);
        if (currentSlingshot != null) Destroy(currentSlingshot);

        currentBomb = null;
        currentSlingshot = null;
        isAiming = false;
        isRightHandInZone = false;
        trajectoryLine.enabled = false;
    }

    void SetSlingshotColor(Color color)
    {
        if (slingshotRenderer != null)
            slingshotRenderer.material.color = color;
    }

    // ---------- MÃO DIREITA (bomba) ----------

    void OnRightTriggerPressed(InputAction.CallbackContext ctx)
    {
        if (currentSlingshot == null || !isRightHandInZone) return;

        currentBomb = Instantiate(bombPrefab, bombSpawnPoint.position, Quaternion.identity);

        Projectile_Bomb bombScript = currentBomb.GetComponent<Projectile_Bomb>();
        if (bombScript != null)
        {
            if (Random.value < giantBombChance)
            {
                bombScript.isGiant = true;
            }

            bombScript.IgnoreCollisionsWith(playerColliders); // registra primeiro, com collider ativo
            bombScript.SetCollisionEnabled(false);              // só então desativa
        }

        Rigidbody rb = currentBomb.GetComponent<Rigidbody>();
        rb.isKinematic = true;

        isAiming = true;
        trajectoryLine.enabled = true;
    }

    void OnRightTriggerReleased(InputAction.CallbackContext ctx)
    {
        if (!isAiming || currentBomb == null) return;

        LaunchBomb();
        isAiming = false;
        trajectoryLine.enabled = false;
    }

    void LaunchBomb()
    {
        Vector3 pullVector = leftHandTransform.position - rightHandTransform.position;
        float pullDistance = Mathf.Clamp(pullVector.magnitude, 0, maxPullDistance);
        float normalizedPull = pullDistance / maxPullDistance;

        // checagem de "arrebentar" se passou do threshold
        if (normalizedPull > breakThreshold)
        {
            float overPull = Mathf.InverseLerp(breakThreshold, 1f, normalizedPull);
            float breakChance = overPull * maxBreakChance;

            if (Random.value < breakChance)
            {
                BreakSlingshot();
                return;
            }
        }

        Vector3 launchVelocity = GetLaunchVelocity();

        Projectile_Bomb bombScript = currentBomb.GetComponent<Projectile_Bomb>();
        if (bombScript != null)
        {
            bombScript.SetCollisionEnabled(true);
        }

        Rigidbody rb = currentBomb.GetComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.linearVelocity = launchVelocity;

        // ROTAÇÃO baseada na força: quanto mais forte, mais spin
        float speedFactor = Mathf.InverseLerp(0, launchForceMultiplier, launchVelocity.magnitude);
        float spinAmount = Mathf.Lerp(minSpin, maxSpin, speedFactor);

        Vector3 randomAxis = new Vector3(
            Random.Range(-1f, 1f),
            Random.Range(-1f, 1f),
            Random.Range(-1f, 1f)
        ).normalized;

        rb.angularVelocity = randomAxis * (spinAmount * Mathf.Deg2Rad);

        currentBomb = null;
    }

    void BreakSlingshot()
    {
        Debug.Log("O estilingue arrebentou! A bomba caiu sem força.");

        Rigidbody rb = currentBomb.GetComponent<Rigidbody>();
        rb.isKinematic = false;

        Projectile_Bomb bombScript = currentBomb.GetComponent<Projectile_Bomb>();
        if (bombScript != null)
        {
            bombScript.SetCollisionEnabled(true);
        }

        // TODO (opcional): tocar som de "elástico arrebentando" e/ou vibrar o controle aqui

        currentBomb = null;
    }

    // ---------- CÁLCULO DE FORÇA/DIREÇÃO ----------

    Vector3 GetLaunchVelocity()
    {
        Vector3 pullVector = leftHandTransform.position - rightHandTransform.position;
        float pullDistance = Mathf.Clamp(pullVector.magnitude, 0, maxPullDistance);

        if (pullDistance < minPullDistance)
            return Vector3.zero;

        float normalizedPull = pullDistance / maxPullDistance;
        float curveValue = pullForceCurve.Evaluate(normalizedPull);

        Vector3 direction = pullVector.normalized;
        return direction * curveValue * launchForceMultiplier;
    }

    // ---------- LOOP: cor (não sensível a ordem de tracking) ----------

    void Update()
    {
        if (currentSlingshot != null)
        {
            SetSlingshotColor(isRightHandInZone ? readyColor : idleColor);
        }
    }

    // ---------- LOOP: mira e trajetória (roda depois do tracking atualizar) ----------

    void LateUpdate()
    {
        if (currentSlingshot != null)
        {
            Vector3 spawnPos = leftHandTransform.position + leftHandTransform.TransformDirection(slingshotOffset);
            currentSlingshot.transform.position = spawnPos;
            currentSlingshot.transform.rotation = leftHandTransform.rotation;
        }

        if (isAiming && currentBomb != null)
        {
            currentBomb.transform.position = bombSpawnPoint.position;

            Vector3 launchVelocity = GetLaunchVelocity();
            DrawTrajectory(bombSpawnPoint.position, launchVelocity);
        }
    }

    void DrawTrajectory(Vector3 startPos, Vector3 startVelocity)
    {
        float timeStep = maxSimulationTime / trajectoryResolution;

        Vector3 pos = startPos;
        Vector3 vel = startVelocity;

        System.Collections.Generic.List<Vector3> points = new System.Collections.Generic.List<Vector3>();
        points.Add(pos);

        for (int i = 1; i < trajectoryResolution; i++)
        {
            Vector3 nextPos = pos + vel * timeStep;
            vel += Physics.gravity * timeStep;

            bool checkCollision = i > 2;

            if (checkCollision && Physics.Linecast(pos, nextPos, out RaycastHit hit, trajectoryCollisionMask, QueryTriggerInteraction.Ignore))
            {
                points.Add(hit.point);
                break;
            }

            points.Add(nextPos);
            pos = nextPos;
        }

        trajectoryLine.positionCount = points.Count;
        trajectoryLine.SetPositions(points.ToArray());
    }

    void OnDrawGizmos()
    {
        if (bombSpawnPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(bombSpawnPoint.position, 0.03f);
        }

        if (rightHandTransform != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(rightHandTransform.position, 0.03f);
        }

        if (leftHandTransform != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(leftHandTransform.position, 0.03f);
        }
    }
}