using System.Text.Json;
using System.Text.Json.Serialization;

namespace SemiEdu.Engine;

// A response owns its explicit ABI field writer. The converter preserves the existing wire schema;
// source-generated metadata selects it without reflection during NativeAOT serialization.
[JsonConverter(typeof(EngineResponseConverter))]
internal sealed record EngineResponse(Action<Utf8JsonWriter> WriteFields);

internal sealed class EngineResponseConverter : JsonConverter<EngineResponse>
{
    public override EngineResponse Read(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions options) => throw new NotSupportedException("Engine responses are output-only.");

    public override void Write(Utf8JsonWriter writer, EngineResponse response, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        response.WriteFields(writer);
        writer.WriteEndObject();
    }
}

[JsonSerializable(typeof(EngineResponse))]
internal partial class EngineJsonContext : JsonSerializerContext;
