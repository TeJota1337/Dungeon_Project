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

    [Header("Input (mão direita — trigger e grip testados em paralelo)")]
    public InputActionReference rightTriggerAction;
    public InputActionReference rightGripAction;

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

    [Header("Elástico (medidor de força: verde → amarelo → vermelho)")]
    public float elasticWidth = 0.01f;
    public Material elasticMaterial;
    public Color forceColorLow = Color.green;
    public Color forceColorMid = Color.yellow;
    public Color forceColorHigh = Color.red;
    [Range(2, 20)] public int elasticSegmentCount = 8;
    [Tooltip("Folga da corda em repouso. 1 = sempre esticada; acima disso ela sobra e balança.")]
    public float elasticSlack = 1.2f;
    public float elasticGravityScale = 1f;

    [Header("Treco (física de pêndulo)")]
    [Tooltip("Quanto o treco 'cai' por gravidade quando ninguém está segurando.")]
    public float pouchGravityScale = 1f;
    [Range(0f, 1f)] public float pouchDamping = 0.95f;

    [Header("Dobra do estilingue (blend shape)")]
    [Tooltip("Nome do parâmetro de blend shape a ativar na base e no treco enquanto o jogador puxa.")]
    public string bendBlendShapeName = "BEND";
    [Tooltip("Peso do blend shape (0-100) no pico do puxão máximo.")]
    public float maxBendWeight = 100f;

    // --- estado interno ---
    private GameObject currentSlingshot;
    private GameObject currentBomb;

    private Transform slingshotTrecoTransform;
    private SlingshotPouch slingshotPouch;
    private Transform jointEsquerdoBase;
    private Transform jointDireitoBase;
    private Transform jointEsquerdoTreco;
    private Transform jointDireitoTreco;
    private VerletRope elasticoEsquerdo;
    private VerletRope elasticoDireito;

    private SkinnedMeshRenderer baseSkinnedRenderer;
    private SkinnedMeshRenderer trecoSkinnedRenderer;
    private int baseBendIndex = -1;
    private int trecoBendIndex = -1;
    private BlendShapeAnchor anchorEsquerdoBase;
    private BlendShapeAnchor anchorDireitoBase;
    private BlendShapeAnchor anchorEsquerdoTreco;
    private BlendShapeAnchor anchorDireitoTreco;
    private bool bendActive;

    private bool isRightHandInZone;
    private bool isAiming;
    private InputAction activeBombAction; // qual ação (trigger ou grip) está segurando a mira atual

    private Vector3[] trajectoryPoints;

    [Header("Háptico (puxada)")]
    [Tooltip("Intervalo entre pulsos hápticos enquanto mira, pra simular tensão contínua sem spamar o motor do controle.")]
    public float hapticPullInterval = 0.08f;
    private float hapticPullTimer;

    void Start()
    {
        SpawnSlingshot();
    }

    void OnEnable()
    {
        if (rightTriggerAction != null)
        {
            rightTriggerAction.action.Enable();
            rightTriggerAction.action.started += OnBombPressed;
            rightTriggerAction.action.canceled += OnBombReleased;
        }

        if (rightGripAction != null)
        {
            rightGripAction.action.Enable();
            rightGripAction.action.started += OnBombPressed;
            rightGripAction.action.canceled += OnBombReleased;
        }
    }

    void OnDisable()
    {
        if (rightTriggerAction != null)
        {
            rightTriggerAction.action.started -= OnBombPressed;
            rightTriggerAction.action.canceled -= OnBombReleased;
        }

        if (rightGripAction != null)
        {
            rightGripAction.action.started -= OnBombPressed;
            rightGripAction.action.canceled -= OnBombReleased;
        }
    }

    void OnDestroy()
    {
        // evita vazar uma bomba presa no pool se o componente for destruído no meio de uma mira.
        if (currentBomb == null) return;

        PooledObject pooledBomb = currentBomb.GetComponent<PooledObject>();
        if (pooledBomb != null) pooledBomb.ReturnToPool();
        else Destroy(currentBomb);
    }

    // ---------- ESTILINGUE (sempre visível, preso na mão esquerda) ----------

    void SpawnSlingshot()
    {
        if (currentSlingshot != null) return;

        currentSlingshot = Instantiate(slingshotPrefab, leftHandTransform.position, leftHandTransform.rotation, leftHandTransform);

        SlingshotZoneDetector detector = currentSlingshot.AddComponent<SlingshotZoneDetector>();
        detector.onHandEnter = () => isRightHandInZone = true;
        detector.onHandExit = () => isRightHandInZone = false;

        SetupTrecoAndElastics(currentSlingshot.transform);
    }

    void SetupTrecoAndElastics(Transform root)
    {
        slingshotTrecoTransform = FindDeepChild(root, "Slingshot_Treco");
        jointEsquerdoBase = FindDeepChild(root, "JointEsquerdoBase");
        jointDireitoBase = FindDeepChild(root, "JointDireitoBase");
        jointEsquerdoTreco = FindDeepChild(root, "JointEsquerdoTreco");
        jointDireitoTreco = FindDeepChild(root, "JointDireitoTreco");

        if (slingshotTrecoTransform == null) Debug.LogWarning("SlingshotController: não encontrei 'Slingshot_Treco' dentro do prefab do estilingue.");
        if (jointEsquerdoBase == null) Debug.LogWarning("SlingshotController: não encontrei 'JointEsquerdoBase' dentro do prefab do estilingue.");
        if (jointDireitoBase == null) Debug.LogWarning("SlingshotController: não encontrei 'JointDireitoBase' dentro do prefab do estilingue.");
        if (jointEsquerdoTreco == null) Debug.LogWarning("SlingshotController: não encontrei 'JointEsquerdoTreco' dentro do prefab do estilingue.");
        if (jointDireitoTreco == null) Debug.LogWarning("SlingshotController: não encontrei 'JointDireitoTreco' dentro do prefab do estilingue.");
        if (elasticMaterial == null) Debug.LogWarning("SlingshotController: 'Elastic Material' não está atribuído no Inspector — o elástico pode renderizar com o material padrão (geralmente invisível/rosa em URP).");

        SetupBend(root);

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

    void SetupBend(Transform root)
    {
        Transform slingshotBaseTransform = FindDeepChild(root, "Slingshot");

        baseSkinnedRenderer = slingshotBaseTransform != null ? slingshotBaseTransform.GetComponentInChildren<SkinnedMeshRenderer>() : null;
        trecoSkinnedRenderer = slingshotTrecoTransform != null ? slingshotTrecoTransform.GetComponentInChildren<SkinnedMeshRenderer>() : null;

        if (baseSkinnedRenderer == null) Debug.LogWarning("SlingshotController: não encontrei um SkinnedMeshRenderer em 'Slingshot' pra dobrar com o blend shape.");
        if (trecoSkinnedRenderer == null) Debug.LogWarning("SlingshotController: não encontrei um SkinnedMeshRenderer em 'Slingshot_Treco' pra dobrar com o blend shape.");

        if (baseSkinnedRenderer != null && !baseSkinnedRenderer.sharedMesh.isReadable)
            Debug.LogWarning("SlingshotController: a malha da base não tem 'Read/Write Enabled' no import — o BakeMesh usado pra mover os joints ao dobrar pode falhar num build.");
        if (trecoSkinnedRenderer != null && !trecoSkinnedRenderer.sharedMesh.isReadable)
            Debug.LogWarning("SlingshotController: a malha do treco não tem 'Read/Write Enabled' no import — o BakeMesh usado pra mover os joints ao dobrar pode falhar num build.");

        baseBendIndex = baseSkinnedRenderer != null ? baseSkinnedRenderer.sharedMesh.GetBlendShapeIndex(bendBlendShapeName) : -1;
        trecoBendIndex = trecoSkinnedRenderer != null ? trecoSkinnedRenderer.sharedMesh.GetBlendShapeIndex(bendBlendShapeName) : -1;

        if (baseSkinnedRenderer != null && baseBendIndex < 0) Debug.LogWarning($"SlingshotController: blend shape '{bendBlendShapeName}' não existe na malha da base do estilingue.");
        if (trecoSkinnedRenderer != null && trecoBendIndex < 0) Debug.LogWarning($"SlingshotController: blend shape '{bendBlendShapeName}' não existe na malha do treco.");

        if (baseBendIndex >= 0)
        {
            if (jointEsquerdoBase != null) anchorEsquerdoBase = new BlendShapeAnchor(baseSkinnedRenderer, jointEsquerdoBase);
            if (jointDireitoBase != null) anchorDireitoBase = new BlendShapeAnchor(baseSkinnedRenderer, jointDireitoBase);
        }

        if (trecoBendIndex >= 0)
        {
            if (jointEsquerdoTreco != null) anchorEsquerdoTreco = new BlendShapeAnchor(trecoSkinnedRenderer, jointEsquerdoTreco);
            if (jointDireitoTreco != null) anchorDireitoTreco = new BlendShapeAnchor(trecoSkinnedRenderer, jointDireitoTreco);
        }
    }

    VerletRope CreateElasticRope(Transform parent, string objName, Transform start, Transform end)
    {
        GameObject go = new GameObject(objName);
        go.transform.SetParent(parent, false);

        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.widthMultiplier = elasticWidth;
        lr.numCapVertices = 4;
        lr.startColor = forceColorLow;
        lr.endColor = forceColorLow;
        if (elasticMaterial != null)
            lr.material = elasticMaterial;

        VerletRope rope = go.AddComponent<VerletRope>();
        rope.segmentCount = elasticSegmentCount;
        rope.slack = elasticSlack;
        rope.gravityScale = elasticGravityScale;
        rope.Initialize(start, end);
        rope.SetColor(forceColorLow); // aplica a cor via MaterialPropertyBlock desde o primeiro frame

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

    // ---------- BOMBA (mão direita — trigger ou grip, o que estiver conectado) ----------

    void OnBombPressed(InputAction.CallbackContext ctx)
    {
        if (currentSlingshot == null || !isRightHandInZone || isAiming) return;

        activeBombAction = ctx.action;

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

    void OnBombReleased(InputAction.CallbackContext ctx)
    {
        // só a mesma ação que iniciou a mira pode terminá-la — evita soltar com o grip
        // uma mira que começou pelo trigger (ou vice-versa) enquanto os dois estão testados juntos.
        if (!isAiming || currentBomb == null || ctx.action != activeBombAction) return;

        LaunchBomb();
        isAiming = false;
        activeBombAction = null;
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

    Color EvaluateForceColor(float t)
    {
        t = Mathf.Clamp01(t);
        return t < 0.5f
            ? Color.Lerp(forceColorLow, forceColorMid, t / 0.5f)
            : Color.Lerp(forceColorMid, forceColorHigh, (t - 0.5f) / 0.5f);
    }

    // ---------- LOOP: mira e trajetória (roda depois do tracking atualizar) ----------

    void LateUpdate()
    {
        if (currentSlingshot != null)
        {
            Vector3 spawnPos = leftHandTransform.position + leftHandTransform.TransformDirection(slingshotOffset);
            currentSlingshot.transform.position = spawnPos;
            currentSlingshot.transform.rotation = leftHandTransform.rotation;

            float pullForce = isAiming ? GetNormalizedPull() : 0f;

            UpdateTreco();
            UpdateBend(pullForce);
            UpdateElasticForceColor(pullForce);
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

    void UpdateBend(float pullForce)
    {
        bool shouldBend = pullForce > 0f;

        if (shouldBend)
        {
            float weight = pullForce * maxBendWeight;

            if (baseBendIndex >= 0) baseSkinnedRenderer.SetBlendShapeWeight(baseBendIndex, weight);
            if (trecoBendIndex >= 0) trecoSkinnedRenderer.SetBlendShapeWeight(trecoBendIndex, weight);

            // move os joints junto com a dobra — sem isso o elástico e o pêndulo do treco
            // ficariam "presos" na pose reta enquanto a malha visualmente dobra.
            anchorEsquerdoBase?.UpdateFollow();
            anchorDireitoBase?.UpdateFollow();
            anchorEsquerdoTreco?.UpdateFollow();
            anchorDireitoTreco?.UpdateFollow();

            bendActive = true;
        }
        else if (bendActive)
        {
            if (baseBendIndex >= 0) baseSkinnedRenderer.SetBlendShapeWeight(baseBendIndex, 0f);
            if (trecoBendIndex >= 0) trecoSkinnedRenderer.SetBlendShapeWeight(trecoBendIndex, 0f);

            anchorEsquerdoBase?.ResetToRest();
            anchorDireitoBase?.ResetToRest();
            anchorEsquerdoTreco?.ResetToRest();
            anchorDireitoTreco?.ResetToRest();

            bendActive = false;
        }
    }

    void UpdateElasticForceColor(float pullForce)
    {
        Color color = EvaluateForceColor(pullForce);

        elasticoEsquerdo?.SetColor(color);
        elasticoDireito?.SetColor(color);
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
