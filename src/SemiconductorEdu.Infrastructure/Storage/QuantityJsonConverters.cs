using System.Text.Json;
using System.Text.Json.Serialization;
using SemiconductorEdu.Domain.Units;

namespace SemiconductorEdu.Infrastructure.Storage;

public sealed class LengthJsonConverter : JsonConverter<Length>
{
    public override Length Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => Length.FromMeters(reader.GetDouble());
    public override void Write(Utf8JsonWriter writer, Length value, JsonSerializerOptions options) => writer.WriteNumberValue(value.Meters);
}

public sealed class DurationJsonConverter : JsonConverter<Duration>
{
    public override Duration Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => Duration.FromSeconds(reader.GetDouble());
    public override void Write(Utf8JsonWriter writer, Duration value, JsonSerializerOptions options) => writer.WriteNumberValue(value.Seconds);
}

public sealed class PressureJsonConverter : JsonConverter<Pressure>
{
    public override Pressure Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => Pressure.FromPascals(reader.GetDouble());
    public override void Write(Utf8JsonWriter writer, Pressure value, JsonSerializerOptions options) => writer.WriteNumberValue(value.Pascals);
}

public sealed class TemperatureJsonConverter : JsonConverter<Temperature>
{
    public override Temperature Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => Temperature.FromKelvin(reader.GetDouble());
    public override void Write(Utf8JsonWriter writer, Temperature value, JsonSerializerOptions options) => writer.WriteNumberValue(value.Kelvin);
}

public sealed class ConcentrationJsonConverter : JsonConverter<Concentration>
{
    public override Concentration Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => Concentration.FromPerCubicMeter(reader.GetDouble());
    public override void Write(Utf8JsonWriter writer, Concentration value, JsonSerializerOptions options) => writer.WriteNumberValue(value.PerCubicMeter);
}

public sealed class ArealDoseJsonConverter : JsonConverter<ArealDose>
{
    public override ArealDose Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => ArealDose.FromPerSquareMeter(reader.GetDouble());
    public override void Write(Utf8JsonWriter writer, ArealDose value, JsonSerializerOptions options) => writer.WriteNumberValue(value.PerSquareMeter);
}

public sealed class EnergyJsonConverter : JsonConverter<Energy>
{
    public override Energy Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => Energy.FromJoules(reader.GetDouble());
    public override void Write(Utf8JsonWriter writer, Energy value, JsonSerializerOptions options) => writer.WriteNumberValue(value.Joules);
}

public sealed class AngularSpeedJsonConverter : JsonConverter<AngularSpeed>
{
    public override AngularSpeed Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => AngularSpeed.FromRadiansPerSecond(reader.GetDouble());
    public override void Write(Utf8JsonWriter writer, AngularSpeed value, JsonSerializerOptions options) => writer.WriteNumberValue(value.RadiansPerSecond);
}

public sealed class SccmJsonConverter : JsonConverter<Sccm>
{
    public override Sccm Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => Sccm.FromValue(reader.GetDouble());
    public override void Write(Utf8JsonWriter writer, Sccm value, JsonSerializerOptions options) => writer.WriteNumberValue(value.Value);
}

public sealed class PercentJsonConverter : JsonConverter<Percent>
{
    public override Percent Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => Percent.FromValue(reader.GetDouble());
    public override void Write(Utf8JsonWriter writer, Percent value, JsonSerializerOptions options) => writer.WriteNumberValue(value.Value);
}
