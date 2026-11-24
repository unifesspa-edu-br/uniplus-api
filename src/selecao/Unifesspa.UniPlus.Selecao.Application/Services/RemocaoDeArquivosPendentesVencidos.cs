namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;

using Microsoft.Extensions.Logging;

/// <summary>
/// Remove os registros de arquivo enviado que ficaram pendentes além do prazo — do documento do
/// Edital, do modelo de documento e do documento da inscrição — junto dos objetos que as URLs de envio
/// deles apontavam no armazenamento privado. O confirmado nunca é removido. Apaga também as chaves
/// agendadas em <c>arquivos_a_apagar</c> (envio e definitiva de arquivo do candidato removido ou descartado),
/// depois do commit de quem as agendou.
/// </summary>
/// <remarks>
/// <para>
/// O registro e os objetos saem numa transação só: o repositório apaga o registro, segurando a linha,
/// executa a remoção dos objetos e só então confirma. Se a remoção dos objetos falha, a transação é
/// desfeita, o registro fica e a próxima execução tenta de novo; a remoção do objeto já ausente é
/// concluída pelo armazenamento. A linha segurada impede a confirmação concorrente de gravar a cópia
/// de um registro que esta execução já decidiu remover, e a remoção condicionada ao pendente vencido
/// poupa a cópia do confirmado que reivindicou o registro primeiro.
/// </para>
/// <para>
/// Os objetos do pendente são os que o registro lista: o de envio e, no arquivo do administrador, a
/// cópia gravada na confirmação cujo registro foi desfeito. O arquivo do candidato não lista nenhum:
/// o bucket de envio dele expira sozinho.
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

    private readonly Fonte[] _fontes;
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
            new Fonte(documentosAPublicar, storage.RemoverAsync),
            new Fonte(modelosDeDocumento, storage.RemoverAsync),
            new Fonte(documentosDaInscricao, static (_, _) => Task.CompletedTask),
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
        int removidos = await RemoverVencidosDasFontesAsync(_clock.GetUtcNow() - Tolerancia, cancellationToken).ConfigureAwait(false);
        await LimparChavesAgendadasAsync(cancellationToken).ConfigureAwait(false);

        if (removidos > 0)
        {
            LogRemovidos(_logger, removidos);
        }

        return removidos;
    }

    private async Task<int> RemoverVencidosDasFontesAsync(DateTimeOffset corte, CancellationToken cancellationToken)
    {
        int removidos = 0;
        foreach (Fonte fonte in _fontes)
        {
            removidos += await RemoverVencidosAsync(fonte, corte, cancellationToken).ConfigureAwait(false);
        }

        return removidos;
    }

    /// <summary>
    /// Percorre os vencidos lote a lote. Para no lote incompleto — não há mais — e no lote com
    /// alguma falha: a consulta seguinte traria de volta o que acabou de falhar, e a execução
    /// insistiria contra um armazenamento indisponível em vez de esperar a próxima.
    /// </summary>
    private async Task<int> RemoverVencidosAsync(Fonte fonte, DateTimeOffset corte, CancellationToken cancellationToken)
    {
        int removidos = 0;
        while (true)
        {
            IReadOnlyList<ArquivoPendenteVencido> lote = await fonte.Repositorio
                .ListarPendentesVencidosAsync(corte, TamanhoDoLote, cancellationToken)
                .ConfigureAwait(false);
            DesfechoDoLote desfecho = await RemoverLoteAsync(fonte, lote, corte, cancellationToken).ConfigureAwait(false);
            removidos += desfecho.Removidos;

            if (desfecho.HouveFalha || lote.Count < TamanhoDoLote)
            {
                return removidos;
            }
        }
    }

    private async Task<DesfechoDoLote> RemoverLoteAsync(
        Fonte fonte, IReadOnlyList<ArquivoPendenteVencido> lote, DateTimeOffset corte, CancellationToken cancellationToken)
    {
        int removidos = 0;
        bool houveFalha = false;
        foreach (ArquivoPendenteVencido pendente in lote)
        {
            DesfechoDaRemocao desfecho = await RemoverPendenteAsync(fonte, pendente, corte, cancellationToken).ConfigureAwait(false);
            removidos += desfecho == DesfechoDaRemocao.Removido ? 1 : 0;
            houveFalha |= desfecho == DesfechoDaRemocao.Falhou;
        }

        return new DesfechoDoLote(removidos, houveFalha);
    }

    private async Task<DesfechoDaRemocao> RemoverPendenteAsync(
        Fonte fonte, ArquivoPendenteVencido pendente, DateTimeOffset corte, CancellationToken cancellationToken)
    {
        try
        {
            bool removido = await fonte.Repositorio
                .RemoverSePendenteVencidoAsync(
                    pendente.Id, corte, ct => RemoverObjetosAsync(pendente.ChavesDeObjeto, fonte.RemoverObjeto, ct), cancellationToken)
                .ConfigureAwait(false);
            return removido ? DesfechoDaRemocao.Removido : DesfechoDaRemocao.Preservado;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogFalhaAoRemoverPendente(_logger, pendente.Id, ex);
            return DesfechoDaRemocao.Falhou;
        }
    }

    private static async Task RemoverObjetosAsync(
        IReadOnlyList<string> chaves, Func<string, CancellationToken, Task> removerObjeto, CancellationToken cancellationToken)
    {
        foreach (string chave in chaves)
        {
            await removerObjeto(chave, cancellationToken).ConfigureAwait(false);
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
            IReadOnlyList<ArquivoAApagar> lote = await _documentosDaInscricao
                .ListarChavesVencidasAsync(agora, TamanhoDoLote, cancellationToken)
                .ConfigureAwait(false);
            bool houveFalha = !await LimparLoteDeChavesAsync(lote, cancellationToken).ConfigureAwait(false);

            if (houveFalha || lote.Count < TamanhoDoLote)
            {
                return;
            }
        }
    }

    private async Task<bool> LimparLoteDeChavesAsync(IReadOnlyList<ArquivoAApagar> lote, CancellationToken cancellationToken)
    {
        bool semFalha = true;
        foreach (ArquivoAApagar chave in lote)
        {
            if (await RemoverChaveAgendadaAsync(chave, cancellationToken).ConfigureAwait(false))
            {
                await _documentosDaInscricao.RemoverChaveAgendadaAsync(chave.Id, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                semFalha = false;
            }
        }

        return semFalha;
    }

    private async Task<bool> RemoverChaveAgendadaAsync(ArquivoAApagar chave, CancellationToken cancellationToken)
    {
        try
        {
            await _armazenamentoDoCandidato.RemoverDefinitivoAsync(chave.ObjectKey, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A chave é composta só por identificadores; o nome do arquivo nunca entra nela.
            LogFalhaAoRemoverChaveAgendada(_logger, chave.Id, chave.ObjectKey, ex);
            return false;
        }
    }

    private sealed record Fonte(IArquivoEnviadoRepository Repositorio, Func<string, CancellationToken, Task> RemoverObjeto);

    private sealed record DesfechoDoLote(int Removidos, bool HouveFalha);

    private enum DesfechoDaRemocao
    {
        Removido,
        Preservado,
        Falhou,
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Removidos {Quantidade} envios de arquivo pendentes vencidos")]
    private static partial void LogRemovidos(ILogger logger, int quantidade);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Falha ao remover o envio pendente vencido {RegistroId} e os objetos dele; o registro fica para a próxima execução")]
    private static partial void LogFalhaAoRemoverPendente(ILogger logger, Guid registroId, Exception exception);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Falha ao remover a chave agendada {ObjectKey} ({RegistroId}); ela fica para a próxima execução")]
    private static partial void LogFalhaAoRemoverChaveAgendada(ILogger logger, Guid registroId, string objectKey, Exception exception);
}
