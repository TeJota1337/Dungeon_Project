using TMPro;
using UnityEngine;

// Mostra a quantidade de cabeças de esqueleto do jogador (PlayerCurrency.SkullCount) - mesmo
// padrão de GameTimerDisplay/ShopTimerDisplay. Arraste seu texto no campo Currency Text.
public class PlayerCurrencyDisplay : MonoBehaviour
{
    public TextMeshProUGUI currencyText;

    void Update()
    {
        if (currencyText == null || PlayerCurrency.Instance == null) return;

        currencyText.text = PlayerCurrency.Instance.SkullCount.ToString();
    }
}
