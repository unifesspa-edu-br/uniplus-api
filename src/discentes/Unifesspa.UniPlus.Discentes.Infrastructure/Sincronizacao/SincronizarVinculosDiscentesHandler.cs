namespace Unifesspa.UniPlus.Discentes.Infrastructure.Sincronizacao;

using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Unifesspa.UniPlus.Discentes.Domain.Entities;
using Unifesspa.UniPlus.Discentes.Domain.Enums;

/// <summary>
/// Executa a reconciliação diária e registra o que ela alcançou.
/// </summary>
/// <remarks>
/// Não declara transação própria, e não depende de não haver uma: tanto os lotes quanto os
/// marcos da execução são gravados em escopo próprio, com confirmação independente.
/// </remarks>
public static partial class SincronizarVinculosDiscentesHandler
{
    public static async Task Handle(
        SincronizarVinculosDiscentes mensagem,
        OrquestradorDeSincronizacao orquestrador,
        IRegistroDeExecucoes execucoes,
        ILogger<SincronizarVinculosDiscentes> logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mensagem);
        ArgumentNullException.ThrowIfNull(orquestrador);
        ArgumentNullException.ThrowIfNull(execucoes);
        ArgumentNullException.ThrowIfNull(logger);

        Guid execucaoId = await execucoes
            .IniciarAsync(mensagem.DataDeReferencia, cancellationToken)
            .ConfigureAwait(false);

        ResumoDaSincronizacao resumo = await orquestrador
            .ExecutarAsync(mensagem.DataDeReferencia, cancellationToken)
            .ConfigureAwait(false);

        // O desfecho é gravado com token próprio, e não com o de quem chamou. Toda execução
        // iniciada precisa alcançar um estado terminal: se o cancelamento chegasse aqui, ele
        // abortaria justamente a gravação que tira a execução de "em andamento", e ela
        // ficaria assim para sempre — sendo que é ela que a próxima execução consulta.
        await execucoes
            .ConcluirAsync(execucaoId, resumo.EmContagens(), resumo.Situacao, CancellationToken.None)
            .ConfigureAwait(false);

        if (resumo.FalhaQueInterrompeu is { } falha)
        {
            // As contagens já refletem o que a execução alcançou antes de parar; a falha
            // sobe para que a mensageria saiba que a sincronização não completou.
            LogFalhou(logger, execucaoId, falha);
            throw falha;
        }

        LogConcluida(logger, execucaoId, resumo.Situacao, resumo.Escritos, resumo.DescartadosForaDoContrato);
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Sincronização de discentes concluída. Execucao={ExecucaoId} Situacao={Situacao} "
            + "Escritos={Escritos} ForaDoContrato={ForaDoContrato}")]
    private static partial void LogConcluida(
        ILogger logger, Guid execucaoId, SyncRunStatus situacao, int escritos, int foraDoContrato);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Sincronização de discentes falhou. Execucao={ExecucaoId}")]
    private static partial void LogFalhou(ILogger logger, Guid execucaoId, Exception excecao);

}
