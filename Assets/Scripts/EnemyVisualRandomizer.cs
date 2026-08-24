using UnityEngine;

[System.Serializable]
public class VisualVariant
{
    public string variantName;
    public GameObject visualPrefab;
    public Avatar avatar; // NOVO: cada variante carrega seu próprio Avatar (Generic)
}

public class EnemyVisualRandomizer : MonoBehaviour
{
    [Header("Onde instanciar o modelo escolhido")]
    public Transform visualParent;

    [Header("Escala do modelo")]
    public float visualScale = 0.7f;

    [Header("Variações visuais possíveis")]
    public VisualVariant[] visualVariants;

    [Header("Animação")]
    public RuntimeAnimatorController baseWalkController;
    public AnimationClip[] walkClips;
    public string walkClipNameInController = "Walk";

    private GameObject spawnedVisual;
    private Animator spawnedAnimator;
    private VisualVariant chosenVariant;

    public Renderer[] Initialize()
    {
        SpawnRandomVisual();
        SetupAnimator();

        return spawnedVisual != null
            ? spawnedVisual.GetComponentsInChildren<Renderer>()
            : new Renderer[0];
    }

    void SpawnRandomVisual()
    {
        if (visualVariants == null || visualVariants.Length == 0) return;

        chosenVariant = visualVariants[Random.Range(0, visualVariants.Length)];
        if (chosenVariant.visualPrefab == null) return;

        spawnedVisual = Instantiate(chosenVariant.visualPrefab, visualParent);
        spawnedVisual.transform.localPosition = Vector3.zero;
        spawnedVisual.transform.localRotation = Quaternion.identity;
        spawnedVisual.transform.localScale = Vector3.one * visualScale; // NOVO
    }

    void SetupAnimator()
    {
        if (spawnedVisual == null || chosenVariant == null) return;

        spawnedAnimator = spawnedVisual.GetComponentInChildren<Animator>();

        if (spawnedAnimator == null)
        {
            spawnedAnimator = spawnedVisual.AddComponent<Animator>();
        }

        // Avatar ESPECÍFICO dessa variante (Generic não aceita Avatar compartilhado)
        if (chosenVariant.avatar != null)
        {
            spawnedAnimator.avatar = chosenVariant.avatar;
        }

        if (baseWalkController != null)
        {
            var overrideController = new AnimatorOverrideController(baseWalkController);

            if (walkClips != null && walkClips.Length > 0)
            {
                AnimationClip chosenClip = walkClips[Random.Range(0, walkClips.Length)];
                overrideController[walkClipNameInController] = chosenClip;
            }

            spawnedAnimator.runtimeAnimatorController = overrideController;
        }
    }
}