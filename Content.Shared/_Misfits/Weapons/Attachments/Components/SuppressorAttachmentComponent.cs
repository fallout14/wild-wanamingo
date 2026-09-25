using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Misfits.Weapons.Attachments.Components;

/// <summary>
/// Replaces the host firearm's report and optionally hides its muzzle flash while installed.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SuppressorAttachmentComponent : Component
{
    [DataField, AutoNetworkedField]
    public SoundSpecifier? SoundGunshot = new SoundPathSpecifier("/Audio/Weapons/Guns/Gunshots/silenced.ogg");

    [DataField, AutoNetworkedField]
    public bool HideMuzzleFlash = true;
}
