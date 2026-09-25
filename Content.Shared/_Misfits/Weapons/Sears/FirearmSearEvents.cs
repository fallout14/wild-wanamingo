using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Misfits.Weapons.Sears;

[Serializable, NetSerializable]
public sealed partial class InstallFirearmSearDoAfterEvent : SimpleDoAfterEvent;
