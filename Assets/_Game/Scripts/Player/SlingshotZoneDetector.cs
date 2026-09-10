using UnityEngine;

public class SlingshotZoneDetector : MonoBehaviour
{
    public System.Action onHandEnter;
    public System.Action onHandExit;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("RightHand"))
            onHandEnter?.Invoke();
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("RightHand"))
            onHandExit?.Invoke();
    }
}