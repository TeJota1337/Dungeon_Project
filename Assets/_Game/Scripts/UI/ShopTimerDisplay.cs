using TMPro;
using UnityEngine;

// Contagem regressiva de quanto tempo falta pra loja fechar (SpawnManager.ShopTimeRemaining) -
// mesmo padrão de GameTimerDisplay, só lendo outro campo do SpawnManager. Arraste seu texto no
// campo Timer Text; visibilidade é por conta do objeto pai (normalmente fica dentro do
// ShopCanvas, então já liga/desliga junto com a loja).
public class ShopTimerDisplay : MonoBehaviour
{
    public TextMeshProUGUI timerText;

    void Update()
    {
        if (timerText == null || SpawnManager.Instance == null) return;

        timerText.text = Mathf.CeilToInt(SpawnManager.Instance.ShopTimeRemaining).ToString();
    }
}
