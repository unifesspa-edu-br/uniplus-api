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
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// Fitness test do co-hosting de idempotência: com um único contexto, o filtro do processo é aplicado
/// por controller pela convenção, exatamente uma vez. Um filtro global ou duplicado faria a segunda
/// instância ver a reserva da primeira e responder 409.
/// </summary>
[Collection(MonolitoHostCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class IdempotenciaCoHostingTests
{
    private readonly MonolitoPostgresFixture _fixture;

    public IdempotenciaCoHostingTests(MonolitoPostgresFixture fixture)
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
            "o filtro de idempotência deve ser aplicado por controller (convention), "
            + "nunca como filtro global");
    }

    [Fact(DisplayName = "Cada endpoint [RequiresIdempotencyKey] tem exatamente 1 filtro de idempotência")]
    public void EndpointIdempotente_TemUmUnicoFiltro()
    {
        IActionDescriptorCollectionProvider provider = _fixture.Factory.Services
            .GetRequiredService<IActionDescriptorCollectionProvider>();

        List<string> violacoes = [];
        int cobertos = 0;

        foreach (ControllerActionDescriptor action in provider.ActionDescriptors.Items
            .OfType<ControllerActionDescriptor>()
            .Where(ExigeIdempotencia))
        {
            cobertos++;
            int filtros = action.FilterDescriptors.Count(fd => EhFiltroIdempotencia(fd.Filter));

            if (filtros != 1)
            {
                violacoes.Add($"{action.DisplayName}: {filtros} filtros de idempotência (esperado 1)");
            }
        }

        cobertos.Should().BeGreaterThan(
            0, "o monólito deve expor endpoints [RequiresIdempotencyKey] — senão o teste é vacuamente verde");
        violacoes.Should().BeEmpty("cada endpoint idempotente deve ter exatamente um filtro");
    }

    private static bool ExigeIdempotencia(ControllerActionDescriptor action) =>
        action.MethodInfo.GetCustomAttribute<RequiresIdempotencyKeyAttribute>(inherit: true) is not null
        || action.ControllerTypeInfo.GetCustomAttribute<RequiresIdempotencyKeyAttribute>(inherit: true) is not null;

    /// <summary>
    /// Indica se o metadata é o filtro de idempotência, instalado via <see cref="TypeFilterAttribute"/>
    /// ou <see cref="ServiceFilterAttribute"/>.
    /// </summary>
    private static bool EhFiltroIdempotencia(IFilterMetadata filter)
    {
        Type alvo = filter switch
        {
            TypeFilterAttribute tf => tf.ImplementationType,
            ServiceFilterAttribute sf => sf.ServiceType,
            _ => filter.GetType(),
        };

        return alvo == typeof(IdempotencyFilter);
    }
}
