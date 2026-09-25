using Robust.Shared.GameStates;

namespace Content.Shared._Misfits.Weapons.Ranged;

/// <summary>Predicted state of the firearm cycle key on its user.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FirearmCycleInputComponent : Component
{
    [AutoNetworkedField]
    public bool Held;
}
