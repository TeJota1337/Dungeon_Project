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

    [Header("Elástico (corda entre os joints, sempre visível)")]
    public float elasticWidth = 0.01f;
    public Color elasticColor = Color.black;
    public Material elasticMaterial;
    [Range(2, 20)] public int elasticSegmentCount = 8;
    [Tooltip("Folga da corda em repouso. 1 = sempre esticada; acima disso ela sobra e balança.")]
    public float elasticSlack = 1.2f;
    public float elasticGravityScale = 1f;

    [Header("Treco (física de pêndulo)")]
    [Tooltip("Quanto o treco 'cai' por gravidade quando ninguém está segurando.")]
    public float pouchGravityScale = 1f;
    [Range(0f, 1f)] public float pouchDamping = 0.95f;

    // --- estado interno ---
    private GameObject currentSlingshot;
    private GameObject currentBomb;
    private Renderer slingshotRenderer;
    private Material slingshotMaterialInstance;
    private Collider slingshotTriggerZone;

    private Transform slingshotTrecoTransform;
    private SlingshotPouch slingshotPouch;
    private Transform jointEsquerdoBase;
    private Transform jointDireitoBase;
    private Transform jointEsquerdoTreco;
    private Transform jointDireitoTreco;
    private VerletRope elasticoEsquerdo;
    private VerletRope elasticoDireito;

    private bool isRightHandInZone;
    private bool isAiming;
    private bool? lastSlingshotColorState;

    private Vector3[] trajectoryPoints;

    [Header("Háptico (puxada)")]
    [Tooltip("Intervalo entre pulsos hápticos enquanto mira, pra simular tensão contínua sem spamar o motor do controle.")]
    public float hapticPullInterval = 0.08f;
    private float hapticPullTimer;

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
        slingshotMaterialInstance = slingshotRenderer != null ? slingshotRenderer.material : null;
        slingshotTriggerZone = currentSlingshot.GetComponentInChildren<Collider>();

        SlingshotZoneDetector detector = currentSlingshot.AddComponent<SlingshotZoneDetector>();
        detector.onHandEnter = () => isRightHandInZone = true;
        detector.onHandExit = () => isRightHandInZone = false;

        SetupTrecoAndElastics(currentSlingshot.transform);

        lastSlingshotColorState = null;
        SetSlingshotColor(idleColor);
    }

    void SetupTrecoAndElastics(Transform root)
    {
        slingshotTrecoTransform = FindDeepChild(root, "Slingshot_Treco");
        jointEsquerdoBase = FindDeepChild(root, "JointEsquerdoBase");
        jointDireitoBase = FindDeepChild(root, "JointDireitoBase");
        jointEsquerdoTreco = FindDeepChild(root, "JointEsquerdoTreco");
        jointDireitoTreco = FindDeepChild(root, "JointDireitoTreco");

        if (slingshotTrecoTransform != null && jointEsquerdoBase != null && jointDireitoBase != null)
        {
            slingshotPouch = slingshotTrecoTransform.gameObject.AddComponent<SlingshotPouch>();
            slingshotPouch.gravityScale = pouchGravityScale;
            slingshotPouch.damping = pouchDamping;
            slingshotPouch.Initialize(jointEsquerdoBase, jointDireitoBase, slingshotTrecoTransform.position);
        }

        if (jointEsquerdoBase != null && jointEsquerdoTreco != null)
            elasticoEsquerdo = CreateElasticRope(root, "ElasticoEsquerdo", jointEsquerdoBase, jointEsquerdoTreco);

        if (jointDireitoBase != null && jointDireitoTreco != null)
            elasticoDireito = CreateElasticRope(root, "ElasticoDireito", jointDireitoBase, jointDireitoTreco);
    }

    VerletRope CreateElasticRope(Transform parent, string objName, Transform start, Transform end)
    {
        GameObject go = new GameObject(objName);
        go.transform.SetParent(parent, false);

        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.widthMultiplier = elasticWidth;
        lr.numCapVertices = 4;
        lr.startColor = elasticColor;
        lr.endColor = elasticColor;
        if (elasticMaterial != null)
            lr.material = elasticMaterial;

        VerletRope rope = go.AddComponent<VerletRope>();
        rope.segmentCount = elasticSegmentCount;
        rope.slack = elasticSlack;
        rope.gravityScale = elasticGravityScale;
        rope.Initialize(start, end);

        return rope;
    }

    static Transform FindDeepChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;

            Transform result = FindDeepChild(child, name);
            if (result != null) return result;
        }
        return null;
    }

    void CancelEverything()
    {
        if (currentBomb != null)
        {
            PooledObject pooledBomb = currentBomb.GetComponent<PooledObject>();
            if (pooledBomb != null) pooledBomb.ReturnToPool();
            else Destroy(currentBomb);
        }
        if (currentSlingshot != null) Destroy(currentSlingshot);

        currentBomb = null;
        currentSlingshot = null;
        slingshotRenderer = null;
        slingshotMaterialInstance = null;
        lastSlingshotColorState = null;
        isAiming = false;
        isRightHandInZone = false;
        trajectoryLine.enabled = false;

        slingshotTrecoTransform = null;
        slingshotPouch = null;
        jointEsquerdoBase = null;
        jointDireitoBase = null;
        jointEsquerdoTreco = null;
        jointDireitoTreco = null;
        elasticoEsquerdo = null;
        elasticoDireito = null;
    }

    void SetSlingshotColor(Color color)
    {
        if (slingshotMaterialInstance != null)
            slingshotMaterialInstance.color = color;
    }

    // ---------- MÃO DIREITA (bomba) ----------

    void OnRightTriggerPressed(InputAction.CallbackContext ctx)
    {
        if (currentSlingshot == null || !isRightHandInZone) return;

        currentBomb = ObjectPoolManager.Instance.Get(bombPrefab, bombSpawnPoint.position, Quaternion.identity);

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

        PlayerHaptics.Instance?.Launch();
        GameAudio.Instance?.PlaySlingshotLaunch(bombSpawnPoint.position);

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

        PlayerHaptics.Instance?.Break();
        GameAudio.Instance?.PlaySlingshotBreak(currentSlingshot != null ? currentSlingshot.transform.position : transform.position);

        currentBomb = null;
    }

    // ---------- CÁLCULO DE FORÇA/DIREÇÃO ----------

    float GetNormalizedPull()
    {
        Vector3 pullVector = leftHandTransform.position - rightHandTransform.position;
        float pullDistance = Mathf.Clamp(pullVector.magnitude, 0, maxPullDistance);
        return pullDistance / maxPullDistance;
    }

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
        if (currentSlingshot != null && lastSlingshotColorState != isRightHandInZone)
        {
            lastSlingshotColorState = isRightHandInZone;
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

            UpdateTreco();
        }

        if (isAiming && currentBomb != null)
        {
            currentBomb.transform.position = bombSpawnPoint.position;

            Vector3 launchVelocity = GetLaunchVelocity();
            DrawTrajectory(bombSpawnPoint.position, launchVelocity);

            hapticPullTimer += Time.deltaTime;
            if (hapticPullTimer >= hapticPullInterval)
            {
                hapticPullTimer = 0f;
                PlayerHaptics.Instance?.PullTick(GetNormalizedPull());
            }
        }
    }

    void UpdateTreco()
    {
        if (slingshotPouch == null) return;

        if (isAiming)
            slingshotPouch.Pin(rightHandTransform.position); // treco vem junto com a mão que está puxando a bomba
        else
            slingshotPouch.Release(); // cai livre até as amarras esticarem, igual um estilingue de verdade
    }

    void DrawTrajectory(Vector3 startPos, Vector3 startVelocity)
    {
        if (trajectoryPoints == null || trajectoryPoints.Length != trajectoryResolution)
            trajectoryPoints = new Vector3[Mathf.Max(trajectoryResolution, 1)];

        float timeStep = maxSimulationTime / trajectoryResolution;

        Vector3 pos = startPos;
        Vector3 vel = startVelocity;

        int count = 1;
        trajectoryPoints[0] = pos;

        for (int i = 1; i < trajectoryResolution; i++)
        {
            Vector3 nextPos = pos + vel * timeStep;
            vel += Physics.gravity * timeStep;

            bool checkCollision = i > 2;

            if (checkCollision && Physics.Linecast(pos, nextPos, out RaycastHit hit, trajectoryCollisionMask, QueryTriggerInteraction.Ignore))
            {
                trajectoryPoints[count++] = hit.point;
                break;
            }

            trajectoryPoints[count++] = nextPos;
            pos = nextPos;
        }

        trajectoryLine.positionCount = count;
        trajectoryLine.SetPositions(trajectoryPoints);
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