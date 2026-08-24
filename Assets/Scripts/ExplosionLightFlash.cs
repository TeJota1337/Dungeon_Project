using System.Collections;
using UnityEngine;

public class ExplosionLightFlash : MonoBehaviour, IPoolable
{
    public float flashIntensity = 8f;
    public float flashDuration = 0.3f;
    public Color flashColor = new Color(1f, 0.6f, 0.2f);

    private Light explosionLight;
    private Coroutine fadeRoutine;

    void Awake()
    {
        explosionLight = GetComponent<Light>();
    }

    public void OnSpawnFromPool()
    {
        explosionLight.color = flashColor;
        explosionLight.intensity = flashIntensity;

        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        fadeRoutine = StartCoroutine(FadeOut());
    }

    public void OnReturnToPool()
    {
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }
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
