using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Maths;

namespace Content.Shared._Misfits.Weapons.Attachments.Components;

/// <summary>
/// Replaces the host firearm's rifle-butt attack with a bayonet thrust while installed.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BayonetAttachmentComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public DamageSpecifier Damage = default!;

    [DataField, AutoNetworkedField]
    public SoundSpecifier HitSound = new SoundPathSpecifier("/Audio/Weapons/bladeslice.ogg");

    /// <summary>
    /// Rotates the copied weapon sprite so its muzzle points toward the melee target.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Angle AnimationRotation = Angle.FromDegrees(-45);
}
