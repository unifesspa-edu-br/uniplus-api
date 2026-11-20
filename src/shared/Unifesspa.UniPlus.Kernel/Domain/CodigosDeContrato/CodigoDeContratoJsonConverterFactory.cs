namespace Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Converte, pelo código declarado, todo enum em que algum membro declara <see cref="CodigoDeContratoAttribute"/>. O
/// enum sem código não é convertido aqui e continua com o conversor que já o atendia.
/// </summary>
/// <remarks>
/// Só <see cref="OpcoesJsonDeDocumento"/> a instala, e é ele que garante a posição à frente de qualquer conversor de enum
/// por nome. A resposta HTTP publica o enum no formato da resposta e não a instala.
/// </remarks>
internal sealed class CodigoDeContratoJsonConverterFactory : JsonConverterFactory
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
