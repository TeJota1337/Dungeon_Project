using UnityEngine;

// Sobe e desce em movimento senoidal ao redor da posição inicial.
// Combine com o MMAutoRotate (Feel) no mesmo objeto pra girar + flutuar juntos.
public class GemBob : MonoBehaviour
{
    public float bobHeight = 0.1f;
    public float bobSpeed = 1f;

    private Vector3 startPosition;

    void Start()
    {
        startPosition = transform.localPosition;
    }

    void Update()
    {
        float offset = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.localPosition = startPosition + Vector3.up * offset;
    }
}
