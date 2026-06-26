namespace Unifesspa.UniPlus.Host.IntegrationTests;

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using AwesomeAssertions;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Host.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;

/// <summary>
/// Fitness test do co-hosting de idempotência: trava a regressão do P1 em que o
/// filtro era global e acumulava uma instância por módulo (o 2º filtro via a
/// reserva do 1º e respondia 409), além de resolver sempre o store do último
/// módulo registrado. O design correto aplica <see cref="IdempotencyFilter{TDbContext}"/>
/// por controller do módulo, fechado no DbContext do schema certo.
/// </summary>
[Collection(MonolitoHostCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class IdempotenciaCoHostingTests
{
    private readonly MonolitoHostFixture _fixture;

    public IdempotenciaCoHostingTests(MonolitoHostFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Nenhum filtro de idempotência é global (MvcOptions.Filters) no monólito")]
    public void Idempotencia_NaoTemFiltroGlobal()
    {
        MvcOptions mvc = _fixture.Factory.Services
            .GetRequiredService<IOptions<MvcOptions>>().Value;

        IReadOnlyList<string> globais =
        [
            .. mvc.Filters
                .Where(f => EhFiltroIdempotencia(f))
                .Select(f => f.GetType().Name),
        ];

        globais.Should().BeEmpty(
            "o filtro de idempotência deve ser aplicado por controller do módulo (convention), "
            + "nunca como filtro global — global acumula uma instância por módulo no co-hosting");
    }

    [Fact(DisplayName = "Cada endpoint [RequiresIdempotencyKey] tem exatamente 1 filtro, do DbContext do seu módulo")]
    public void EndpointIdempotente_TemUmFiltroDoModuloCorreto()
    {
        IActionDescriptorCollectionProvider provider = _fixture.Factory.Services
            .GetRequiredService<IActionDescriptorCollectionProvider>();

        List<string> violacoes = [];
        int cobertos = 0;

        foreach (ControllerActionDescriptor action in provider.ActionDescriptors.Items
            .OfType<ControllerActionDescriptor>())
        {
            if (!ExigeIdempotencia(action))
            {
                continue;
            }

            cobertos++;

            List<Type> dbContextsDosFiltros =
            [
                .. action.FilterDescriptors
                    .Select(fd => fd.Filter)
                    .Select(TipoAlvoDeFiltroDeIdempotencia)
                    .Where(t => t is not null)
                    .Select(t => t!.GetGenericArguments()[0]),
            ];

            if (dbContextsDosFiltros.Count != 1)
            {
                violacoes.Add(
                    $"{action.DisplayName}: {dbContextsDosFiltros.Count} filtros de idempotência (esperado 1)");
                continue;
            }

            string moduloFiltro = ModuloDoNamespace(dbContextsDosFiltros[0].Namespace);
            string moduloController = ModuloDoNamespace(action.ControllerTypeInfo.Namespace);

            if (!string.Equals(moduloFiltro, moduloController, StringComparison.Ordinal))
            {
                violacoes.Add(
                    $"{action.DisplayName}: filtro do módulo '{moduloFiltro}' aplicado a controller do módulo '{moduloController}'");
            }
        }

        cobertos.Should().BeGreaterThan(
            0, "o monólito deve expor endpoints [RequiresIdempotencyKey] — senão o teste é vacuamente verde");
        violacoes.Should().BeEmpty(
            "cada endpoint idempotente deve ter exatamente um filtro, fechado no DbContext do seu próprio módulo");
    }

    private static bool ExigeIdempotencia(ControllerActionDescriptor action) =>
        action.MethodInfo.GetCustomAttribute<RequiresIdempotencyKeyAttribute>(inherit: true) is not null
        || action.ControllerTypeInfo.GetCustomAttribute<RequiresIdempotencyKeyAttribute>(inherit: true) is not null;

    private static bool EhFiltroIdempotencia(IFilterMetadata filter) =>
        TipoAlvoDeFiltroDeIdempotencia(filter) is not null;

    /// <summary>
    /// Retorna o tipo fechado <c>IdempotencyFilter&lt;TDbContext&gt;</c> se o
    /// metadata for um filtro de idempotência (via <see cref="TypeFilterAttribute"/>
    /// ou <see cref="ServiceFilterAttribute"/>); senão <see langword="null"/>.
    /// </summary>
    private static Type? TipoAlvoDeFiltroDeIdempotencia(IFilterMetadata filter)
    {
        Type? alvo = filter switch
        {
            TypeFilterAttribute tf => tf.ImplementationType,
            ServiceFilterAttribute sf => sf.ServiceType,
            _ => filter.GetType(),
        };

        return alvo is { IsGenericType: true }
            && alvo.GetGenericTypeDefinition() == typeof(IdempotencyFilter<>)
            ? alvo
            : null;
    }

    private static string ModuloDoNamespace(string? ns)
    {
        // Unifesspa.UniPlus.{Modulo}.{Camada}... → {Modulo}
        if (string.IsNullOrEmpty(ns))
        {
            return string.Empty;
        }

        string[] partes = ns.Split('.');
        return partes.Length >= 3 ? partes[2] : string.Empty;
    }
}
