using Robust.Shared.GameStates;

namespace Content.Shared._Misfits.Scope;

/// <summary>
/// Grants an enhanced vision mode only while this scope is actively being used.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ScopeVisionComponent : Component
{
    [DataField, AutoNetworkedField]
    public ScopeVisionMode Mode;
}

public enum ScopeVisionMode : byte
{
    None,
    NightVision,
    Thermal,
}

/// <summary>
/// Raised by mouse-wheel zoom binds before normal player zoom is considered.
/// </summary>
[ByRefEvent]
public record struct ScopeZoomInputEvent(int Direction, bool Handled = false);
