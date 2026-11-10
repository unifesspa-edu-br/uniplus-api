namespace Unifesspa.UniPlus.Autorizacao.API.Rotinas;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Unifesspa.UniPlus.Autorizacao.Application.Monitoramento;
using Unifesspa.UniPlus.Infrastructure.Core.Hosting;

/// <summary>
/// Amostra o estado da governança de perfis a cada 5 minutos e entrega o retrato às métricas. Só lê, sem trava: cada
/// réplica informa o mesmo valor. A falha só vai ao log da rotina; o gauge <c>uniplus.autorizacao.amostra.idade</c> a
/// torna visível enquanto a amostra envelhece.
/// </summary>
internal sealed class AmostragemDaGovernancaHostedService : PeriodicJobHostedService
{
    private static readonly TimeSpan IntervaloDaAmostragem = TimeSpan.FromMinutes(5);

    public AmostragemDaGovernancaHostedService(
        IServiceScopeFactory scopeFactory, TimeProvider timeProvider, ILogger<AmostragemDaGovernancaHostedService> logger)
        : base(scopeFactory, timeProvider, logger)
    {
    }

    protected override string JobName => "amostragem da governança de perfis";

    protected override TimeSpan Interval => IntervaloDaAmostragem;

    protected override async Task RunOnceAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);

        await services.GetRequiredService<AmostragemDaGovernanca>().ExecutarAsync(cancellationToken).ConfigureAwait(false);
    }
}
