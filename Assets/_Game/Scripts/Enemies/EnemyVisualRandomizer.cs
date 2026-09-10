using UnityEngine;

public class EnemyVisualRandomizer : MonoBehaviour
{
    [Header("Onde instanciar o modelo escolhido")]
    public Transform visualParent;

    [Header("Anima��o (compartilhada entre todos os tipos de inimigo)")]
    public RuntimeAnimatorController baseWalkController;
    public AnimationClip[] walkClips;
    public string walkClipNameInController = "Walk";

    private GameObject spawnedVisual;
    private Animator spawnedAnimator;
    private VisualVariant chosenVariant;

    // O visual (variantes + escala) vem do EnemyDefinition sorteado pelo SpawnManager pra este spawn.
    public Renderer[] Initialize(EnemyDefinition definition)
    {
        SpawnRandomVisual(definition);
        SetupAnimator();

        return spawnedVisual != null
            ? spawnedVisual.GetComponentsInChildren<Renderer>()
            : new Renderer[0];
    }

    void SpawnRandomVisual(EnemyDefinition definition)
    {
        // objeto pode estar sendo reaproveitado do pool: remove o modelo do uso anterior antes de sortear outro
        if (spawnedVisual != null)
        {
            Destroy(spawnedVisual);
            spawnedVisual = null;
            spawnedAnimator = null;
        }

        VisualVariant[] visualVariants = definition != null ? definition.visualVariants : null;
        if (visualVariants == null || visualVariants.Length == 0) return;

        chosenVariant = visualVariants[Random.Range(0, visualVariants.Length)];
        if (chosenVariant.visualPrefab == null) return;

        spawnedVisual = Instantiate(chosenVariant.visualPrefab, visualParent);
        spawnedVisual.transform.localPosition = Vector3.zero;
        spawnedVisual.transform.localRotation = Quaternion.identity;
        spawnedVisual.transform.localScale = Vector3.one * definition.visualScale;
    }

    void SetupAnimator()
    {
        if (spawnedVisual == null || chosenVariant == null) return;

        spawnedAnimator = spawnedVisual.GetComponentInChildren<Animator>();

        if (spawnedAnimator == null)
        {
            spawnedAnimator = spawnedVisual.AddComponent<Animator>();
        }

        // Avatar ESPEC�FICO dessa variante (Generic n�o aceita Avatar compartilhado)
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