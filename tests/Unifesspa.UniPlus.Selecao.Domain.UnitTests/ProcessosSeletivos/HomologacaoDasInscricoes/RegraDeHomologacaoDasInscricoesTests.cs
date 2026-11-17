namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.HomologacaoDasInscricoes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.HomologacaoDasInscricoes.CondicoesDeHomologacaoParaTestes;

/// <summary>A regra que o edital declara na fase: qual resultado leva a lista e que alternativas de condições homologam a inscrição.</summary>
public sealed class RegraDeHomologacaoDasInscricoesTests
{
    [Fact(DisplayName = "A regra guarda o resultado apontado e as alternativas na ordem declarada")]
    public void Declarar_ComResultadoEAlternativas_MantemAOrdem()
    {
        AlternativaDeCondicoes primeira = Alternativa(ConfirmacaoDoInep(2026, "SEM_RESULTADO_INEP"));
        AlternativaDeCondicoes segunda = Alternativa(ConfirmacaoDoInep(2025, "SEM_RESULTADO_INEP_ANTERIOR"));

        RegraDeHomologacaoDasInscricoes regra = RegraDeHomologacaoDasInscricoes.Declarar("RESULTADO_HOMOLOGACAO", [primeira, segunda]);

        regra.ResultadoAtoCodigo.Should().Be("RESULTADO_HOMOLOGACAO");
        regra.Alternativas.Should().Equal(primeira, segunda);
    }

    [Fact(DisplayName = "Regra sem alternativa é aceita: a fase homologa toda inscrição enviada, e o aviso é do painel de conformidade")]
    public void Declarar_SemAlternativas_HomologaTodaInscricaoEnviada()
    {
        RegraDeHomologacaoDasInscricoes regra = RegraDeHomologacaoDasInscricoes.Declarar("RESULTADO_HOMOLOGACAO", []);

        regra.HomologaTodaInscricaoEnviada.Should().BeTrue();
    }

    [Fact(DisplayName = "Regra com alternativas não homologa toda inscrição enviada")]
    public void Declarar_ComAlternativas_NaoHomologaTodaInscricao()
    {
        RegraDeHomologacaoDasInscricoes regra = RegraDeHomologacaoDasInscricoes.Declarar(
            "RESULTADO_HOMOLOGACAO", [Alternativa(ConfirmacaoDoInep(2026, "SEM_RESULTADO_INEP"))]);

        regra.HomologaTodaInscricaoEnviada.Should().BeFalse();
    }

    [Theory(DisplayName = "Regra sem o resultado apontado é declarada com o resultado ausente, sem recusa própria")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Declarar_SemResultadoApontado_GuardaAAusencia(string? resultadoAtoCodigo)
    {
        RegraDeHomologacaoDasInscricoes regra = RegraDeHomologacaoDasInscricoes.Declarar(resultadoAtoCodigo, []);

        regra.ResultadoAtoCodigo.Should().BeNull();
    }

    [Fact(DisplayName = "Regras com as mesmas alternativas na mesma ordem têm o mesmo conteúdo; a ordem diferente as distingue")]
    public void MesmoConteudo_ConsideraAOrdemDasAlternativas()
    {
        AlternativaDeCondicoes atual = Alternativa(ConfirmacaoDoInep(2026, "SEM_RESULTADO_INEP"));
        AlternativaDeCondicoes anterior = Alternativa(ConfirmacaoDoInep(2025, "SEM_RESULTADO_INEP_ANTERIOR"));
        RegraDeHomologacaoDasInscricoes regra = RegraDeHomologacaoDasInscricoes.Declarar("RESULTADO_HOMOLOGACAO", [atual, anterior]);

        regra.MesmoConteudo(RegraDeHomologacaoDasInscricoes.Declarar("RESULTADO_HOMOLOGACAO", [atual, anterior])).Should().BeTrue();
        regra.MesmoConteudo(RegraDeHomologacaoDasInscricoes.Declarar("RESULTADO_HOMOLOGACAO", [anterior, atual])).Should().BeFalse();
    }

    [Fact(DisplayName = "Regras que apontam resultados diferentes, ou um resultado e nenhum, não têm o mesmo conteúdo")]
    public void MesmoConteudo_ConsideraOResultadoApontado()
    {
        RegraDeHomologacaoDasInscricoes regra = RegraDeHomologacaoDasInscricoes.Declarar("RESULTADO_HOMOLOGACAO", []);

        regra.MesmoConteudo(RegraDeHomologacaoDasInscricoes.Declarar("OUTRO_RESULTADO", [])).Should().BeFalse();
        regra.MesmoConteudo(RegraDeHomologacaoDasInscricoes.Declarar(null, [])).Should().BeFalse();
        RegraDeHomologacaoDasInscricoes.Declarar(null, []).MesmoConteudo(RegraDeHomologacaoDasInscricoes.Declarar(" ", [])).Should().BeTrue();
    }

    [Fact(DisplayName = "Regra com alternativa nula é recusada")]
    public void Declarar_ComAlternativaNula_Lanca()
    {
        Action declarar = () => RegraDeHomologacaoDasInscricoes.Declarar("RESULTADO_HOMOLOGACAO", [null!]);

        declarar.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "O código do resultado apontado é guardado sem os espaços das pontas")]
    public void Declarar_ResultadoComEspacos_ENormalizado()
    {
        RegraDeHomologacaoDasInscricoes regra = RegraDeHomologacaoDasInscricoes.Declarar("  RESULTADO_HOMOLOGACAO ", []);

        regra.ResultadoAtoCodigo.Should().Be("RESULTADO_HOMOLOGACAO");
    }
}
