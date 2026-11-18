namespace Unifesspa.UniPlus.Autorizacao.API.Rotinas;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Unifesspa.UniPlus.Autorizacao.Application.Commands.Reclassificacoes;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.RevisoesDasConcessoes;
using Unifesspa.UniPlus.Infrastructure.Core.Hosting;

/// <summary>
/// Agenda <see cref="ProcessamentoDasRevisoesDasConcessoes"/> e os avisos da reclassificação a cada 5 minutos. Com mais de uma réplica, só a que inicia a execução
/// única processa; as outras pulam a execução.
/// </summary>
internal sealed class RevisoesDasConcessoesHostedService : PeriodicJobHostedService
{
    private static readonly TimeSpan IntervaloDaRotina = TimeSpan.FromMinutes(5);

    public RevisoesDasConcessoesHostedService(
        IServiceScopeFactory scopeFactory, TimeProvider timeProvider, ILogger<RevisoesDasConcessoesHostedService> logger)
        : base(scopeFactory, timeProvider, logger)
    {
    }

    protected override string JobName => "revisões das concessões";

    protected override TimeSpan Interval => IntervaloDaRotina;

    protected override async Task RunOnceAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        await services.GetRequiredService<ProcessamentoDasRevisoesDasConcessoes>().ExecutarAsync(cancellationToken).ConfigureAwait(false);
        await services.GetRequiredService<ProcessamentoDosAvisosDaReclassificacao>().ExecutarAsync(cancellationToken).ConfigureAwait(false);
    }
}
