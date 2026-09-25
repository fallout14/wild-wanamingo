using Robust.Shared.GameStates;

namespace Content.Shared._Misfits.Weapons.TwinTube;

/// <summary>
/// Two fixed magazine tubes. Loading and cycling use the selected tube;
/// firing switches to the other tube when the selected one is empty.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TwinTubeAmmoProviderComponent : Component
{
    public const string LeftTube = "left_tube";
    public const string RightTube = "right_tube";

    [DataField, AutoNetworkedField]
    public bool RightSelected;
}
