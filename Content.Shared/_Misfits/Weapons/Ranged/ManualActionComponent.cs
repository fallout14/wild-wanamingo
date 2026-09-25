using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Misfits.Weapons.Ranged;

/// <summary>Requires the held firearm to be manually cycled between shots.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ManualActionComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool NeedsCycle;

    [DataField, AutoNetworkedField]
    public bool SlamFire;

    [DataField]
    public SoundSpecifier CycleSound = new SoundPathSpecifier("/Audio/Weapons/Guns/Cock/ltrifle_cock.ogg");
}
