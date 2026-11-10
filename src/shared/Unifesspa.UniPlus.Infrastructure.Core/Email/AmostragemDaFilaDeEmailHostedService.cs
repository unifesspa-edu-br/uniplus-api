namespace Unifesspa.UniPlus.Infrastructure.Core.Email;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Unifesspa.UniPlus.Infrastructure.Core.Hosting;

/// <summary>
/// Amostra a fila durável de e-mail a cada minuto e entrega o retrato às métricas. A falha só vai ao log da rotina; o
/// gauge <c>uniplus.email.amostra.idade</c> a torna visível enquanto a amostra envelhece.
/// </summary>
public sealed class AmostragemDaFilaDeEmailHostedService : PeriodicJobHostedService
{
    private static readonly TimeSpan IntervaloDaAmostragem = TimeSpan.FromMinutes(1);

    public AmostragemDaFilaDeEmailHostedService(
        IServiceScopeFactory scopeFactory, TimeProvider timeProvider, ILogger<AmostragemDaFilaDeEmailHostedService> logger)
        : base(scopeFactory, timeProvider, logger)
    {
    }

    protected override string JobName => "amostragem da fila de e-mail";

    protected override TimeSpan Interval => IntervaloDaAmostragem;

    protected override async Task RunOnceAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);

        await services.GetRequiredService<AmostragemDaFilaDeEmail>().ExecutarAsync(cancellationToken).ConfigureAwait(false);
    }
}
