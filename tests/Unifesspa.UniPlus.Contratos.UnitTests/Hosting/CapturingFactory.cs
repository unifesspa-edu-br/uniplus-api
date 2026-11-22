namespace Unifesspa.UniPlus.Contratos.UnitTests.Hosting;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

internal sealed class CapturingFactory<TEntryPoint> : WebApplicationFactory<TEntryPoint>
    where TEntryPoint : class
{
    private IReadOnlyList<ServiceDescriptor>? _capturedSnapshot;

    /// <summary>
    /// Snapshot imutável da <see cref="IServiceCollection"/> capturado em
    /// <c>ConfigureWebHost.ConfigureServices</c> (ANTES de
    /// <c>ConfigureTestServices</c> remover os hosted services problemáticos
    /// para permitir Start). Preserva a ordem de registro original do
    /// Program.cs do módulo.
    /// </summary>
    public IReadOnlyList<ServiceDescriptor> CapturedSnapshot =>
        _capturedSnapshot ?? Materialize();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            // Snapshot ANTES de ConfigureTestServices — preserva ordem original.
            _capturedSnapshot = [.. services];
        });

        builder.ConfigureTestServices(RemoverMigrationEWolverine);
    }

    // Remove MigrationHostedService e WolverineRuntime para permitir
    // Start do host sem tentar conectar contra Postgres sintético.
    // O snapshot já guardou a ordem original — esta remoção só
    // afeta o IServiceProvider materializado por get_Services.
    private static void RemoverMigrationEWolverine(IServiceCollection services)
    {
        ServiceDescriptor[] toRemove = [.. services
            .Where(d => d.ServiceType == typeof(IHostedService)
                && (MigrationOrderHeuristics.IsMigrationHostedService(d.ImplementationType)
                    || MigrationOrderHeuristics.IsWolverineRuntime(d)))];
        foreach (ServiceDescriptor descriptor in toRemove)
        {
            services.Remove(descriptor);
        }
    }

    private IReadOnlyList<ServiceDescriptor> Materialize()
    {
        // Acessar Services força WebApplicationFactory a executar ConfigureWebHost
        // e Start do host (com os hosted services problemáticos removidos).
        // Após isso _capturedSnapshot estará preenchido pelo callback acima.
        _ = Services;
        return _capturedSnapshot
            ?? throw new InvalidOperationException(
                "ConfigureWebHost executou sem preencher _capturedSnapshot — pipeline mudou.");
    }
}

internal static class MigrationOrderHeuristics
{
    private const string MigrationHostedServiceFullName =
        "Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection.MigrationHostedService`1";

    private const string WolverineAssemblyName = "Wolverine";

    public static bool IsMigrationHostedService(Type? implementationType) =>
        implementationType is { IsGenericType: true }
        && string.Equals(
            implementationType.GetGenericTypeDefinition().FullName,
            MigrationHostedServiceFullName,
            StringComparison.Ordinal);

    /// <summary>
    /// Nome do <c>TContext</c> em <c>MigrationHostedService&lt;TContext&gt;</c>
    /// (ex.: <c>UniPlusDbContext</c>), ou <c>null</c> se o descriptor não for um
    /// migration hosted service. Usado para travar a cobertura por DbContext.
    /// </summary>
    public static string? GetMigrationContextName(Type? implementationType) =>
        IsMigrationHostedService(implementationType)
            ? implementationType!.GetGenericArguments()[0].Name
            : null;

    public static bool IsWolverineRuntime(ServiceDescriptor descriptor) =>
        descriptor.ImplementationFactory is not null
        && string.Equals(
            descriptor.ImplementationFactory.Method.DeclaringType?.Assembly.GetName().Name,
            WolverineAssemblyName,
            StringComparison.Ordinal);
}
