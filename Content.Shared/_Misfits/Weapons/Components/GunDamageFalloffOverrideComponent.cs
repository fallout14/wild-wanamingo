// #Misfits Add - Lets a gun re-tune the damage falloff of the bullets it fires
namespace Content.Shared._Misfits.Weapons.Components;

/// <summary>
/// Overrides the <see cref="BallisticDamageFalloffComponent"/> values of every projectile this gun fires,
/// so a weapon can make a shared caliber keep or lose damage over range differently
/// (e.g. a suppressed 9mm marksman rifle that drops off hard past medium range).
/// </summary>
[RegisterComponent]
public sealed partial class GunDamageFalloffOverrideComponent : Component
{
    /// <summary>
    /// Distance in tiles before damage starts to fall off.
    /// </summary>
    [DataField]
    public float FalloffStartTiles = 4f;

    /// <summary>
    /// Distance in tiles at which damage reaches <see cref="MinDamageMultiplier"/>.
    /// </summary>
    [DataField]
    public float MaxFalloffTiles = 15f;

    /// <summary>
    /// Damage multiplier at and beyond <see cref="MaxFalloffTiles"/>.
    /// </summary>
    [DataField]
    public float MinDamageMultiplier = 0.5f;
}
