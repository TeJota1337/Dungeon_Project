using System.Collections.Generic;
using UnityEngine;

public class DamageNumberManager : MonoBehaviour
{
    public static DamageNumberManager Instance;

    public GameObject damageNumberPrefab;
    public float spawnHeightOffset = 0.3f;
    public float spreadRadius = 0.1f;

    private Dictionary<GameObject, DamageNumber> activeNumbers = new Dictionary<GameObject, DamageNumber>();

    void Awake()
    {
        Instance = this;
    }

    public void Spawn(GameObject owner, Vector3 worldPosition, int amount, bool isCrit, Color color)
    {
        if (activeNumbers.TryGetValue(owner, out DamageNumber existing))
        {
            if (existing == null)
            {
                activeNumbers.Remove(owner);
            }
            else if (existing.CanStack)
            {
                existing.AddDamage(amount, isCrit, color);
                return;
            }
        }

        Vector3 randomOffset = new Vector3(
            Random.Range(-spreadRadius, spreadRadius),
            0f,
            Random.Range(-spreadRadius, spreadRadius)
        );

        Vector3 spawnPos = worldPosition + Vector3.up * spawnHeightOffset + randomOffset;
        GameObject obj = ObjectPoolManager.Instance.Get(damageNumberPrefab, spawnPos, Quaternion.identity);
        DamageNumber dn = obj.GetComponent<DamageNumber>();
        dn.Setup(owner, amount, isCrit, color);

        activeNumbers[owner] = dn;
    }

    // Chamado pelo pr�prio DamageNumber ao voltar pro pool: como o objeto n�o �
    // destru�do de verdade (s� desativado e reaproveitado), a checagem "existing == null"
    // do Spawn() n�o basta pra detectar que a refer�ncia antiga expirou.
    public void ClearOwnerIfCurrent(GameObject owner, DamageNumber number)
    {
        if (owner == null) return;

        if (activeNumbers.TryGetValue(owner, out DamageNumber current) && current == number)
            activeNumbers.Remove(owner);
    }
}