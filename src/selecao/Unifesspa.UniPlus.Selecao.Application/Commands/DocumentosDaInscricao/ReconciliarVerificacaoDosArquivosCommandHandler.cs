namespace Unifesspa.UniPlus.Selecao.Application.Commands.DocumentosDaInscricao;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;

using Kernel.Results;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Services;

/// <summary>
/// Só uma réplica executa cada lote (lock consultivo que dura a transação); a que chega depois, com o lock já
/// liberado, não encontra o que republicar. Os pedidos saem pelo outbox da transação que registra a publicação.
/// </summary>
public static partial class ReconciliarVerificacaoDosArquivosCommandHandler
{
    public static async Task<(Result<int> Resposta, IEnumerable<object> Mensagens)> Handle(
        ReconciliarVerificacaoDosArquivosCommand command,
        IDocumentoDaInscricaoRepository documentoRepository,
        ISelecaoUnitOfWork unitOfWork,
        IOptions<ReconciliacaoDaVerificacaoOptions> opcoes,
        TimeProvider timeProvider,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(documentoRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(opcoes);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        if (!await documentoRepository.TentarObterExclusividadeDaReconciliacaoAsync(cancellationToken).ConfigureAwait(false))
        {
            return (Result<int>.Success(0), []);
        }

        ReconciliacaoDaVerificacaoOptions configuracao = opcoes.Value;
        IReadOnlyList<ArquivoDaApresentacao> lote = await documentoRepository
            .ListarEmVerificacaoParaRepublicarAsync(timeProvider.GetUtcNow(), configuracao.TamanhoDoLote, cancellationToken)
            .ConfigureAwait(false);

        foreach (ArquivoDaApresentacao arquivo in lote)
        {
            arquivo.RegistrarPublicacao(timeProvider, configuracao.Politica);
        }

        if (lote.Count == 0)
        {
            return (Result<int>.Success(0), []);
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        LogRepublicados(logger, lote.Count);
        return (Result<int>.Success(lote.Count), [.. lote.Select(PedidoDeVerificacao.De)]);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Republicados {Quantidade} pedidos de verificação de arquivo sem resultado")]
    private static partial void LogRepublicados(ILogger logger, int quantidade);
}
