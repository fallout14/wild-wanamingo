using Content.Client.Weapons.Ranged.Systems;

namespace Content.Client.Weapons.Ranged.Components;

/// <summary>
/// Visualizer for gun mag presence; can change states based on ammo count or toggle visibility entirely.
/// </summary>
[RegisterComponent, Access(typeof(GunSystem))]
public sealed partial class MagazineVisualsComponent : Component
{
    /// <summary>
    /// The chamber visualizer replaces the entire base frame for magazine and bolt states.
    /// Use for artwork containing the whole gun instead of separate magazine overlays.
    /// </summary>
    [DataField] public bool FullSprite;

    /// <summary>Optional prefix for full weapon frames when Foldable is folded.</summary>
    [DataField] public string? FoldedStatePrefix;

    /// <summary>
    /// What RsiState we use.
    /// </summary>
    [DataField("magState")] public string? MagState;

    /// <summary>
    /// How many steps there are
    /// </summary>
    [DataField("steps")] public int MagSteps;

    /// <summary>
    /// Should we hide when the count is 0
    /// </summary>
    [DataField("zeroVisible")] public bool ZeroVisible;
}

public enum GunVisualLayers : byte
{
    Base,
    BaseUnshaded,
    Mag,
    MagUnshaded,
}
