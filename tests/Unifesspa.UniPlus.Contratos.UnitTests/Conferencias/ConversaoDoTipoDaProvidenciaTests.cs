namespace Unifesspa.UniPlus.Contratos.UnitTests.Conferencias;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Converters;

public sealed class ConversaoDoTipoDaProvidenciaTests
{
    private readonly CodigoDeContratoValueConverter<TipoDaProvidencia> _conversor = new();

    [Theory(DisplayName = "A leitura aceita os códigos exatos dos tipos de providência")]
    [InlineData("RETIRADA", TipoDaProvidencia.Retirada)]
    [InlineData("REVISAO", TipoDaProvidencia.Revisao)]
    public void ConvertFromProvider_CodigoDeclarado_DevolveOTipo(string codigo, TipoDaProvidencia tipoEsperado) =>
        _conversor.ConvertFromProvider(codigo).Should().Be(tipoEsperado);

    [Theory(DisplayName = "A leitura recusa códigos com outra grafia ou desconhecidos")]
    [InlineData("retirada")]
    [InlineData(" RETIRADA")]
    [InlineData("RETIRADA ")]
    [InlineData("RETIRADA DE ACESSO")]
    [InlineData("RETIRADA_DE_ACESSO")]
    [InlineData("DESCONHECIDO")]
    public void ConvertFromProvider_CodigoNaoDeclarado_LancaArgumentException(string codigo) =>
        FluentActions.Invoking(() => _conversor.ConvertFromProvider(codigo)).Should().Throw<ArgumentException>();
}
