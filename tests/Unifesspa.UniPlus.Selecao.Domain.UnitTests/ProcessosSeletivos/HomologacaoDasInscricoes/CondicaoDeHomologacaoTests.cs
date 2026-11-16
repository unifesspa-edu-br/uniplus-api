namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.HomologacaoDasInscricoes;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;

using Xunit;

/// <summary>Uma condição da regra: o que se apura da inscrição, de que edição do exame, e o motivo desfavorável que ela leva quando não é cumprida.</summary>
public sealed class CondicaoDeHomologacaoTests
{
    [Fact(DisplayName = "A condição do resultado no retorno do INEP guarda a edição do exame e o motivo desfavorável")]
    public void Criar_ResultadoNoRetornoDoInep_GuardaEdicaoEMotivo()
    {
        Result<CondicaoDeHomologacao> resultado = CondicaoDeHomologacao.Criar(
            TipoDeCondicaoDeHomologacao.ResultadoNoRetornoDoInep, edicaoDoExame: 2026, "SEM_RESULTADO_INEP", enquadramento: null);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Tipo.Should().Be(TipoDeCondicaoDeHomologacao.ResultadoNoRetornoDoInep);
        resultado.Value.EdicaoDoExame.Should().Be(2026);
        resultado.Value.MotivoDesfavoravelCodigo.Should().Be("SEM_RESULTADO_INEP");
        resultado.Value.Enquadramento.Should().BeNull();
    }

    [Fact(DisplayName = "O enquadramento opcional é um predicado em forma normal disjuntiva, mantido como veio")]
    public void Criar_ComEnquadramento_GuardaOPredicado()
    {
        PredicadoDnf enquadramento = PredicadoDnf.CriarDeCondicoesAgrupadas(
            [(0, CondicaoDnf.Criar("MODALIDADE", Operador.Igual, JsonSerializer.SerializeToElement("AMPLA")).Value!)]).Value!;

        Result<CondicaoDeHomologacao> resultado = CondicaoDeHomologacao.Criar(
            TipoDeCondicaoDeHomologacao.ResultadoNoRetornoDoInep, 2026, "SEM_RESULTADO_INEP", enquadramento);

        resultado.Value!.Enquadramento.Should().BeSameAs(enquadramento);
    }

    [Theory(DisplayName = "As edições nos limites do intervalo plausível são aceitas")]
    [InlineData(1998)]
    [InlineData(2100)]
    public void Criar_EdicaoNosLimites_Aceita(int edicao)
    {
        Result<CondicaoDeHomologacao> resultado = CondicaoDeHomologacao.Criar(
            TipoDeCondicaoDeHomologacao.ResultadoNoRetornoDoInep, edicao, "SEM_RESULTADO_INEP", enquadramento: null);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Condições com o mesmo conteúdo são iguais; mudar a edição, o motivo ou o enquadramento as distingue")]
    public void MesmoConteudo_ComparaTipoEdicaoMotivoEEnquadramento()
    {
        CondicaoDeHomologacao condicao = CondicoesDeHomologacaoParaTestes.ConfirmacaoDoInep(2026, "SEM_RESULTADO_INEP");

        condicao.MesmoConteudo(CondicoesDeHomologacaoParaTestes.ConfirmacaoDoInep(2026, "SEM_RESULTADO_INEP")).Should().BeTrue();
        condicao.MesmoConteudo(CondicoesDeHomologacaoParaTestes.ConfirmacaoDoInep(2025, "SEM_RESULTADO_INEP")).Should().BeFalse();
        condicao.MesmoConteudo(CondicoesDeHomologacaoParaTestes.ConfirmacaoDoInep(2026, "OUTRO_MOTIVO")).Should().BeFalse();
    }

    [Theory(DisplayName = "Condição sem edição do exame, ou com edição que não é um ano plausível, é recusada")]
    [InlineData(2101)]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-2026)]
    [InlineData(1997)]
    [InlineData(10000)]
    public void Criar_EdicaoDoExameInvalida_Recusa(int? edicao)
    {
        Result<CondicaoDeHomologacao> resultado = CondicaoDeHomologacao.Criar(
            TipoDeCondicaoDeHomologacao.ResultadoNoRetornoDoInep, edicao, "SEM_RESULTADO_INEP", enquadramento: null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(HomologacaoInscricaoErrorCodes.EdicaoDoExameInvalida);
    }

    [Theory(DisplayName = "Condição sem o código do motivo desfavorável, ou com código fora do formato, é recusada com o erro do código do motivo")]
    [InlineData(null, MotivoDeDecisaoErrorCodes.CodigoObrigatorio)]
    [InlineData("  ", MotivoDeDecisaoErrorCodes.CodigoObrigatorio)]
    [InlineData("sem resultado", MotivoDeDecisaoErrorCodes.CodigoFormatoInvalido)]
    public void Criar_MotivoDesfavoravelInvalido_Recusa(string? motivo, string erroEsperado)
    {
        Result<CondicaoDeHomologacao> resultado = CondicaoDeHomologacao.Criar(
            TipoDeCondicaoDeHomologacao.ResultadoNoRetornoDoInep, 2026, motivo, enquadramento: null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(erroEsperado);
    }

    [Fact(DisplayName = "Tipo de condição fora do vocabulário apurável é recusado como não disponível")]
    public void Criar_TipoForaDoVocabulario_ENaoDisponivel()
    {
        Result<CondicaoDeHomologacao> resultado = CondicaoDeHomologacao.Criar(
            (TipoDeCondicaoDeHomologacao)99, 2026, "SEM_RESULTADO_INEP", enquadramento: null);

        resultado.Error!.Code.Should().Be(HomologacaoInscricaoErrorCodes.CondicaoNaoDisponivel);
    }
}
