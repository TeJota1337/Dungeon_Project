using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

// Centraliza os pulsos hápticos do jogador. Cada mão precisa de um HapticImpulsePlayer
// configurado na cena, apontando (via Input Action Reference) pra ação "Haptic Device"
// do XRI Default Input Actions - é isso que manda a vibração pro controle físico via OpenXR.
public class PlayerHaptics : MonoBehaviour
{
    public static PlayerHaptics Instance { get; private set; }

    [Header("Saídas hápticas (um HapticImpulsePlayer por mão)")]
    public HapticImpulsePlayer leftHand;
    public HapticImpulsePlayer rightHand;

    [Header("Puxada do estilingue (pulso repetido enquanto mira)")]
    public float pullMinAmplitude = 0.05f;
    public float pullMaxAmplitude = 0.35f;
    public float pullPulseDuration = 0.03f;

    [Header("Arrebentar")]
    public float breakAmplitude = 0.9f;
    public float breakDuration = 0.15f;

    [Header("Lançamento")]
    public float launchAmplitude = 0.3f;
    public float launchDuration = 0.05f;

    [Header("Confirmação de acerto")]
    public float hitAmplitude = 0.5f;
    public float hitDuration = 0.04f;

    void Awake()
    {
        Instance = this;
    }

    public void PullTick(float normalizedPull)
    {
        float amplitude = Mathf.Lerp(pullMinAmplitude, pullMaxAmplitude, normalizedPull);
        leftHand?.SendHapticImpulse(amplitude, pullPulseDuration);
        rightHand?.SendHapticImpulse(amplitude, pullPulseDuration);
    }

    public void Break()
    {
        leftHand?.SendHapticImpulse(breakAmplitude, breakDuration);
        rightHand?.SendHapticImpulse(breakAmplitude, breakDuration);
    }

    public void Launch()
    {
        rightHand?.SendHapticImpulse(launchAmplitude, launchDuration);
    }

    public void HitConfirm()
    {
        rightHand?.SendHapticImpulse(hitAmplitude, hitDuration);
    }
}
