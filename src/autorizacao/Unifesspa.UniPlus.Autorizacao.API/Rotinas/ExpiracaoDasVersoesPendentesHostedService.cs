namespace Unifesspa.UniPlus.Autorizacao.API.Rotinas;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Infrastructure.Core.Hosting;

/// <summary>
/// Agenda <see cref="ExpiracaoDasVersoesPendentes"/> a cada 15 minutos. Com mais de uma réplica, cada uma executa a
/// sua: a expiração trava o perfil e confere de novo, e a réplica que chega depois não expira nada.
/// </summary>
internal sealed class ExpiracaoDasVersoesPendentesHostedService : PeriodicJobHostedService
{
    private static readonly TimeSpan IntervaloDaRotina = TimeSpan.FromMinutes(15);

    public ExpiracaoDasVersoesPendentesHostedService(
        IServiceScopeFactory scopeFactory, TimeProvider timeProvider, ILogger<ExpiracaoDasVersoesPendentesHostedService> logger)
        : base(scopeFactory, timeProvider, logger)
    {
    }

    protected override string JobName => "expiração das versões pendentes de perfil";

    protected override TimeSpan Interval => IntervaloDaRotina;

    protected override Task RunOnceAsync(IServiceProvider services, CancellationToken cancellationToken) =>
        services.GetRequiredService<ExpiracaoDasVersoesPendentes>().ExecutarAsync(cancellationToken);
}
