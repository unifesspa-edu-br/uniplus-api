namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using System.Reflection;

using AwesomeAssertions;

using Microsoft.AspNetCore.Mvc.Filters;

using Unifesspa.UniPlus.ArchTests.TestSupport;
using Unifesspa.UniPlus.Infrastructure.Core.Authorization;

/// <summary>
/// O contrato OpenAPI só declara o <c>403</c> numa rota que exige algo além do login, e reconhece essa rota pela
/// marca <see cref="IExigeAutorizacaoAlemDoLogin"/>. Um atributo de autorização sem a marca deixaria a rota
/// responder <c>403</c> sem o contrato dizer.
/// </summary>
public sealed class FiltroDeAutorizacaoDeclaraExigenciaTests
{
    [Fact(DisplayName = "Fitness_AtributoDeFiltroDeAutorizacaoSemMarca_Falha")]
    public void Fitness_AtributoDeFiltroDeAutorizacaoSemMarca_Falha()
    {
        SemMarca([typeof(FiltroSemMarca)]).Should().ContainSingle().Which.Should().Contain(nameof(FiltroSemMarca));

        SemMarca(AtributosDeFiltroDaSolucao()).Should().BeEmpty(
            "o transformador do contrato reconhece a exigência pela marca, e não por uma lista de tipos");
    }

    private static IEnumerable<string> SemMarca(IEnumerable<Type> atributos)
        => atributos
            .Where(static t => !typeof(IExigeAutorizacaoAlemDoLogin).IsAssignableFrom(t))
            .Select(static t => t.FullName!);

    private static IEnumerable<Type> AtributosDeFiltroDaSolucao()
        => Directory
            .GetFiles(AppContext.BaseDirectory, "Unifesspa.UniPlus.*.dll")
            .Where(static caminho => !Path.GetFileName(caminho).Contains("Tests", StringComparison.Ordinal))
            .Select(static caminho => Assembly.LoadFrom(caminho))
            .SelectMany(static a => ControllersDaSolucao.TiposCarregaveis(a))
            .Where(static t => t is { IsClass: true, IsAbstract: false }
                && typeof(Attribute).IsAssignableFrom(t)
                && (typeof(IAuthorizationFilter).IsAssignableFrom(t) || typeof(IAsyncAuthorizationFilter).IsAssignableFrom(t)));

    [AttributeUsage(AttributeTargets.Method)]
    private sealed class FiltroSemMarca : Attribute, IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationFilterContext context)
        {
        }
    }
}
