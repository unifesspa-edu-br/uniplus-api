namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using System.Text.RegularExpressions;

using ArchUnitNET.Domain;
using ArchUnitNET.Fluent.Syntax.Elements.Types;

using AwesomeAssertions;

using Unifesspa.UniPlus.ArchTests.TestSupport;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Monolito.Persistencia;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

/// <summary>
/// O contexto de persistência do monólito é detalhe da infraestrutura (ADR-0042, ADR-0170): só a <c>Infrastructure</c> de
/// um módulo, o projeto de composição da persistência e a própria base do contexto dependem dele. Domínio, aplicação e
/// camada de entrada leem e gravam por repositório e por leitor, e nunca por ele. Os tipos aninhados (máquinas de estado e
/// lambdas) contam como o tipo que os contém.
/// </summary>
public sealed class ContextoUnicoSoNaInfraestruturaTests
{
    private const string BaseDoContexto = @"Unifesspa\.UniPlus\.Infrastructure\.Core";

    private const string ComposicaoDaPersistencia = @"Unifesspa\.UniPlus\.Monolito\.Persistencia";

    [Fact(DisplayName = "Só a Infrastructure dos módulos, a composição da persistência e a base do contexto dependem do contexto único")]
    public void Produto_SoOsDonosDependemDoContexto()
    {
        DependentesDoContexto(ArquiteturaDasTravas.Produto).Select(static t => t.FullName).Should()
            .Contain(static n => n.StartsWith("Unifesspa.UniPlus.Selecao.Infrastructure.", StringComparison.Ordinal))
            .And.Contain(static n => n.StartsWith("Unifesspa.UniPlus.Monolito.Persistencia.", StringComparison.Ordinal),
                "sem dependente descoberto, a conferência passaria vazia");

        DependentesIndevidos(ArquiteturaDasTravas.Produto, ModulosDoMonolito.Raizes).Should().BeEmpty(
            "Domain, Application e API nunca dependem do contexto: a gravação passa pelo repositório e pela unidade de trabalho");
    }

    [Fact(DisplayName = "Fitness_ContextoForaDaInfraestrutura_Falha")]
    public void Fitness_ContextoForaDaInfraestrutura_Falha()
    {
        List<string> reprovados = DependentesIndevidos(ArquiteturaDasTravas.Fixtures, ModulosDoMonolito.RaizesDosFixtures);

        reprovados.Should().Contain(static v => v.Contains("LeOContextoDiretoNaAplicacao", StringComparison.Ordinal));
        reprovados.Should().NotContain(static v => v.Contains("RepositorioDeAlfa", StringComparison.Ordinal));
    }

    private static IEnumerable<IType> DependentesDoContexto(Architecture arquitetura) =>
        Types().That().DependOnAny(Contextos()).GetObjects(arquitetura);

    private static List<string> DependentesIndevidos(Architecture arquitetura, string[] raizes) =>
        ArquiteturaDasTravas.Reprovacoes(
            Types().That().ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.").And().DoNotHaveFullNameMatching(Donos(raizes))
                .Should().NotDependOnAnyTypesThat().Are(Contextos()),
            arquitetura);

    private static string Donos(string[] raizes) =>
        $@"^({string.Join('|', raizes.Select(static r => $@"{Regex.Escape(r)}\.Infrastructure"))}"
        + $"|{ComposicaoDaPersistencia}|{BaseDoContexto})(\\.|\\+|$)";

    private static GivenTypesConjunction Contextos() =>
        Types().That().HaveFullName(typeof(UniPlusDbContext).FullName!).Or().HaveFullName(typeof(MonolitoDbContext).FullName!);
}
