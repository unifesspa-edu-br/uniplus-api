namespace Unifesspa.UniPlus.Regras.UnitTests.ValueObjects;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;

using Xunit;

/// <summary>O predicado depois de criado não muda: quem o recebe não pode alterar as cláusulas por trás da interface.</summary>
public sealed class PredicadoDnfImutabilidadeTests
{
    [Fact(DisplayName = "As cláusulas do predicado não podem ser alteradas por conversão para lista")]
    public void Clausulas_NaoAdmitemAlteracaoPorConversao()
    {
        PredicadoDnf predicado = PredicadoComUmaClausula();

        Action alterar = () => ((IList<ClausulaDnf>)predicado.Clausulas).Clear();

        alterar.Should().Throw<NotSupportedException>();
        predicado.Clausulas.Should().ContainSingle();
    }

    [Fact(DisplayName = "O predicado não depende da lista de linhas com que foi criado")]
    public void Criar_NaoDependeDaListaDeLinhasRecebida()
    {
        List<(int Clausula, CondicaoDnf Condicao)> linhas = [(0, Condicao("AMPLA"))];
        PredicadoDnf predicado = PredicadoDnf.CriarDeCondicoesAgrupadas(linhas).Value!;

        linhas.Add((1, Condicao("PPI")));

        predicado.Clausulas.Should().ContainSingle();
    }

    private static PredicadoDnf PredicadoComUmaClausula() =>
        PredicadoDnf.CriarDeCondicoesAgrupadas([(0, Condicao("AMPLA"))]).Value!;

    private static CondicaoDnf Condicao(string modalidade) =>
        CondicaoDnf.Criar("MODALIDADE", Operador.Igual, JsonSerializer.SerializeToElement(modalidade)).Value!;
}
