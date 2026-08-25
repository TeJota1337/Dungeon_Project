using UnityEngine;

// Corda leve simulada por Verlet integration: sem Rigidbody, sem PhysX, só a posição
// de cada ponto e a posição do frame anterior (a velocidade fica implícita nessa diferença).
// Roda depois do SlingshotController (DefaultExecutionOrder) pra sempre simular com as
// âncoras já atualizadas no mesmo frame, evitando o "piscar" de corda um frame atrasada.
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(LineRenderer))]
public class VerletRope : MonoBehaviour
{
    [Header("Âncoras")]
    public Transform anchorStart;
    public Transform anchorEnd;

    [Header("Simulação")]
    [Range(2, 20)] public int segmentCount = 8;
    [Tooltip("Folga da corda em repouso. 1 = sempre esticada em linha reta; acima disso, ela sobra e balança.")]
    public float slack = 1.2f;
    public float gravityScale = 1f;
    [Range(0f, 1f)] public float damping = 0.98f;
    [Range(1, 20)] public int constraintIterations = 12;
    [Tooltip("Trava o deltaTime usado na simulação pra um pico de frame (GC, hitch) não fazer a corda 'voar' e piscar.")]
    public float maxDeltaTime = 0.033f;

    private Vector3[] points;
    private Vector3[] prevPoints;
    private float segmentLength;
    private LineRenderer lineRenderer;
    private bool initialized;

    void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
    }

    public void Initialize(Transform start, Transform end)
    {
        anchorStart = start;
        anchorEnd = end;

        float restDistance = Vector3.Distance(anchorStart.position, anchorEnd.position);
        segmentLength = Mathf.Max(restDistance * slack, 0.001f) / segmentCount;

        points = new Vector3[segmentCount + 1];
        prevPoints = new Vector3[segmentCount + 1];

        for (int i = 0; i <= segmentCount; i++)
        {
            float t = (float)i / segmentCount;
            points[i] = Vector3.Lerp(anchorStart.position, anchorEnd.position, t);
            prevPoints[i] = points[i];
        }

        lineRenderer.useWorldSpace = true;
        lineRenderer.positionCount = points.Length;

        initialized = true;
        PushToLineRenderer();
    }

    void LateUpdate()
    {
        if (!initialized || anchorStart == null || anchorEnd == null) return;

        Simulate();
        SatisfyConstraints();
        PushToLineRenderer();
    }

    void Simulate()
    {
        float dt = Mathf.Min(Time.deltaTime, maxDeltaTime);
        Vector3 gravityStep = Physics.gravity * gravityScale * dt * dt;

        // pontas (índice 0 e último) são ancoradas em SatisfyConstraints, não integram aqui.
        for (int i = 1; i < points.Length - 1; i++)
        {
            Vector3 current = points[i];
            Vector3 velocity = (current - prevPoints[i]) * damping;
            points[i] = current + velocity + gravityStep;
            prevPoints[i] = current;
        }
    }

    void SatisfyConstraints()
    {
        int last = points.Length - 1;

        for (int iteration = 0; iteration < constraintIterations; iteration++)
        {
            points[0] = anchorStart.position;
            points[last] = anchorEnd.position;

            for (int i = 0; i < last; i++)
            {
                Vector3 delta = points[i + 1] - points[i];
                float distance = delta.magnitude;
                if (distance < 0.0001f) continue;

                float error = (distance - segmentLength) / distance;
                Vector3 correction = delta * (0.5f * error);

                if (i != 0) points[i] += correction;
                if (i + 1 != last) points[i + 1] -= correction;
            }
        }

        points[0] = anchorStart.position;
        points[last] = anchorEnd.position;
    }

    void PushToLineRenderer()
    {
        lineRenderer.SetPositions(points);
    }
}
