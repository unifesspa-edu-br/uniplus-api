namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Reclassificacoes;

using Microsoft.Extensions.Logging;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A rotina dos avisos da reclassificação: envia, num comando por fato, o aviso das reclassificações e das divergências
/// que ainda não têm linha em <c>avisos_da_reclassificacao</c>. Com mais de uma instância, só a que inicia a execução
/// única processa. A falha de um fato não impede os demais, e o fato que falhou volta no ciclo seguinte.
/// </summary>
public sealed partial class ProcessamentoDosAvisosDaReclassificacao
{
    private const string NomeDaRotina = "autorizacao.avisos_da_reclassificacao";
    private const int LimiteDoCiclo = 200;

    private readonly IReclassificacaoRepository _reclassificacoes;
    private readonly ICommandBus _bus;
    private readonly IExecucaoUnicaDaRotina _execucaoUnica;
    private readonly ILogger<ProcessamentoDosAvisosDaReclassificacao> _logger;

    public ProcessamentoDosAvisosDaReclassificacao(
        IReclassificacaoRepository reclassificacoes, ICommandBus bus, IExecucaoUnicaDaRotina execucaoUnica, ILogger<ProcessamentoDosAvisosDaReclassificacao> logger)
    {
        ArgumentNullException.ThrowIfNull(reclassificacoes);
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(execucaoUnica);
        ArgumentNullException.ThrowIfNull(logger);

        _reclassificacoes = reclassificacoes;
        _bus = bus;
        _execucaoUnica = execucaoUnica;
        _logger = logger;
    }

    /// <summary>Envia os avisos pendentes e devolve quantos fatos foram avisados; com outra instância na rotina, pula.</summary>
    public async Task<int> ExecutarAsync(CancellationToken cancellationToken)
    {
        IAsyncDisposable? execucao = await _execucaoUnica.TentarIniciarAsync(NomeDaRotina, cancellationToken).ConfigureAwait(false);
        if (execucao is null)
        {
            return 0;
        }

        await using (execucao.ConfigureAwait(false))
        {
            return await AvisarTodosAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<int> AvisarTodosAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<FatoSemAviso> fatos = await _reclassificacoes.ListarFatosSemAvisoAsync(LimiteDoCiclo, cancellationToken).ConfigureAwait(false);
        int avisados = 0;

        foreach (FatoSemAviso fato in fatos)
        {
            avisados += await AvisarAsync(fato, cancellationToken).ConfigureAwait(false);
        }

        return avisados;
    }

    private async Task<int> AvisarAsync(FatoSemAviso fato, CancellationToken cancellationToken)
    {
        try
        {
            Result<bool> resultado = await _bus
                .Send(new EnviarAvisoDaReclassificacaoCommand(fato.Origem, fato.Id), cancellationToken)
                .ConfigureAwait(false);

            return resultado.IsSuccess && resultado.Value ? 1 : 0;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogFalhaAoAvisar(_logger, fato.Origem.ToString(), fato.Id, ex);

            return 0;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Não foi possível avisar o fato {Origem} {FatoId} da reclassificação")]
    private static partial void LogFalhaAoAvisar(ILogger logger, string origem, Guid fatoId, Exception exception);
}
