using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.GameStates;

namespace Content.Shared._Misfits.Weapons.Sears;

/// <summary>
/// Opt-in marker defining which permanent sear upgrades a firearm accepts.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FirearmSearCompatibleComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool AllowBurst;

    [DataField, AutoNetworkedField]
    public bool AllowFullAuto;
}

/// <summary>
/// An item consumed to permanently add a fire mode to a compatible firearm.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FirearmSearComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public SelectiveFire Mode;

    [DataField, AutoNetworkedField]
    public TimeSpan InstallTime = TimeSpan.FromSeconds(5);
}

/// <summary>
/// Persistent record of the sear installed in a firearm.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class InstalledFirearmSearComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public SelectiveFire Mode;
}
