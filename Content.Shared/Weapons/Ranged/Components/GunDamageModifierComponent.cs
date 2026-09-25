using Content.Shared.Damage;
using Robust.Shared.GameStates;

namespace Content.Shared.Weapons.Ranged.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class GunDamageModifierComponent : Component
{
    // Adds extra damage to projectiles fired by an applicable weapon.
    [DataField]
    public DamageSpecifier Damage = new();
}