using Content.Shared._Misfits.Weapons.TwinTube;
using Content.Shared.Weapons.Ranged.Events;

namespace Content.Client.Weapons.Ranged.Systems;

public sealed partial class GunSystem
{
    private void OnTwinTubeAmmoCount(EntityUid uid, TwinTubeAmmoProviderComponent component, UpdateAmmoCounterEvent args)
    {
        if (args.Control is not DefaultStatusControl control)
            return;

        var count = new GetAmmoCountEvent();
        RaiseLocalEvent(uid, ref count);
        control.Update(count.Count, count.Capacity);
    }
}
