namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using System.Reflection;

using AwesomeAssertions;

using Microsoft.AspNetCore.Authorization;

using Unifesspa.UniPlus.ArchTests.Fixtures;
using Unifesspa.UniPlus.ArchTests.TestSupport;

/// <summary>
/// A decisão de negócio é do ponto único de decisão (ADR-0078), e não de um
/// <c>[Authorize(Policy = …)]</c>. A política de controller só serve para dizer quem é a
/// pessoa que chama, e a lista dessas políticas é fechada: política nova quebra o build.
/// </summary>
public sealed class PoliticaDeControllerTests
{
    /// <summary>
    /// Políticas de identidade, registradas em <c>OidcAuthenticationConfiguration</c>:
    /// <c>Candidato</c> separa quem se inscreve (titular identificado pelo CPF) de quem opera o
    /// sistema, e nenhuma concessão do catálogo descreve essa pessoa; <c>Instalacao</c> protege a
    /// instalação inicial, que acontece antes de existir qualquer concessão para decidir.
    /// </summary>
    private static readonly string[] PoliticasDeIdentidade = ["Candidato", "Instalacao"];

    [Fact(DisplayName = "Fitness_PolicyDeControllerParaRegraDeNegocio_Falha")]
    public void Fitness_PolicyDeControllerParaRegraDeNegocio_Falha()
    {
        Violacoes([typeof(FixturesDeAutorizacao.PoliticaDeNegocioNaClasse)])
            .Should().ContainSingle().Which.Should().Contain("PodeAprovarEdital");
        Violacoes([typeof(FixturesDeAutorizacao.PoliticaDeNegocioNaAcao)])
            .Should().ContainSingle().Which.Should().Contain("CoordenadorDaUnidade");

        Violacoes(ControllersDaSolucao.Todos()).Should().BeEmpty(
            "regra de negócio se decide no ponto único, com [ExigePermissao<…>]; política de controller só nomeia a identidade de quem chama");
    }

    [Fact(DisplayName = "Fitness_PolicyDeControllerSoAutenticacao_Passa")]
    public void Fitness_PolicyDeControllerSoAutenticacao_Passa()
    {
        Violacoes([typeof(FixturesDeAutorizacao.SoAutenticacao), typeof(FixturesDeAutorizacao.PoliticaDeIdentidade)])
            .Should().BeEmpty();

        IReadOnlyList<Type> controllers = ControllersDaSolucao.Todos();
        controllers.SelectMany(static t => ControllersDaSolucao.AcoesDe(t)
                .SelectMany(a => ControllersDaSolucao.AtributosDe<AuthorizeAttribute>(t, a)))
            .Should().Contain(static a => a.Policy == null, "há controllers reais só com [Authorize]")
            .And.Contain(static a => a.Policy == "Candidato", "há controllers reais com a política de identidade");
    }

    private static List<string> Violacoes(IEnumerable<Type> controllers)
        => [.. controllers.SelectMany(static t => PoliticasDe(t)
            .Where(static p => !PoliticasDeIdentidade.Contains(p.Politica, StringComparer.Ordinal))
            .Select(p => $"{t.FullName}.{p.Local}: política \"{p.Politica}\""))];

    private static IEnumerable<(string Local, string Politica)> PoliticasDe(Type controller)
    {
        foreach (AuthorizeAttribute a in controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Where(static a => !string.IsNullOrEmpty(a.Policy)))
        {
            yield return ("(classe)", a.Policy!);
        }

        foreach (MethodInfo acao in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            foreach (AuthorizeAttribute a in acao.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
                .Where(static a => !string.IsNullOrEmpty(a.Policy)))
            {
                yield return (acao.Name, a.Policy!);
            }
        }
    }
}
