using UnityEngine;

[System.Serializable]
public class VisualVariant
{
    public string variantName;
    public GameObject visualPrefab;
    public Avatar avatar; // cada variante carrega seu próprio Avatar (Generic)
}
