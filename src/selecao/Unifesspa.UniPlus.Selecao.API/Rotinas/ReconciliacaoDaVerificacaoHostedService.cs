namespace Unifesspa.UniPlus.Selecao.API.Rotinas;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Infrastructure.Core.Hosting;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.DocumentosDaInscricao;

/// <summary>
/// Agenda a reconciliação no intervalo de <see cref="ReconciliacaoDaVerificacaoOptions"/> e repete o lote enquanto
/// ele vier cheio. Todas as réplicas agendam; a exclusividade de cada lote é do handler.
/// </summary>
internal sealed class ReconciliacaoDaVerificacaoHostedService : PeriodicJobHostedService
{
    private readonly ReconciliacaoDaVerificacaoOptions _opcoes;

    public ReconciliacaoDaVerificacaoHostedService(
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        IOptions<ReconciliacaoDaVerificacaoOptions> options,
        ILogger<ReconciliacaoDaVerificacaoHostedService> logger)
        : base(scopeFactory, timeProvider, logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _opcoes = options.Value;
    }

    protected override string JobName => "reconciliação da verificação dos arquivos do candidato";

    protected override TimeSpan Interval => _opcoes.Intervalo;

    protected override async Task RunOnceAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ICommandBus bus = services.GetRequiredService<ICommandBus>();
        int republicados;
        do
        {
            Result<int> resultado = await bus.Send(new ReconciliarVerificacaoDosArquivosCommand(), cancellationToken).ConfigureAwait(false);
            republicados = resultado.IsSuccess ? resultado.Value : 0;
        }
        while (republicados >= _opcoes.TamanhoDoLote);
    }
}
