namespace Unifesspa.UniPlus.Selecao.ArchTests;

using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.xUnit;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

/// <summary>
/// Fase e etapa se conhecem só por valores comuns (janela, código de fase, enums): as regras de cada uma são avaliadas sem o
/// tipo da outra, e a relação entre as duas vive em <c>FasesEEtapas</c>. A fronteira vale para os tipos nomeados da fase e da
/// etapa, porque o namespace ainda abriga as regras de gravação e publicação que leem as duas.
/// </summary>
public sealed class FronteiraEntreFaseEEtapaTests
{
    private const string Raiz = "Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.";

    private static readonly string[] TiposDaFase =
    [
        "Cronograma.FaseDeclarada",
        "Cronograma.AtosDaFase",
        "Cronograma.BancasDaFase",
        "Cronograma.CronogramaDoProcesso",
        "Cronograma.EstruturaDoCronograma",
        "Cronograma.CompletudeDoCronograma",
        "Cronograma.JanelaDeIsencao",
    ];

    private static readonly string[] TiposDaEtapa =
    [
        "Etapas.EtapaDeclarada",
        "Etapas.AvaliacaoDeclaradaDaEtapa",
        "Etapas.AtosDaEtapa",
        "Etapas.BancasDaEtapa",
        "Etapas.EtapasDoProcesso",
        "Etapas.ConjuntoDasEtapas",
        "Etapas.CitacoesDeNota",
        "Etapas.CitacaoDaEtapa",
        "Etapas.ConformidadeDaAvaliacaoDasEtapas",
    ];

    private static readonly Architecture Arquitetura = ArquiteturaDaSelecao.Produto;

    [Fact(DisplayName = "Os tipos nomeados da fase e da etapa existem, para a fronteira não ficar vazia por erro de nome")]
    public void OsTiposNomeadosExistem()
    {
        string[] existentes = [.. Arquitetura.Types.Select(static tipo => tipo.FullName)];

        string[] ausentes = [.. TiposDaFase.Concat(TiposDaEtapa).Select(static nome => Raiz + nome).Where(nome => !existentes.Contains(nome))];

        ausentes.Should().BeEmpty();
    }

    [Fact(DisplayName = "Os tipos da fase não dependem de tipo da etapa")]
    public void TiposDaFase_NaoDependemDosTiposDaEtapa()
    {
        IArchRule regra = Types()
            .That().HaveFullNameMatching(Expressao(TiposDaFase))
            .Should().NotDependOnAnyTypesThat().HaveFullNameMatching(Expressao(TiposDaEtapa))
            .Because("a regra da fase recebe a fase por valores; quem relaciona fase e etapa é FasesEEtapas");

        regra.Check(Arquitetura);
    }

    [Fact(DisplayName = "Os tipos da etapa não dependem de tipo da fase")]
    public void TiposDaEtapa_NaoDependemDosTiposDaFase()
    {
        IArchRule regra = Types()
            .That().HaveFullNameMatching(Expressao(TiposDaEtapa))
            .Should().NotDependOnAnyTypesThat().HaveFullNameMatching(Expressao(TiposDaFase))
            .Because("a regra da etapa recebe a etapa por valores; quem relaciona fase e etapa é FasesEEtapas");

        regra.Check(Arquitetura);
    }

    private static string Expressao(IEnumerable<string> nomes) =>
        $@"^{System.Text.RegularExpressions.Regex.Escape(Raiz)}({string.Join("|", nomes.Select(static nome => System.Text.RegularExpressions.Regex.Escape(nome)))})(\+|$)";
}
