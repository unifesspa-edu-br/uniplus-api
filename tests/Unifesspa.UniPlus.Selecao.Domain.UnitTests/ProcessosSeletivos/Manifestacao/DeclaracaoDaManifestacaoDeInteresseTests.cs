namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Manifestacao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Manifestacao;

/// <summary>
/// A manifestação de interesse é um só valor de contrato (UNI-REQ-0503): sem valor, não é prevista e a declaração vale; os dois
/// códigos preveem e exigem a manifestação; o código que o catálogo não conhece é recusado no campo, sem repetir o valor.
/// </summary>
public sealed class DeclaracaoDaManifestacaoDeInteresseTests
{
    [Fact(DisplayName = "Sem código, a manifestação não é prevista e a declaração é válida")]
    public void SemCodigo_NaoPrevista()
    {
        Result<ManifestacaoDeInteresse?> resultado = DeclaracaoDaManifestacaoDeInteresse.Declarar(null);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().BeNull();
    }

    [Theory(DisplayName = "Cada código de contrato declara a manifestação que prevê")]
    [InlineData("UMA_VEZ", ManifestacaoDeInteresse.UmaVez)]
    [InlineData("CONFIRMA_A_CADA_CHAMADA", ManifestacaoDeInteresse.ConfirmaACadaChamada)]
    public void CodigoDeContrato_DeclaraAManifestacao(string codigo, ManifestacaoDeInteresse esperada)
    {
        Result<ManifestacaoDeInteresse?> resultado = DeclaracaoDaManifestacaoDeInteresse.Declarar(codigo);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().Be(esperada);
    }

    [Theory(DisplayName = "O código fora do catálogo é recusado no campo da manifestação")]
    [InlineData("OUTRA")]
    [InlineData("uma_vez")]
    [InlineData(" UMA_VEZ")]
    [InlineData("")]
    [InlineData("NAO_PREVISTA")]
    public void CodigoDesconhecido_Recusado(string codigo)
    {
        Result<ManifestacaoDeInteresse?> resultado = DeclaracaoDaManifestacaoDeInteresse.Declarar(codigo);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(static erro =>
            erro.Field == "manifestacaoDeInteresse" && erro.Error.Code == ManifestacaoDeInteresseErrorCodes.CodigoDesconhecido);
    }

    [Fact(DisplayName = "A recusa lista as duas opções e não repete o código informado")]
    public void Recusa_ListaOpcoes_SemRepetirOValor()
    {
        Result<ManifestacaoDeInteresse?> resultado = DeclaracaoDaManifestacaoDeInteresse.Declarar("VALOR_SECRETO_9876");

        string mensagem = resultado.Error!.Message;
        mensagem.Should().NotContain("VALOR_SECRETO_9876");
        mensagem.Should().Contain("Manifesta interesse uma só vez").And.Contain("Confirma o interesse a cada chamada");
    }

    [Fact(DisplayName = "O catálogo tem só os dois códigos e nenhum diz que a manifestação não é prevista")]
    public void Catalogo_TemSoOsDoisCodigos() =>
        CatalogoDe<ManifestacaoDeInteresse>.Todos().Select(static opcao => opcao.Codigo)
            .Should().Equal("UMA_VEZ", "CONFIRMA_A_CADA_CHAMADA");
}
