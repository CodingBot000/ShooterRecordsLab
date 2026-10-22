using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShooterRecordsLab.Api.Contracts;

internal sealed class UtcDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("A UTC ISO 8601 timestamp is required.");
        }

        var text = reader.GetString();
        if (text is null || (!text.EndsWith('Z') && !text.EndsWith("+00:00", StringComparison.Ordinal)) ||
            !reader.TryGetDateTimeOffset(out var value) || value.Offset != TimeSpan.Zero)
        {
            throw new JsonException("A UTC ISO 8601 timestamp with Z or +00:00 is required.");
        }

        return value;
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString("O", CultureInfo.InvariantCulture));
}
