namespace SemiEdu.Engine;

/// <summary>Immutable process inputs created after shared input validation.</summary>
public interface IProcessParameters { }

public sealed record InitParameters(
    string Diameter,
    double? SubstrateThickness,
    string Orientation,
    string DopingType,
    double? BackgroundDoping,
    double? DomainWidth,
    double? Contamination,
    string StartSurface,
    double? InitialOxide) : IProcessParameters
{
    public static implicit operator InitParameters(Recipe recipe) => new(
        recipe.E("diameter"),
        recipe.Has("substrateThickness") ? recipe.N("substrateThickness") : null,
        recipe.E("orientation"),
        recipe.E("dopingType"),
        recipe.Has("backgroundDoping") ? recipe.N("backgroundDoping") : null,
        recipe.Has("domainWidth") ? recipe.N("domainWidth") : null,
        recipe.Has("contamination") ? recipe.N("contamination") : null,
        recipe.E("startSurface"),
        recipe.Has("initialOxide") ? recipe.N("initialOxide") : null);
}

public sealed record OxidationParameters(
    string Method,
    double? Temperature,
    double? Pressure,
    double? Duration,
    double? TargetThickness,
    double? ThicknessTolerance) : IProcessParameters
{
    public static implicit operator OxidationParameters(Recipe recipe) => new(
        recipe.E("method"),
        recipe.Has("temperature") ? recipe.N("temperature") : null,
        recipe.Has("pressure") ? recipe.N("pressure") : null,
        recipe.Has("duration") ? recipe.N("duration") : null,
        recipe.Has("targetThickness") ? recipe.N("targetThickness") : null,
        recipe.Has("thicknessTolerance") ? recipe.N("thicknessTolerance") : null);
}

public sealed record MaskMakingParameters(
    double? DesignClearWidth,
    double? DesignPitch,
    double? Offset,
    double? RepeatCount,
    double? WriteDose,
    double? BeamSize,
    string MaskType,
    double? AbsorberEtchTime,
    double? CrEtchTime,
    double? DevelopTime,
    string MaskLayer,
    string DesignTone,
    string Pellicle,
    double? MaskCdTolerance,
    double? MaxAbsorberResidual) : IProcessParameters
{
    public static implicit operator MaskMakingParameters(Recipe recipe) => new(
        recipe.Has("designClearWidth") ? recipe.N("designClearWidth") : null,
        recipe.Has("designPitch") ? recipe.N("designPitch") : null,
        recipe.Has("offset") ? recipe.N("offset") : null,
        recipe.Has("repeatCount") ? recipe.N("repeatCount") : null,
        recipe.Has("writeDose") ? recipe.N("writeDose") : null,
        recipe.Has("beamSize") ? recipe.N("beamSize") : null,
        recipe.E("maskType"),
        recipe.Has("absorberEtchTime") ? recipe.N("absorberEtchTime") : null,
        recipe.Has("crEtchTime") ? recipe.N("crEtchTime") : null,
        recipe.Has("developTime") ? recipe.N("developTime") : null,
        recipe.E("maskLayer"),
        recipe.E("designTone"),
        recipe.E("pellicle"),
        recipe.Has("maskCdTolerance") ? recipe.N("maskCdTolerance") : null,
        recipe.Has("maxAbsorberResidual") ? recipe.N("maxAbsorberResidual") : null);
}

public sealed record PhotoParameters(
    string MaskId,
    string ExposureMode,
    string ResistTone,
    double? Dose,
    double? DevelopTime,
    double? Focus,
    double? ResistThickness,
    double? TargetCd,
    double? CdTolerance,
    double? MaxPrResidual) : IProcessParameters
{
    public static implicit operator PhotoParameters(Recipe recipe) => new(
        recipe.E("maskId"),
        recipe.E("exposureMode"),
        recipe.E("resistTone"),
        recipe.Has("dose") ? recipe.N("dose") : null,
        recipe.Has("developTime") ? recipe.N("developTime") : null,
        recipe.Has("focus") ? recipe.N("focus") : null,
        recipe.Has("resistThickness") ? recipe.N("resistThickness") : null,
        recipe.Has("targetCd") ? recipe.N("targetCd") : null,
        recipe.Has("cdTolerance") ? recipe.N("cdTolerance") : null,
        recipe.Has("maxPrResidual") ? recipe.N("maxPrResidual") : null);
}

