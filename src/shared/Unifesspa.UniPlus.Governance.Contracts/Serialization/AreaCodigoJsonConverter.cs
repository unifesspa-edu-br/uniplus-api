namespace Unifesspa.UniPlus.Governance.Contracts.Serialization;

using System.Text.Json;
using System.Text.Json.Serialization;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Serializa <see cref="AreaCodigo"/> como string JSON plana (não objeto),
/// com fail-fast simétrico: a leitura rejeita token não-string ou valor
/// inválido, e a escrita rejeita <c>default(AreaCodigo)</c> — ambos viram
/// <see cref="JsonException"/> com contexto, em vez de um round-trip
/// silenciosamente inconsistente.
/// </summary>
public sealed class AreaCodigoJsonConverter : JsonConverter<AreaCodigo>
{
    public override AreaCodigo Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException(
                $"AreaCodigo espera um valor string em JSON, recebido {reader.TokenType}.");
        }

        string? bruto = reader.GetString();
        Result<AreaCodigo> resultado = AreaCodigo.From(bruto);
        if (resultado.IsFailure)
        {
            throw new JsonException(
                $"Valor inválido para AreaCodigo: '{bruto}'. {resultado.Error!.Message}");
        }

        return resultado.Value!;
    }

    public override void Write(Utf8JsonWriter writer, AreaCodigo value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        if (value.Value is null)
        {
            throw new JsonException(
                "Não é possível serializar default(AreaCodigo): valor não inicializado. "
                + "AreaCodigo válido só é produzido por AreaCodigo.From.");
        }

        writer.WriteStringValue(value.Value);
    }
}
