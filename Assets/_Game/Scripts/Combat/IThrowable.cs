using UnityEngine;

// Contrato mínimo que qualquer prefab de item lançável pelo estilingue precisa implementar,
// pra o SlingshotController conseguir lançar "o que estiver equipado" sem precisar saber se é
// uma bomba, uma pedra, ou um item futuro. Projectile_Bomb já implementa isso.
public interface IThrowable
{
    void SetCollisionEnabled(bool enabled);
    void IgnoreCollisionsWith(Collider[] collidersToIgnore);

    // Avisa o projétil de qual ItemDefinition o gerou, pra ele poder consultar o bônus de dano
    // acumulado desse item em PlayerUpgrades (GDD 2, seção 9 - upgrades da loja).
    void SetSourceItem(ItemDefinition item);
}
