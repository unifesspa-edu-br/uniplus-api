namespace Unifesspa.UniPlus.Kernel.Tempo;

using System.Text.Json;
using System.Text.Json.Serialization;

using Unifesspa.UniPlus.Kernel.Extensions;

/// <summary>
/// Lê e escreve todo instante em JSON truncado em microssegundos, a precisão que o banco
/// persiste, no formato padrão do <see cref="DateTimeOffset"/>. Sem arredondar: o 7º dígito é descartado.
/// </summary>
public sealed class InstanteEmMicrossegundosJsonConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetDateTimeOffset().TruncadoEmMicrossegundos();

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteStringValue(value.TruncadoEmMicrossegundos());
    }
}
