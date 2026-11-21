namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.Bancas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class MontagemDaRegraDeDeliberacaoTests
{
    [Fact(DisplayName = "Regra ausente é rascunho: a montagem a aceita sem regra")]
    public void Montar_SemRegraDeclarada_AceitaSemRegra()
    {
        Result<RegraDeDeliberacao?> resultado = MontagemDaRegraDeDeliberacao.Montar(null, "bancas[0]");

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().BeNull();
    }

    [Fact(DisplayName = "Regra válida é montada com o que o edital declarou")]
    public void Montar_RegraValida_MontaARegra()
    {
        Result<RegraDeDeliberacao?> resultado = MontagemDaRegraDeDeliberacao.Montar(RegrasDeDeliberacaoDeTeste.Valida(), "bancas[0]");

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Declaracao().Should().BeEquivalentTo(RegrasDeDeliberacaoDeTeste.Valida().ParaDeclaracao());
    }

    [Fact(DisplayName = "Cada violação da regra sai sob o caminho da banca, e todas juntas")]
    public void Montar_RegraComViolacoes_AcumulaOsCamposSobOCaminhoDaBanca()
    {
        Result<RegraDeDeliberacao?> resultado = MontagemDaRegraDeDeliberacao.Montar(
            RegrasDeDeliberacaoDeTeste.ComTresViolacoes(), "fases[1].bancasRequeridas[2]");

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(static erro => erro.Field).Should().BeEquivalentTo(
        [
            "fases[1].bancasRequeridas[2].regraDeDeliberacao.integrantes",
            "fases[1].bancasRequeridas[2].regraDeDeliberacao.criterio",
            "fases[1].bancasRequeridas[2].regraDeDeliberacao.encerramento",
        ]);
    }

    [Fact(DisplayName = "O ato individual sem votação do recurso é montado como veio")]
    public void Montar_AtoIndividualSemVotacao_MontaARegra()
    {
        Result<RegraDeDeliberacao?> resultado = MontagemDaRegraDeDeliberacao.Montar(RegrasDeDeliberacaoDeTeste.AtoIndividual(), "bancas[0]");

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Quorum.Should().BeNull();
    }

    [Fact(DisplayName = "O ato individual com a votação do recurso inteira é montado com o quórum declarado")]
    public void Montar_AtoIndividualComVotacaoInteira_MontaARegraComOQuorum()
    {
        Result<RegraDeDeliberacao?> resultado = MontagemDaRegraDeDeliberacao.Montar(
            RegrasDeDeliberacaoDeTeste.AtoIndividualComVotacaoDoRecurso(), "bancas[0]");

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.QuorumExigido.Should().Be(2);
    }

    [Fact(DisplayName = "O ato individual que declara só o quórum recusa o critério e o encerramento sob o caminho da banca")]
    public void Montar_AtoIndividualSoComOQuorum_RecusaOCriterioEOEncerramento()
    {
        Result<RegraDeDeliberacao?> resultado = MontagemDaRegraDeDeliberacao.Montar(
            RegrasDeDeliberacaoDeTeste.AtoIndividualSoComOQuorum(), "fases[0].bancasRequeridas[1]");

        resultado.Errors.Select(static erro => erro.Field).Should().BeEquivalentTo(
        [
            "fases[0].bancasRequeridas[1].regraDeDeliberacao.criterio",
            "fases[0].bancasRequeridas[1].regraDeDeliberacao.encerramento",
        ]);
        resultado.Errors.Should().OnlyContain(static erro => erro.Error.Code == "Banca.VotacaoDoRecursoIncompleta");
    }
}
