using System.Text.Json;
using System.Text.Json.Serialization;
using ShooterRecordsLab.Domain.Enums;

namespace ShooterRecordsLab.Api.Contracts;

internal sealed class OutcomeConverter : JsonConverter<Outcome>
{
    public override Outcome Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("outcome must be Win, Loss or Draw.");
        }

        return reader.GetString() switch
        {
            "Win" => Outcome.Win,
            "Loss" => Outcome.Loss,
            "Draw" => Outcome.Draw,
            _ => throw new JsonException("outcome must be Win, Loss or Draw.")
        };
    }

    public override void Write(Utf8JsonWriter writer, Outcome value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString());
}
