using System.Linq;
using Content.Shared.Effects;
using Robust.Shared.Player;

namespace Content.Server.Effects;

public sealed class ColorFlashEffectSystem : SharedColorFlashEffectSystem
{
    public override void RaiseEffect(Color color, List<EntityUid> entities, Filter filter, float? animationLength = null)
    {
        // #Misfits Change: suppress the red hit-flash on player-controlled entities — it is metagamey to broadcast a player's damage state visually.
        var nonPlayerEntities = entities.Where(e => !HasComp<ActorComponent>(e)).ToList();
        if (nonPlayerEntities.Count == 0)
            return;

        RaiseNetworkEvent(new ColorFlashEffectEvent(color, GetNetEntityList(nonPlayerEntities), animationLength), filter);
    }
}
