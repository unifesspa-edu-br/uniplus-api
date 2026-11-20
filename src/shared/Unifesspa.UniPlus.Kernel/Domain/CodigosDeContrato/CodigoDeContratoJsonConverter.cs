namespace Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Escreve o enum pelo código que o membro declarou em <see cref="CodigoDeContratoAttribute"/> e lê só o código, em
/// texto, como valor e como chave de dicionário. É o conversor dos documentos lidos por máquina: o conteúdo gravado e
/// as mensagens trocadas entre processos.
/// </summary>
/// <typeparam name="TEnum">Enum cujos membros declaram o código.</typeparam>
/// <remarks>
/// Código desconhecido e token que não é texto lançam <see cref="JsonException"/> sem repetir o valor recebido, que
/// pode ser dado de quem enviou a mensagem. O enum inválido lança <see cref="CodigosDeContratoInvalidosException"/>, que
/// não é tratada: o defeito de declaração não é defeito do documento. A fábrica
/// <see cref="CodigoDeContratoJsonConverterFactory"/> cria este conversor; o tipo anulável do enum usa o mesmo
/// conversor, que o serializador embrulha.
/// </remarks>
internal sealed class CodigoDeContratoJsonConverter<TEnum> : JsonConverter<TEnum>
    where TEnum : struct, Enum
{
    /// <inheritdoc />
    /// <exception cref="JsonException">O token não é texto ou o código não existe no enum.</exception>
    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"O código de {typeof(TEnum).Name} deve ser um texto.");
        }

        return Resolver(reader.GetString());
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteStringValue(value.Codigo());
    }

    /// <inheritdoc />
    /// <exception cref="JsonException">O código não existe no enum.</exception>
    public override TEnum ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        Resolver(reader.GetString());

    /// <inheritdoc />
    public override void WriteAsPropertyName(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WritePropertyName(value.Codigo());
    }

    private static TEnum Resolver(string? codigo) =>
        CatalogoDe<TEnum>.TentarObter(codigo, out TEnum valor)
            ? valor
            : throw new JsonException($"O código informado não existe em {typeof(TEnum).Name}.");
}
