using Robust.Shared;
using Robust.Shared.Configuration;

namespace Content.Shared._Misfits.CCVar;

[CVarDefs]
public sealed class MaterialExtractorCVars : CVars
{
    /// <summary>
    /// Guaranteed extractors per Wendover map at round start, provided enough locations exist.
    /// Negative values are treated as zero. Changes apply next round.
    /// </summary>
    public static readonly CVarDef<int> GuaranteedCount =
        CVarDef.Create("misfits.material_extractor_count", 2, CVar.SERVER | CVar.SERVERONLY);

    /// <summary>
    /// Probability of one additional extractor per Wendover map (0 = never, 1 = always).
    /// Clamped to [0, 1]. Changes apply next round.
    /// </summary>
    public static readonly CVarDef<float> ExtraChance =
        CVarDef.Create("misfits.material_extractor_extra_chance", 0.5f, CVar.SERVER | CVar.SERVERONLY);
}
