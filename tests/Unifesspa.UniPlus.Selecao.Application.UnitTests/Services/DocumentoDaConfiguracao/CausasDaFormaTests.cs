namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.DocumentoDaConfiguracao;

using AwesomeAssertions;

using Kernel.Results;

using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

public sealed class CausasDaFormaTests
{
    private const string Ponteiro = "/etapas/0/segredo";

    public static TheoryData<FieldError, string?, string> Causas => new()
    {
        { CausasDaForma.Malformado(), null, DocumentoDaConfiguracaoErrorCodes.FormaInvalida },
        { CausasDaForma.RaizNaoEhObjeto(), string.Empty, DocumentoDaConfiguracaoErrorCodes.TipoInvalido },
        { CausasDaForma.TamanhoAcimaDoLimite(), null, DocumentoDaConfiguracaoErrorCodes.AcimaDoLimite },
        { CausasDaForma.QuantidadeDeNosAcimaDoLimite(), string.Empty, DocumentoDaConfiguracaoErrorCodes.AcimaDoLimite },
        { CausasDaForma.ProfundidadeAcimaDoLimite(Ponteiro), Ponteiro, DocumentoDaConfiguracaoErrorCodes.AcimaDoLimite },
        { CausasDaForma.PropriedadeRepetida(Ponteiro), Ponteiro, DocumentoDaConfiguracaoErrorCodes.PropriedadeRepetida },
        { CausasDaForma.ChaveRepetida(Ponteiro), Ponteiro, DocumentoDaConfiguracaoErrorCodes.ChaveRepetida },
        { CausasDaForma.CampoDesconhecido(Ponteiro), Ponteiro, DocumentoDaConfiguracaoErrorCodes.CampoDesconhecido },
        { CausasDaForma.TipoInvalido(Ponteiro, "texto"), Ponteiro, DocumentoDaConfiguracaoErrorCodes.TipoInvalido },
        { CausasDaForma.ValorInvalido(Ponteiro, "deve ser um dos valores permitidos"), Ponteiro, DocumentoDaConfiguracaoErrorCodes.ValorInvalido },
        { CausasDaForma.CampoObrigatorioAusente(Ponteiro), Ponteiro, DocumentoDaConfiguracaoErrorCodes.CampoObrigatorioAusente },
        { CausasDaForma.VersaoDoFormatoNaoSuportada(Ponteiro), Ponteiro, DocumentoDaConfiguracaoErrorCodes.VersaoDoFormatoNaoSuportada },
    };

    [Theory(DisplayName = "Cada causa de forma aponta o ponteiro do item, ou nenhum campo quando não é do documento, e tem o código de domínio da própria causa")]
    [MemberData(nameof(Causas))]
    public void Causa_ApontaOPonteiroETemOCodigoDaCausa(FieldError causa, string? ponteiro, string codigo)
    {
        ArgumentNullException.ThrowIfNull(causa);

        causa.Field.Should().Be(ponteiro);
        causa.Error.Code.Should().Be(codigo);
        causa.Error.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Fact(DisplayName = "A mensagem do tipo inválido diz o tipo esperado")]
    public void TipoInvalido_DizOTipoEsperado() =>
        CausasDaForma.TipoInvalido(Ponteiro, "número inteiro").Error.Message.Should().Contain("número inteiro");

    [Fact(DisplayName = "As mensagens dos limites dizem o limite")]
    public void Limites_DizemOLimite()
    {
        CausasDaForma.TamanhoAcimaDoLimite().Error.Message.Should().Contain("2 MiB");
        CausasDaForma.ProfundidadeAcimaDoLimite(Ponteiro).Error.Message.Should().Contain("32");
        CausasDaForma.QuantidadeDeNosAcimaDoLimite().Error.Message.Should().Contain("10000");
    }

    [Fact(DisplayName = "As causas do caractere nulo são valor inválido no ponteiro recebido e dizem a regra sem repetir texto")]
    public void CaractereNulo_EValorInvalidoNoPonteiroRecebido()
    {
        FieldError[] causas = [CausasDaForma.CaractereNuloNoValor(Ponteiro), CausasDaForma.CaractereNuloNoNomeDaPropriedade(Ponteiro)];

        causas.Should().OnlyContain(static causa => causa.Field == Ponteiro && causa.Error.Code == DocumentoDaConfiguracaoErrorCodes.ValorInvalido);
        causas.Should().OnlyContain(static causa => causa.Error.Message.Contains("U+0000", StringComparison.Ordinal));
    }
}
