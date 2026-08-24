using System.Collections;
using UnityEngine;

public class ExplosionLightFlash : MonoBehaviour
{
    public float flashIntensity = 8f;
    public float flashDuration = 0.3f;
    public Color flashColor = new Color(1f, 0.6f, 0.2f);

    private Light explosionLight;

    void Awake()
    {
        explosionLight = GetComponent<Light>();
        explosionLight.color = flashColor;
        explosionLight.intensity = flashIntensity;
        StartCoroutine(FadeOut());
    }

    IEnumerator FadeOut()
    {
        float timer = 0f;
        while (timer < flashDuration)
        {
            timer += Time.deltaTime;
            explosionLight.intensity = Mathf.Lerp(flashIntensity, 0f, timer / flashDuration);
            yield return null;
        }
        explosionLight.intensity = 0f;
    }
}