public sealed record EtchParameters(
    string TargetMaterial,
    double? Temperature,
    string Method,
    double? RfPower,
    double? Pressure,
    double? Flow,
    double? Concentration,
    string Scope,
    double? Duration,
    double? TargetEtchDepth,
    double? EtchTolerance) : IProcessParameters
{
    public static implicit operator EtchParameters(Recipe recipe) => new(
        recipe.E("targetMaterial"),
        recipe.Has("temperature") ? recipe.N("temperature") : null,
        recipe.E("method"),
        recipe.Has("rfPower") ? recipe.N("rfPower") : null,
        recipe.Has("pressure") ? recipe.N("pressure") : null,
        recipe.Has("flow") ? recipe.N("flow") : null,
        recipe.Has("concentration") ? recipe.N("concentration") : null,
        recipe.E("scope"),
        recipe.Has("duration") ? recipe.N("duration") : null,
        recipe.Has("targetEtchDepth") ? recipe.N("targetEtchDepth") : null,
        recipe.Has("etchTolerance") ? recipe.N("etchTolerance") : null);
}

public sealed record CleanParameters(
    string Purpose,
    string Method,
    double? Temperature,
    double? Power,
    double? Duration,
    double? Concentration,
    double? Pressure,
    double? MaxContamination,
    double? MaxPrResidual) : IProcessParameters
{
    public static implicit operator CleanParameters(Recipe recipe) => new(
        recipe.E("purpose"),
        recipe.E("method"),
        recipe.Has("temperature") ? recipe.N("temperature") : null,
        recipe.Has("power") ? recipe.N("power") : null,
        recipe.Has("duration") ? recipe.N("duration") : null,
        recipe.Has("concentration") ? recipe.N("concentration") : null,
        recipe.Has("pressure") ? recipe.N("pressure") : null,
        recipe.Has("maxContamination") ? recipe.N("maxContamination") : null,
        recipe.Has("maxPrResidual") ? recipe.N("maxPrResidual") : null);
}

public sealed record DepositionParameters(
    string Method,
    string Material,
    double? Temperature,
    double? Pressure,
    double? Flow,
    double? Duration,
    double? Power,
    double? PulseTime,
    double? Cycles,
    double? PurgeTime,
    double? TargetThickness,
    double? ThicknessTolerance,
    double? MaxPurgePenalty,
    double? MaxNonuniformity) : IProcessParameters
{
    public static implicit operator DepositionParameters(Recipe recipe) => new(
        recipe.E("method"),
        recipe.E("material"),
        recipe.Has("temperature") ? recipe.N("temperature") : null,
        recipe.Has("pressure") ? recipe.N("pressure") : null,
        recipe.Has("flow") ? recipe.N("flow") : null,
        recipe.Has("duration") ? recipe.N("duration") : null,
        recipe.Has("power") ? recipe.N("power") : null,
        recipe.Has("pulseTime") ? recipe.N("pulseTime") : null,
        recipe.Has("cycles") ? recipe.N("cycles") : null,
        recipe.Has("purgeTime") ? recipe.N("purgeTime") : null,
        recipe.Has("targetThickness") ? recipe.N("targetThickness") : null,
        recipe.Has("thicknessTolerance") ? recipe.N("thicknessTolerance") : null,
        recipe.Has("maxPurgePenalty") ? recipe.N("maxPurgePenalty") : null,
        recipe.Has("maxNonuniformity") ? recipe.N("maxNonuniformity") : null);
}

public sealed record CmpParameters(
    string TargetMaterial,
    double? PolishPressure,
    string Slurry,
    double? PadSpeed,
    double? WaferSpeed,
    double? Duration,
    double? TargetThickness,
    double? ThicknessTolerance,
    double? MaxStepHeight) : IProcessParameters
{
    public static implicit operator CmpParameters(Recipe recipe) => new(
        recipe.E("targetMaterial"),
        recipe.Has("polishPressure") ? recipe.N("polishPressure") : null,
        recipe.E("slurry"),
        recipe.Has("padSpeed") ? recipe.N("padSpeed") : null,
        recipe.Has("waferSpeed") ? recipe.N("waferSpeed") : null,
        recipe.Has("duration") ? recipe.N("duration") : null,
        recipe.Has("targetThickness") ? recipe.N("targetThickness") : null,
        recipe.Has("thicknessTolerance") ? recipe.N("thicknessTolerance") : null,
        recipe.Has("maxStepHeight") ? recipe.N("maxStepHeight") : null);
}

public sealed record ImplantParameters(
    string Species,
    double? Energy,
    double? Tilt,
    double? ImplantDose,
    double? TargetDepth,
    double? DepthTolerance) : IProcessParameters
{
    public static implicit operator ImplantParameters(Recipe recipe) => new(
        recipe.E("species"),
        recipe.Has("energy") ? recipe.N("energy") : null,
        recipe.Has("tilt") ? recipe.N("tilt") : null,
        recipe.Has("implantDose") ? recipe.N("implantDose") : null,
        recipe.Has("targetDepth") ? recipe.N("targetDepth") : null,
        recipe.Has("depthTolerance") ? recipe.N("depthTolerance") : null);
}

