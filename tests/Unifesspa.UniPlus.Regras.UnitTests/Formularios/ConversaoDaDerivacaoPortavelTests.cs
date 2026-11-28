namespace Unifesspa.UniPlus.Regras.UnitTests.Formularios;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Leituras;
using Unifesspa.UniPlus.Regras.ValueObjects;

using static Unifesspa.UniPlus.Regras.UnitTests.Formularios.PortavelDeTeste;

/// <summary>A conversão da derivação pelo formulário portável: recusa todas as regras e o código do fato, e só então cria a derivação.</summary>
public sealed class ConversaoDaDerivacaoPortavelTests
{
    private static readonly CondicaoPrecondicaoDto[][] CondicaoValida = [[new("DADO", "IGUAL", JsonSerializer.SerializeToElement(true))]];

    private static Result<DefinicaoFormulario> ComDerivacao(DerivacaoPortavel derivacao) =>
        (Formulario(Item("ELEGIVEL")) with { Derivacoes = [derivacao] }).ParaDefinicao();

    [Fact(DisplayName = "Regra sem quando e código do fato vazio voltam juntos, a regra antes do código, sem exceção")]
    public void Derivacao_RegraSemQuandoECodigoVazio_RecusaAsDuasNessaOrdem()
    {
        DerivacaoPortavel derivacao = new(" ", false, [new RegraDerivacaoPortavel(null!, "A")]);

        Result<DefinicaoFormulario> resultado = ComDerivacao(derivacao);

        resultado.Errors.Select(static e => e.Field).Should().Equal("derivacoes[0].regras[0].quando", "derivacoes[0].fatoCodigo");
        resultado.Errors.Select(static e => e.Error.Code).Should().Equal(
            FormularioPortavelErrorCodes.DerivacaoInvalida, FormularioPortavelErrorCodes.EstruturaInvalida);
    }

    [Fact(DisplayName = "Só o código do fato vazio gera uma recusa, e a derivação não é criada")]
    public void Derivacao_SoCodigoVazio_RecusaSoOCodigo()
    {
        DerivacaoPortavel derivacao = new(" ", true, [new RegraDerivacaoPortavel([], null)]);

        Result<DefinicaoFormulario> resultado = ComDerivacao(derivacao);

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("derivacoes[0].fatoCodigo");
        resultado.IsFailure.Should().BeTrue();
    }

    [Fact(DisplayName = "Condição recusada não confere a contribuição da regra")]
    public void Derivacao_CondicaoRecusada_NaoConfereAContribuicao()
    {
        DerivacaoPortavel derivacao = new("CATEGORIA", false, [new RegraDerivacaoPortavel([[new("", "IGUAL", JsonSerializer.SerializeToElement(true))]], null)]);

        Result<DefinicaoFormulario> resultado = ComDerivacao(derivacao);

        resultado.Errors.Select(static e => e.Field).Should().Equal("derivacoes[0].regras[0].quando[0][0].fato");
    }

    [Fact(DisplayName = "Derivação categórica sem o que a regra contribui aponta a contribuição")]
    public void Derivacao_CategoricaSemContribuicao_ApontaAContribuicao()
    {
        DerivacaoPortavel derivacao = new("CATEGORIA", false, [new RegraDerivacaoPortavel(CondicaoValida, null)]);

        Result<DefinicaoFormulario> resultado = ComDerivacao(derivacao);

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("derivacoes[0].regras[0].contribui");
    }

    [Fact(DisplayName = "Pela definição, a derivação sem regras e a regra recusada apontam o caminho da derivação")]
    public void ParaDefinicao_DerivacaoSemRegras_ApontaADerivacao()
    {
        FormularioPortavel formulario = Formulario(Item("ELEGIVEL")) with { Derivacoes = [new DerivacaoPortavel("COTISTA", true, [])] };

        formulario.ParaDefinicao().Errors.Should().ContainSingle().Which.Field.Should().Be("derivacoes[0]");
    }
}
