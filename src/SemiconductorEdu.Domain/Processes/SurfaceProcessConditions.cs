using SemiconductorEdu.Domain.State;
using SemiconductorEdu.Domain.Units;
namespace SemiconductorEdu.Domain.Processes;
public enum ResistTone { Positive, Negative }
public enum ExposureMode { DUV, EUV }
public enum SurfaceMethod { Dry, Wet }
public enum EtchScope { Patterned, Blanket }
public enum CleanPurpose { SurfaceClean, PRStrip }
public enum CmpSlurry { Oxide, Metal }
public sealed record PhotoConditions(ResistTone Tone, ExposureMode Exposure, Length ResistThickness,
    double Dose, Length Focus, Length ClearWidth, Length Pitch, Length Offset, Duration DevelopTime) : IProcessParameters;
public sealed record EtchConditions(SurfaceMethod Method, WaferMaterial TargetMaterial, string Chemistry, EtchScope Scope,
    Duration Duration, Temperature Temperature, Pressure? Pressure = null, double? RfPower = null,
    Sccm? Flow = null, Percent? Concentration = null) : IProcessParameters;
public sealed record CleanConditions(SurfaceMethod Method, CleanPurpose Purpose, string Chemistry,
    Temperature Temperature, Duration Duration, Percent? Concentration = null,
    double? Power = null, Pressure? Pressure = null) : IProcessParameters;
public sealed record CmpConditions(CmpSlurry Slurry, WaferMaterial TargetMaterial, Pressure PolishPressure,
    AngularSpeed PadSpeed, AngularSpeed WaferSpeed, Duration Duration) : IProcessParameters;
