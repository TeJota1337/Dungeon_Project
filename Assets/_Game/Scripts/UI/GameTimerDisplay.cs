using UnityEngine;
using TMPro;

// Escreve o progresso de waves do SpawnManager num texto TMP ("Wave 3/18"). Arraste seu
// texto no campo Timer Text; a posição/Canvas em si é por sua conta.
// (Nome da classe/campo mantido do sistema antigo baseado em tempo, pra não perder a
// referência já configurada no Inspector - o conteúdo mostrado é que mudou.)
public class GameTimerDisplay : MonoBehaviour
{
    public TextMeshProUGUI timerText;

    void Update()
    {
        if (timerText == null || SpawnManager.Instance == null) return;

        int current = Mathf.Clamp(SpawnManager.Instance.CurrentWaveIndex + 1, 0, SpawnManager.Instance.TotalWaves);
        timerText.text = $"Wave {current}/{SpawnManager.Instance.TotalWaves}";
    }
}