public sealed record AnnealParameters(
    double? Temperature,
    double? Duration,
    string Method,
    double? MinActivation) : IProcessParameters
{
    public static implicit operator AnnealParameters(Recipe recipe) => new(
        recipe.Has("temperature") ? recipe.N("temperature") : null,
        recipe.Has("duration") ? recipe.N("duration") : null,
        recipe.E("method"),
        recipe.Has("minActivation") ? recipe.N("minActivation") : null);
}

public sealed record MetalParameters(
    string MaskId,
    double? Width,
    double? Offset,
    string Metal,
    string Role,
    double? Thickness,
    double? Length,
    double? MaxResistance) : IProcessParameters
{
    public static implicit operator MetalParameters(Recipe recipe) => new(
        recipe.E("maskId"),
        recipe.Has("width") ? recipe.N("width") : null,
        recipe.Has("offset") ? recipe.N("offset") : null,
        recipe.E("metal"),
        recipe.E("role"),
        recipe.Has("thickness") ? recipe.N("thickness") : null,
        recipe.Has("length") ? recipe.N("length") : null,
        recipe.Has("maxResistance") ? recipe.N("maxResistance") : null);
}

public sealed record ContactParameters(
    string SourceMaskId,
    string DrainMaskId,
    string Metal,
    double? PlugAboveNm) : IProcessParameters
{
    public static implicit operator ContactParameters(Recipe recipe) => new(
        recipe.E("sourceMaskId"),
        recipe.E("drainMaskId"),
        recipe.E("metal"),
        recipe.Has("plugAboveNm") ? recipe.N("plugAboveNm") : null);
}

public sealed record InspectionParameters(
    string InspectionMode,
    string TargetId,
    double? TargetThickness,
    double? ThicknessTolerance,
    double? MaxNonuniformity,
    double? TargetCd,
    double? CdTolerance,
    double? MaxContamination,
    double? MaxResistance,
    double? TargetDepth,
    double? DepthTolerance,
    double? MinActivation) : IProcessParameters
{
    public static implicit operator InspectionParameters(Recipe recipe) => new(
        recipe.E("inspectionMode"),
        recipe.E("targetId"),
        recipe.Has("targetThickness") ? recipe.N("targetThickness") : null,
        recipe.Has("thicknessTolerance") ? recipe.N("thicknessTolerance") : null,
        recipe.Has("maxNonuniformity") ? recipe.N("maxNonuniformity") : null,
        recipe.Has("targetCd") ? recipe.N("targetCd") : null,
        recipe.Has("cdTolerance") ? recipe.N("cdTolerance") : null,
        recipe.Has("maxContamination") ? recipe.N("maxContamination") : null,
        recipe.Has("maxResistance") ? recipe.N("maxResistance") : null,
        recipe.Has("targetDepth") ? recipe.N("targetDepth") : null,
        recipe.Has("depthTolerance") ? recipe.N("depthTolerance") : null,
        recipe.Has("minActivation") ? recipe.N("minActivation") : null);
}

public sealed record TransistorTestParameters(
    double? SweepStart,
    double? SweepStop,
    string TestMode,
    double? Vdd,
    double? Vg,
    double? Vd,
    double? Vs,
    double? Vb,
    string DeviceType,
    double? SweepPoints,
    double? MaxIg,
    double? MinIon,
    double? MaxIoff,
    double? MinOnOff,
    double? MinVth,
    double? MaxVth) : IProcessParameters
{
    public static implicit operator TransistorTestParameters(Recipe recipe) => new(
        recipe.Has("sweepStart") ? recipe.N("sweepStart") : null,
        recipe.Has("sweepStop") ? recipe.N("sweepStop") : null,
        recipe.E("testMode"),
        recipe.Has("vdd") ? recipe.N("vdd") : null,
        recipe.Has("vg") ? recipe.N("vg") : null,
        recipe.Has("vd") ? recipe.N("vd") : null,
        recipe.Has("vs") ? recipe.N("vs") : null,
        recipe.Has("vb") ? recipe.N("vb") : null,
        recipe.E("deviceType"),
        recipe.Has("sweepPoints") ? recipe.N("sweepPoints") : null,
        recipe.Has("maxIg") ? recipe.N("maxIg") : null,
        recipe.Has("minIon") ? recipe.N("minIon") : null,
        recipe.Has("maxIoff") ? recipe.N("maxIoff") : null,
        recipe.Has("minOnOff") ? recipe.N("minOnOff") : null,
        recipe.Has("minVth") ? recipe.N("minVth") : null,
        recipe.Has("maxVth") ? recipe.N("maxVth") : null);
}
