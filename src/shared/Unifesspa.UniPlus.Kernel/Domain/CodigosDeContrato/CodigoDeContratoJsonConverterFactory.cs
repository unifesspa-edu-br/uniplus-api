namespace Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Converte, pelo código declarado, todo enum em que algum membro declara <see cref="CodigoDeContratoAttribute"/>. O
/// enum sem código não é convertido aqui e continua com o conversor que já o atendia.
/// </summary>
/// <remarks>
/// Registre a fábrica antes de qualquer conversor de enum por nome, como <see cref="JsonStringEnumConverter"/>: o
/// serializador usa o primeiro conversor que aceita o tipo. Esta fábrica serve ao documento lido por máquina; a resposta
/// HTTP publica o enum no formato da resposta e não a registra.
/// </remarks>
public sealed class CodigoDeContratoJsonConverterFactory : JsonConverterFactory
{
    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert) =>
        EnumsDeContrato.EhEnumQueDeclaraCodigo(typeToConvert);

    /// <inheritdoc />
    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(typeToConvert);

        return (JsonConverter?)Activator.CreateInstance(typeof(CodigoDeContratoJsonConverter<>).MakeGenericType(typeToConvert));
    }
}
