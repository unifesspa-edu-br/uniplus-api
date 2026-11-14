namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using System.Text.RegularExpressions;

using ArchUnitNET.Domain;
using ArchUnitNET.Domain.Dependencies;

using AwesomeAssertions;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.ArchTests.TestSupport;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// Quem implementa uma interface do <c>Contracts</c> de um módulo lê, ou entrega a gravação a quem chama (ADR-0170): a
/// porta de escrita não confirma, e o leitor roda na transação do chamador, sem rastrear e sem gravar. Por isso a
/// implementação não chama o <c>SaveChanges</c> do contexto, a unidade de trabalho, o rastreador de alterações nem a
/// escrita em massa ou em SQL. Os tipos aninhados (máquinas de estado e lambdas) contam como o tipo que os contém.
/// </summary>
public sealed class LeitorDoContratoNaoGravaTests
{
    private static readonly Regex Gravacao = new(
        @"^((SaveChanges|SalvarAlteracoes|ExecuteUpdate|ExecuteDelete|ExecuteSql)[\w<>,]*|get_ChangeTracker)\(",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    private static readonly Regex DonoDaGravacao = new(
        $@"^(Microsoft\.EntityFrameworkCore(\.|$)|{Regex.Escape(typeof(IUnitOfWork).FullName!)}$|{Regex.Escape(typeof(UniPlusDbContext).FullName!)}$)",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    private const string GovernancaCompartilhada = "Unifesspa.UniPlus.Governance";

    [Fact(DisplayName = "Quem implementa interface do Contracts não grava nem rastreia")]
    public void Produto_ImplementacaoDoContratoNaoGrava()
    {
        ImplementadoresDoContrato(ArquiteturaDasTravas.Produto, ModulosDoMonolito.Raizes).Should()
            .NotBeEmpty("sem implementação descoberta, a conferência passaria vazia");

        ImplementacoesQueGravam(ArquiteturaDasTravas.Produto, ModulosDoMonolito.Raizes).Should().BeEmpty(
            "o leitor não grava: a gravação é de quem o chama");
    }

    [Fact(DisplayName = "Fitness_LeitorQueGrava_Falha")]
    public void Fitness_LeitorQueGrava_Falha()
    {
        List<string> reprovados = ImplementacoesQueGravam(ArquiteturaDasTravas.Fixtures, ModulosDoMonolito.RaizesDosFixtures);

        foreach (string leitor in new[]
        {
            "LeitorQueSalvaPeloContexto", "LeitorQueSalvaPelaUnidadeDeTrabalho", "LeitorQueUsaORastreador",
            "LeitorQueApagaEmMassa", "LeitorQueAtualizaEmMassa", "LeitorQueExecutaSql",
        })
        {
            reprovados.Should().Contain(v => v.Contains(leitor, StringComparison.Ordinal), $"{leitor} grava");
        }

        reprovados.Should().NotContain(static v => v.Contains("LeitorQueSoLe", StringComparison.Ordinal));
    }

    private static List<Class> ImplementadoresDoContrato(Architecture arquitetura, string[] raizes) =>
        ImplementadoresDeInterface.Em(arquitetura, i => EhDoContrato(i, raizes));

    private static bool EhDoContrato(Interface candidata, string[] raizes) =>
        new[] { GovernancaCompartilhada }.Concat(raizes)
            .Any(r => Regex.IsMatch(candidata.Namespace.FullName, $@"^{Regex.Escape(r)}\.Contracts(\.|$)"));

    // O ArchUnitNET não casa a chamada de um método genérico (ExecuteDeleteAsync<T>) com o membro declarado, e a regra fluente
    // deixaria de ver a escrita em massa: por isso as chamadas de cada implementação são lidas do modelo da arquitetura.
    private static List<string> ImplementacoesQueGravam(Architecture arquitetura, string[] raizes)
    {
        string alvo = ImplementadoresDeInterface.PadraoComAninhados(ImplementadoresDoContrato(arquitetura, raizes));

        return
        [
            .. arquitetura.Types
                .Where(tipo => Regex.IsMatch(tipo.FullName, alvo, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5)))
                .SelectMany(static tipo => tipo.Members.OfType<MethodMember>().SelectMany(static metodo => metodo.MemberDependencies))
                .OfType<MethodCallDependency>()
                .Where(static chamada => EhGravacao(chamada.TargetMember))
                .Select(static chamada => $"{chamada.Origin.FullName} does call {chamada.TargetMember.FullName}")
                .Distinct(StringComparer.Ordinal),
        ];
    }

    private static bool EhGravacao(IMember chamado) =>
        Gravacao.IsMatch(chamado.Name) && DonoDaGravacao.IsMatch(chamado.DeclaringType.FullName);
}
