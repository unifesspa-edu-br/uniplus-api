namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.HomologacaoDasInscricoes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.HomologacaoDasInscricoes.CondicoesDeHomologacaoParaTestes;

/// <summary>A regra que o edital declara na fase: qual resultado leva a lista e que alternativas de condições homologam a inscrição.</summary>
public sealed class RegraDeHomologacaoDasInscricoesTests
{
    [Fact(DisplayName = "A regra guarda o resultado apontado e as alternativas na ordem declarada")]
    public void Criar_ComResultadoEAlternativas_MantemAOrdem()
    {
        AlternativaDeCondicoes primeira = Alternativa(ConfirmacaoDoInep(2026, "SEM_RESULTADO_INEP"));
        AlternativaDeCondicoes segunda = Alternativa(ConfirmacaoDoInep(2025, "SEM_RESULTADO_INEP_ANTERIOR"));

        Result<RegraDeHomologacaoDasInscricoes> resultado = RegraDeHomologacaoDasInscricoes.Criar("RESULTADO_HOMOLOGACAO", [primeira, segunda]);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.ResultadoAtoCodigo.Should().Be("RESULTADO_HOMOLOGACAO");
        resultado.Value.Alternativas.Should().Equal(primeira, segunda);
    }

    [Fact(DisplayName = "Regra sem alternativa é aceita: a fase homologa toda inscrição enviada, e o aviso é do painel de conformidade")]
    public void Criar_SemAlternativas_EAceita()
    {
        Result<RegraDeHomologacaoDasInscricoes> resultado = RegraDeHomologacaoDasInscricoes.Criar("RESULTADO_HOMOLOGACAO", []);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.HomologaTodaInscricaoEnviada.Should().BeTrue();
    }

    [Fact(DisplayName = "Regra com alternativas não homologa toda inscrição enviada")]
    public void Criar_ComAlternativas_NaoHomologaTodaInscricao()
    {
        Result<RegraDeHomologacaoDasInscricoes> resultado = RegraDeHomologacaoDasInscricoes.Criar(
            "RESULTADO_HOMOLOGACAO", [Alternativa(ConfirmacaoDoInep(2026, "SEM_RESULTADO_INEP"))]);

        resultado.Value!.HomologaTodaInscricaoEnviada.Should().BeFalse();
    }

    [Theory(DisplayName = "Regra sem o resultado apontado é recusada como resultado não declarado")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_SemResultadoApontado_ResultadoNaoDeclarado(string? resultadoAtoCodigo)
    {
        Result<RegraDeHomologacaoDasInscricoes> resultado = RegraDeHomologacaoDasInscricoes.Criar(resultadoAtoCodigo, []);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(HomologacaoDasInscricoesErrorCodes.ResultadoNaoDeclarado);
    }

    [Fact(DisplayName = "Regras com as mesmas alternativas na mesma ordem têm o mesmo conteúdo; a ordem diferente as distingue")]
    public void MesmoConteudo_ConsideraAOrdemDasAlternativas()
    {
        AlternativaDeCondicoes atual = Alternativa(ConfirmacaoDoInep(2026, "SEM_RESULTADO_INEP"));
        AlternativaDeCondicoes anterior = Alternativa(ConfirmacaoDoInep(2025, "SEM_RESULTADO_INEP_ANTERIOR"));
        RegraDeHomologacaoDasInscricoes regra = RegraDeHomologacaoDasInscricoes.Criar("RESULTADO_HOMOLOGACAO", [atual, anterior]).Value!;

        regra.MesmoConteudo(RegraDeHomologacaoDasInscricoes.Criar("RESULTADO_HOMOLOGACAO", [atual, anterior]).Value!).Should().BeTrue();
        regra.MesmoConteudo(RegraDeHomologacaoDasInscricoes.Criar("RESULTADO_HOMOLOGACAO", [anterior, atual]).Value!).Should().BeFalse();
        regra.MesmoConteudo(RegraDeHomologacaoDasInscricoes.Criar("OUTRO_RESULTADO", [atual, anterior]).Value!).Should().BeFalse();
    }

    [Fact(DisplayName = "Regra com alternativa nula é recusada")]
    public void Criar_ComAlternativaNula_Lanca()
    {
        Action criar = () => RegraDeHomologacaoDasInscricoes.Criar("RESULTADO_HOMOLOGACAO", [null!]);

        criar.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "O código do resultado apontado é guardado sem os espaços das pontas")]
    public void Criar_ResultadoComEspacos_ENormalizado()
    {
        Result<RegraDeHomologacaoDasInscricoes> resultado = RegraDeHomologacaoDasInscricoes.Criar("  RESULTADO_HOMOLOGACAO ", []);

        resultado.Value!.ResultadoAtoCodigo.Should().Be("RESULTADO_HOMOLOGACAO");
    }
}
