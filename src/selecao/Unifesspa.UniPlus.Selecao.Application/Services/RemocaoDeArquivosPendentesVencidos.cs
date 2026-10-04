namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Abstractions;

using Domain.Interfaces;

using Microsoft.Extensions.Logging;

/// <summary>
/// Remove os registros de arquivo enviado que ficaram pendentes além do prazo — do documento do
/// Edital e do modelo de documento — junto dos objetos que as URLs de envio deles apontavam no
/// armazenamento privado. O confirmado nunca é removido.
/// </summary>
/// <remarks>
/// <para>
/// O objeto sai antes do registro. Se a remoção do objeto falha, o registro fica e a próxima
/// execução tenta de novo; se o registro não chega a sair depois do objeto, a próxima execução
/// repete a remoção do objeto já ausente, que o armazenamento trata como concluída. Na ordem
/// inversa, uma falha entre os dois passos deixaria um objeto sem registro, que nenhuma execução
/// voltaria a encontrar.
/// </para>
/// <para>
/// Apagar o objeto de um pendente que é confirmado ao mesmo tempo não perde nada: a confirmação
/// lê o conteúdo antes de reivindicar o registro e grava a cópia selada numa chave própria, que é
/// a única que o confirmado usa dali em diante. A remoção do registro é condicionada no banco ao
/// pendente vencido e não alcança o que a confirmação já reivindicou.
/// </para>
/// </remarks>
public sealed partial class RemocaoDeArquivosPendentesVencidos
{
    /// <summary>Quantos registros cada consulta traz, para a execução não carregar a tabela inteira.</summary>
    public const int TamanhoDoLote = 100;

    private readonly IArquivoEnviadoRepository[] _repositorios;
    private readonly IArquivoArmazenadoStorage _storage;
    private readonly TimeProvider _clock;
    private readonly ILogger<RemocaoDeArquivosPendentesVencidos> _logger;

    public RemocaoDeArquivosPendentesVencidos(
        IDocumentoEditalRepository documentosEdital,
        IModeloDeDocumentoRepository modelosDeDocumento,
        IArquivoArmazenadoStorage storage,
        TimeProvider clock,
        ILogger<RemocaoDeArquivosPendentesVencidos> logger)
    {
        ArgumentNullException.ThrowIfNull(documentosEdital);
        ArgumentNullException.ThrowIfNull(modelosDeDocumento);
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        _repositorios = [documentosEdital, modelosDeDocumento];
        _storage = storage;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>Remove os pendentes vencidos no instante da chamada; devolve quantos registros saíram.</summary>
    public async Task<int> ExecutarAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset agora = _clock.GetUtcNow();
        int removidos = 0;
        foreach (IArquivoEnviadoRepository repositorio in _repositorios)
        {
            removidos += await RemoverVencidosAsync(repositorio, agora, cancellationToken).ConfigureAwait(false);
        }

        if (removidos > 0)
        {
            LogRemovidos(_logger, removidos);
        }

        return removidos;
    }

    /// <summary>
    /// Percorre os vencidos lote a lote. Para no lote incompleto — não há mais — e no lote com
    /// alguma falha: a consulta seguinte traria de volta o que acabou de falhar, e a execução
    /// insistiria contra um armazenamento indisponível em vez de esperar a próxima.
    /// </summary>
    private async Task<int> RemoverVencidosAsync(
        IArquivoEnviadoRepository repositorio, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        int removidos = 0;
        while (true)
        {
            IReadOnlyList<ArquivoPendenteVencido> lote = await repositorio
                .ListarPendentesVencidosAsync(agora, TamanhoDoLote, cancellationToken)
                .ConfigureAwait(false);

            bool houveFalha = false;
            foreach (ArquivoPendenteVencido pendente in lote)
            {
                if (!await RemoverObjetoAsync(pendente, cancellationToken).ConfigureAwait(false))
                {
                    houveFalha = true;
                    continue;
                }

                if (await repositorio.RemoverSePendenteVencidoAsync(pendente.Id, agora, cancellationToken).ConfigureAwait(false))
                {
                    removidos++;
                }
            }

            if (houveFalha || lote.Count < TamanhoDoLote)
            {
                return removidos;
            }
        }
    }

    private async Task<bool> RemoverObjetoAsync(ArquivoPendenteVencido pendente, CancellationToken cancellationToken)
    {
        try
        {
            await _storage.RemoverAsync(pendente.ObjectKey, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A chave é composta só por identificadores; o nome do arquivo nunca entra nela.
            LogFalhaAoRemoverObjeto(_logger, pendente.Id, pendente.ObjectKey, ex);
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Removidos {Quantidade} envios de arquivo pendentes vencidos")]
    private static partial void LogRemovidos(ILogger logger, int quantidade);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Falha ao remover o objeto {ObjectKey} do envio pendente vencido {RegistroId}; o registro fica para a próxima execução")]
    private static partial void LogFalhaAoRemoverObjeto(ILogger logger, Guid registroId, string objectKey, Exception exception);
}
