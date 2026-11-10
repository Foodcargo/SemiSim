namespace SemiconductorEdu.Domain.Units;

public readonly record struct Length
{
    private Length(double meters) => Meters = EnsureFinite(meters);

    public double Meters { get; }
    public double Nanometers => Meters / 1e-9;
    public double Micrometers => Meters / 1e-6;

    public static Length FromMeters(double value) => new(value);
    public static Length FromNanometers(double value) => new(value * 1e-9);
    public static Length FromMicrometers(double value) => new(value * 1e-6);

    private static double EnsureFinite(double value) => double.IsFinite(value) ? value : throw new ArgumentOutOfRangeException(nameof(value));
}

public readonly record struct Duration
{
    private Duration(double seconds) => Seconds = EnsureFinite(seconds);

    public double Seconds { get; }
    public double Minutes => Seconds / 60.0;

    public static Duration FromSeconds(double value) => new(value);
    public static Duration FromMinutes(double value) => new(value * 60.0);

    private static double EnsureFinite(double value) => double.IsFinite(value) ? value : throw new ArgumentOutOfRangeException(nameof(value));
}

public readonly record struct Pressure
{
    private const double TorrToPascal = 133.322368;
    private const double MilliTorrToPascal = 0.133322368;

    private Pressure(double pascals) => Pascals = EnsureFinite(pascals);

    public double Pascals { get; }
    public double Torr => Pascals / TorrToPascal;
    public double MilliTorr => Pascals / MilliTorrToPascal;

    public static Pressure FromPascals(double value) => new(value);
    public static Pressure FromTorr(double value) => new(value * TorrToPascal);
    public static Pressure FromMilliTorr(double value) => new(value * MilliTorrToPascal);

    private static double EnsureFinite(double value) => double.IsFinite(value) ? value : throw new ArgumentOutOfRangeException(nameof(value));
}

public readonly record struct Temperature
{
    private Temperature(double kelvin) => Kelvin = EnsureFinite(kelvin);

    public double Kelvin { get; }
    public double Celsius => Kelvin - 273.15;

    public static Temperature FromKelvin(double value) => new(value);
    public static Temperature FromCelsius(double value) => new(value + 273.15);

    private static double EnsureFinite(double value) => double.IsFinite(value) ? value : throw new ArgumentOutOfRangeException(nameof(value));
}

public readonly record struct Concentration
{
    private Concentration(double perCubicMeter) => PerCubicMeter = EnsureFinite(perCubicMeter);

    public double PerCubicMeter { get; }
    public double PerCubicCentimeter => PerCubicMeter / 1e6;

    public static Concentration FromPerCubicMeter(double value) => new(value);
    public static Concentration FromPerCubicCentimeter(double value) => new(value * 1e6);

    private static double EnsureFinite(double value) => double.IsFinite(value) ? value : throw new ArgumentOutOfRangeException(nameof(value));
}

public readonly record struct ArealDose
{
    private ArealDose(double perSquareMeter) => PerSquareMeter = EnsureFinite(perSquareMeter);

    public double PerSquareMeter { get; }
    public double IonsPerSquareCentimeter => PerSquareMeter / 1e4;

    public static ArealDose FromPerSquareMeter(double value) => new(value);
    public static ArealDose FromIonsPerSquareCentimeter(double value) => new(value * 1e4);

    private static double EnsureFinite(double value) => double.IsFinite(value) ? value : throw new ArgumentOutOfRangeException(nameof(value));
}

public readonly record struct Energy
{
    private Energy(double joules) => Joules = EnsureFinite(joules);

    public double Joules { get; }
    public double KiloElectronVolts => Joules / 1.602176634e-16;

    public static Energy FromJoules(double value) => new(value);
    public static Energy FromKiloElectronVolts(double value) => new(value * 1.602176634e-16);

    private static double EnsureFinite(double value) => double.IsFinite(value) ? value : throw new ArgumentOutOfRangeException(nameof(value));
}

public readonly record struct AngularSpeed
{
    private AngularSpeed(double radiansPerSecond) => RadiansPerSecond = EnsureFinite(radiansPerSecond);

    public double RadiansPerSecond { get; }
    public double RevolutionsPerMinute => RadiansPerSecond * 60.0 / (2.0 * Math.PI);

    public static AngularSpeed FromRadiansPerSecond(double value) => new(value);
    public static AngularSpeed FromRevolutionsPerMinute(double value) => new(value * (2.0 * Math.PI / 60.0));

    private static double EnsureFinite(double value) => double.IsFinite(value) ? value : throw new ArgumentOutOfRangeException(nameof(value));
}

public readonly record struct Sccm
{
    private Sccm(double value) => Value = EnsureFinite(value);

    public double Value { get; }
    public static Sccm FromValue(double value) => new(value);

    private static double EnsureFinite(double value) => double.IsFinite(value) ? value : throw new ArgumentOutOfRangeException(nameof(value));
}

public readonly record struct Percent
{
    private Percent(double value) => Value = EnsureFinite(value);

    public double Value { get; }
    public double Ratio => Value / 100.0;
    public static Percent FromValue(double value) => new(value);

    private static double EnsureFinite(double value) => double.IsFinite(value) ? value : throw new ArgumentOutOfRangeException(nameof(value));
}
