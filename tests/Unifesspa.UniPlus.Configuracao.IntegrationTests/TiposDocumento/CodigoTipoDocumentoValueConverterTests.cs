namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposDocumento;

using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;

/// <summary>
/// Conversão do código do tipo de documento entre o value object e a coluna
/// <c>varchar</c>. O caminho de ida é trivial; o de volta é que carrega a
/// decisão: reidratar um valor que o value object recusaria falha na hora, com
/// contexto, em vez de devolver <c>null</c> e estourar mais tarde longe da causa.
/// </summary>
/// <remarks>
/// O estado que este teste exercita não se cria pelo fluxo normal — o CHECK
/// <c>ck_tipo_documento_codigo_formato</c> recusa o insert cru e o agregado recusa
/// a escrita. Ele existe para o caso que sobra: alteração manual da coluna.
/// </remarks>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class CodigoTipoDocumentoValueConverterTests
{
    private static readonly CodigoTipoDocumentoValueConverter Converter = new();

    [Theory(DisplayName = "Reidratação aceita o que o value object aceitaria")]
    [InlineData("RG")]
    [InlineData("LAUDO_MEDICO")]
    [InlineData("DECLARACAO_IRPF_2025")]
    public void Reidratar_ValorCanonico_DevolveValueObject(string gravado)
    {
        CodigoTipoDocumento reidratado = ConverterDeProvider(gravado);

        reidratado.Valor.Should().Be(gravado);
    }

    [Theory(DisplayName = "Reidratação de valor fora do formato falha com contexto, sem devolver nulo")]
    [InlineData("01")]
    [InlineData("laudo_medico")]
    [InlineData("LAUDO-MEDICO")]
    [InlineData("L")]
    public void Reidratar_ValorForaDoFormato_Lanca(string corrompido)
    {
        Action act = () => ConverterDeProvider(corrompido);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*CodigoTipoDocumento*")
            .WithMessage("*alteração manual*");
    }

    [Fact(DisplayName = "Gravação usa o valor canônico do value object")]
    public void Gravar_UsaValorCanonico()
    {
        CodigoTipoDocumento codigo = CodigoTipoDocumento.Criar("LAUDO_MEDICO").Value!;

        object? gravado = Converter.ConvertToProvider(codigo);

        gravado.Should().Be("LAUDO_MEDICO");
    }

    private static CodigoTipoDocumento ConverterDeProvider(string valor)
    {
        // A expressão compilada é a mesma que o EF usa na materialização; invocá-la
        // diretamente exercita o caminho real, sem precisar corromper uma coluna.
        Func<string, CodigoTipoDocumento> materializar =
            ((Expression<Func<string, CodigoTipoDocumento>>)Converter.ConvertFromProviderExpression).Compile();

        return materializar(valor);
    }
}
