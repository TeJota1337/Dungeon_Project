using UnityEngine;
using TMPro;

// Escreve o tempo restante do SpawnManager num texto TMP. Arraste seu texto
// no campo Timer Text; a posição/Canvas em si é por sua conta.
public class GameTimerDisplay : MonoBehaviour
{
    public TextMeshProUGUI timerText;

    void Update()
    {
        if (timerText == null || SpawnManager.Instance == null) return;

        timerText.text = SpawnManager.Instance.GetFormattedTimeRemaining();
    }
}
