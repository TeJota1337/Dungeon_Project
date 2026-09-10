using UnityEngine;

// Simula o "treco" (bolso do estilingue) como um único ponto com física de pêndulo:
// pendurado por duas amarras de comprimento fixo (uma pra cada joint da base), puxado
// pela gravidade igual o VerletRope, mas com só um ponto e duas restrições em vez de
// uma corrente inteira. Enquanto a mão direita está mirando, o ponto fica travado nela;
// ao soltar, ele cai livre até as amarras ficarem esticadas de novo (igual um estilingue
// de verdade descansando no garfo).
//
// Roda ANTES do VerletRope (que é DefaultExecutionOrder 100) pra que as cordas, ao
// simularem, já leiam a posição final do treco neste mesmo frame.
[DefaultExecutionOrder(50)]
public class SlingshotPouch : MonoBehaviour
{
    [Header("Âncoras (joints da base)")]
    public Transform anchorA;
    public Transform anchorB;

    [Header("Física")]
    public float gravityScale = 1f;
    [Range(0f, 1f)] public float damping = 0.95f;
    [Range(1, 20)] public int constraintIterations = 8;
    public float maxDeltaTime = 0.033f;

    private float restLengthA;
    private float restLengthB;
    private Vector3 point;
    private Vector3 prevPoint;
    private bool initialized;
    private bool pinned;
    private Vector3 pinTarget;

    public void Initialize(Transform anchorATransform, Transform anchorBTransform, Vector3 restWorldPosition)
    {
        anchorA = anchorATransform;
        anchorB = anchorBTransform;

        restLengthA = Vector3.Distance(anchorA.position, restWorldPosition);
        restLengthB = Vector3.Distance(anchorB.position, restWorldPosition);

        point = restWorldPosition;
        prevPoint = restWorldPosition;
        initialized = true;

        transform.position = point;
    }

    public void Pin(Vector3 worldPosition)
    {
        pinned = true;
        pinTarget = worldPosition;
    }

    public void Release()
    {
        pinned = false;
    }

    void LateUpdate()
    {
        if (!initialized) return;

        if (pinned)
        {
            point = pinTarget;
            prevPoint = pinTarget;
        }
        else
        {
            Simulate();
            SatisfyConstraints();
        }

        transform.position = point;
    }

    void Simulate()
    {
        float dt = Mathf.Min(Time.deltaTime, maxDeltaTime);
        Vector3 gravityStep = Physics.gravity * gravityScale * dt * dt;

        Vector3 velocity = (point - prevPoint) * damping;
        Vector3 next = point + velocity + gravityStep;
        prevPoint = point;
        point = next;
    }

    void SatisfyConstraints()
    {
        for (int i = 0; i < constraintIterations; i++)
        {
            ConstrainTo(anchorA.position, restLengthA);
            ConstrainTo(anchorB.position, restLengthB);
        }
    }

    // "Amarra" que só puxa quando estica além do comprimento de repouso — deixa o ponto
    // cair livre até ficar esticado nos dois lados, formando o V pendurado.
    void ConstrainTo(Vector3 anchorPos, float restLength)
    {
        Vector3 delta = point - anchorPos;
        float distance = delta.magnitude;
        if (distance <= restLength || distance < 0.0001f) return;

        point = anchorPos + delta * (restLength / distance);
    }
}
