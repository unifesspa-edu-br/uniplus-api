namespace Unifesspa.UniPlus.Autorizacao.API.Rotinas;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Unifesspa.UniPlus.Autorizacao.Application.Commands.Janelas;
using Unifesspa.UniPlus.Infrastructure.Core.Hosting;

/// <summary>
/// Agenda <see cref="ProcessamentoDasJanelas"/> a cada 5 minutos. Com mais de uma réplica, só a que obtém a trava de
/// instância única processa; as outras pulam a execução.
/// </summary>
internal sealed class JanelasDeMigracaoHostedService : PeriodicJobHostedService
{
    private static readonly TimeSpan IntervaloDaRotina = TimeSpan.FromMinutes(5);

    public JanelasDeMigracaoHostedService(
        IServiceScopeFactory scopeFactory, TimeProvider timeProvider, ILogger<JanelasDeMigracaoHostedService> logger)
        : base(scopeFactory, timeProvider, logger)
    {
    }

    protected override string JobName => "janelas de migração de concessões";

    protected override TimeSpan Interval => IntervaloDaRotina;

    protected override Task RunOnceAsync(IServiceProvider services, CancellationToken cancellationToken) =>
        services.GetRequiredService<ProcessamentoDasJanelas>().ExecutarAsync(cancellationToken);
}
