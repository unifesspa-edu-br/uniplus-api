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

    [Fact(DisplayName = "O ato individual que declara quórum é recusado no campo do quórum")]
    public void Montar_AtoIndividualComQuorum_RecusaOQuorum()
    {
        RegraDeDeliberacaoInput declarada = new(ModoDeDeliberacao.AtoIndividual, Integrantes: 1, Suplentes: 0, Quorum: 1);

        Result<RegraDeDeliberacao?> resultado = MontagemDaRegraDeDeliberacao.Montar(declarada, "bancas[0]");

        FieldError recusa = resultado.Errors.Should().ContainSingle().Subject;
        recusa.Field.Should().Be("bancas[0].regraDeDeliberacao.quorum");
        recusa.Error.Code.Should().Be("Banca.AtoIndividualSemVotacao");
    }
}
