namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;

using Microsoft.Extensions.Logging;

/// <summary>
/// Remove os registros de arquivo enviado que ficaram pendentes além do prazo — do documento do
/// Edital, do modelo de documento e do documento da inscrição — junto dos objetos que as URLs de envio
/// deles apontavam no armazenamento privado. O confirmado nunca é removido. Apaga também as chaves
/// agendadas em <c>chaves_a_limpar</c> (envio e definitiva de arquivo do candidato removido ou descartado),
/// depois do commit de quem as agendou.
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
/// A remoção do registro é condicionada no banco ao pendente vencido. O objeto pendente do candidato não é
/// removido: o bucket de envio expira sozinho.
/// </para>
/// </remarks>
public sealed partial class RemocaoDeArquivosPendentesVencidos
{
    /// <summary>Quantos registros cada consulta traz, para a execução não carregar a tabela inteira.</summary>
    public const int TamanhoDoLote = 100;

    /// <summary>
    /// Quanto o registro espera depois do <c>ExpiraEm</c> antes de sair. O prazo é gravado antes de
    /// a URL de envio ser assinada, e a URL vale o prazo inteiro a partir da assinatura; um envio
    /// aceito no fim da validade também termina depois dela. Sem a folga, o objeto enviado nessa
    /// janela chegaria depois da remoção e ficaria sem registro.
    /// </summary>
    public static readonly TimeSpan Tolerancia = TimeSpan.FromHours(1);

    private readonly (IArquivoEnviadoRepository Repositorio, Func<string, CancellationToken, Task>? RemoverObjeto)[] _fontes;
    private readonly IDocumentoDaInscricaoRepository _documentosDaInscricao;
    private readonly IArquivoDoCandidatoStorage _armazenamentoDoCandidato;
    private readonly TimeProvider _clock;
    private readonly ILogger<RemocaoDeArquivosPendentesVencidos> _logger;

    public RemocaoDeArquivosPendentesVencidos(
        IDocumentoAPublicarRepository documentosAPublicar,
        IModeloDeDocumentoRepository modelosDeDocumento,
        IDocumentoDaInscricaoRepository documentosDaInscricao,
        IArquivoArmazenadoStorage storage,
        IArquivoDoCandidatoStorage armazenamentoDoCandidato,
        TimeProvider clock,
        ILogger<RemocaoDeArquivosPendentesVencidos> logger)
    {
        ArgumentNullException.ThrowIfNull(documentosAPublicar);
        ArgumentNullException.ThrowIfNull(modelosDeDocumento);
        ArgumentNullException.ThrowIfNull(documentosDaInscricao);
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(armazenamentoDoCandidato);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        _fontes =
        [
            (documentosAPublicar, storage.RemoverAsync),
            (modelosDeDocumento, storage.RemoverAsync),
            (documentosDaInscricao, null),
        ];
        _documentosDaInscricao = documentosDaInscricao;
        _armazenamentoDoCandidato = armazenamentoDoCandidato;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Remove os pendentes vencidos há mais que a <see cref="Tolerancia"/> no instante da chamada;
    /// devolve quantos registros saíram.
    /// </summary>
    public async Task<int> ExecutarAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset agora = _clock.GetUtcNow() - Tolerancia;
        int removidos = 0;
        foreach ((IArquivoEnviadoRepository repositorio, Func<string, CancellationToken, Task>? removerObjeto) in _fontes)
        {
            removidos += await RemoverVencidosAsync(repositorio, removerObjeto, agora, cancellationToken).ConfigureAwait(false);
        }

        await LimparChavesAgendadasAsync(cancellationToken).ConfigureAwait(false);

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
        IArquivoEnviadoRepository repositorio,
        Func<string, CancellationToken, Task>? removerObjeto,
        DateTimeOffset agora,
        CancellationToken cancellationToken)
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
                if (removerObjeto is not null && !await RemoverObjetoAsync(pendente, removerObjeto, cancellationToken).ConfigureAwait(false))
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

    /// <summary>
    /// Apaga as chaves agendadas cuja hora passou. A chave agendada é sempre a definitiva do arquivo do candidato, no
    /// bucket definitivo. Ela sai da agenda depois de apagada; a falha fica para a próxima execução.
    /// </summary>
    private async Task LimparChavesAgendadasAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset agora = _clock.GetUtcNow();
        while (true)
        {
            IReadOnlyList<ChaveALimpar> lote = await _documentosDaInscricao
                .ListarChavesVencidasAsync(agora, TamanhoDoLote, cancellationToken)
                .ConfigureAwait(false);

            bool houveFalha = false;
            foreach (ChaveALimpar chave in lote)
            {
                if (!await RemoverObjetoAsync(new ArquivoPendenteVencido(chave.Id, chave.ObjectKey), _armazenamentoDoCandidato.RemoverDefinitivoAsync, cancellationToken).ConfigureAwait(false))
                {
                    houveFalha = true;
                    continue;
                }

                await _documentosDaInscricao.RemoverChaveAgendadaAsync(chave.Id, cancellationToken).ConfigureAwait(false);
            }

            if (houveFalha || lote.Count < TamanhoDoLote)
            {
                return;
            }
        }
    }

    private async Task<bool> RemoverObjetoAsync(
        ArquivoPendenteVencido pendente, Func<string, CancellationToken, Task> removerObjeto, CancellationToken cancellationToken)
    {
        try
        {
            await removerObjeto(pendente.ObjectKey, cancellationToken).ConfigureAwait(false);
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
