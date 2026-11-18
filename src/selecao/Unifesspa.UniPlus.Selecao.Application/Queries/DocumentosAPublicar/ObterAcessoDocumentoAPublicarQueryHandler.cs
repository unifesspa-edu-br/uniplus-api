namespace Unifesspa.UniPlus.Selecao.Application.Queries.DocumentosAPublicar;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Handler convention-based do acesso de leitura ao documento a publicar.
/// Assina só depois de estabelecer que o dono existe, que o documento existe, é deste processo e
/// está confirmado — a assinatura é o último passo, nunca um que se descarta
/// depois. O dono é conferido antes do documento, e não deduzido dele: a porta lê pelo filtro global, então o
/// processo excluído logicamente não tem dono e é recusado como inexistente, mesmo com a linha e os documentos
/// preservados; o rascunho é apagado de fato com seus documentos.
/// </summary>
public static class ObterAcessoDocumentoAPublicarQueryHandler
{
    /// <summary>
    /// TTL da URL pre-assinada de leitura, em segundos. Bem menor que o do
    /// envio (<see cref="PrazosDoArquivoEnviado.EnvioSegundos"/>),
    /// e por um motivo concreto: o envio precisa cobrir a transferência de um
    /// arquivo de até 20 MB numa conexão ruim, enquanto a leitura só precisa
    /// cobrir o intervalo entre o clique e o navegador seguir o link.
    /// </summary>
    public const int TtlLeituraSegundos = PrazosDoArquivoEnviado.LeituraSegundos;

    /// <summary>TTL da URL pre-assinada de leitura como <see cref="TimeSpan"/> — ver <see cref="TtlLeituraSegundos"/>.</summary>
    public static readonly TimeSpan TtlLeitura = PrazosDoArquivoEnviado.Leitura;

    public static async Task<Result<AcessoDocumentoAPublicarDto>> Handle(
        ObterAcessoDocumentoAPublicarQuery query,
        IUnidadeDonaDoProcesso donos,
        IDocumentoAPublicarRepository documentoAPublicarRepository,
        IArquivoArmazenadoStorage storage,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(donos);
        ArgumentNullException.ThrowIfNull(documentoAPublicarRepository);
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(clock);

        if (await donos.DoProcessoAsync(query.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false) is null)
        {
            return Result<AcessoDocumentoAPublicarDto>.Failure(AcessoAoProcesso.NaoEncontrado(query.ProcessoSeletivoId));
        }
        Result<DocumentoAPublicar> confirmado = await DocumentoConfirmadoDoProcessoAsync(query, documentoAPublicarRepository, cancellationToken).ConfigureAwait(false);
        return confirmado.IsFailure
            ? Result<AcessoDocumentoAPublicarDto>.Failure(confirmado.Error!)
            : await AssinarLeituraAsync(confirmado.Value!, storage, clock, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Documento de outro processo recebe a mesma recusa de documento inexistente: separar as duas confirmaria, a
    /// quem tenta um id colhido em outro lugar, que ele existe — e a rota deste processo não é o lugar de contar isso.
    /// O pendente não passou pela validação de conteúdo: o que está na chave de upload pode não ser do formato
    /// declarado, pode exceder o limite e pode ter sido sobrescrito enquanto a URL de PUT ainda valia.
    /// </summary>
    private static async Task<Result<DocumentoAPublicar>> DocumentoConfirmadoDoProcessoAsync(
        ObterAcessoDocumentoAPublicarQuery query,
        IDocumentoAPublicarRepository documentoAPublicarRepository,
        CancellationToken cancellationToken)
    {
        DocumentoAPublicar? documento = await documentoAPublicarRepository
            .ObterPorIdAsync(query.DocumentoAPublicarId, cancellationToken)
            .ConfigureAwait(false);

        if (documento is null || documento.ProcessoSeletivoId != query.ProcessoSeletivoId)
        {
            return Result<DocumentoAPublicar>.Failure(new DomainError(
                DocumentoAPublicarErrorCodes.NaoEncontrado, "Documento não encontrado neste Processo Seletivo."));
        }

        return documento.Status != StatusArquivoEnviado.Confirmado || documento.ObjectKeyConfirmado is null
            ? Result<DocumentoAPublicar>.Failure(new DomainError(
                DocumentoAPublicarErrorCodes.NaoConfirmado, "Documento ainda não confirmado — não há conteúdo validado para conferir."))
            : Result<DocumentoAPublicar>.Success(documento);
    }

    private static async Task<Result<AcessoDocumentoAPublicarDto>> AssinarLeituraAsync(
        DocumentoAPublicar confirmado,
        IArquivoArmazenadoStorage storage,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        DateTimeOffset expiraEm = clock.GetUtcNow().Add(TtlLeitura);

        // A cópia gravada na confirmação, nunca a chave de upload: é ela que o hash do registro atesta, e a de upload segue alcançável
        // por uma URL de PUT que pode não ter expirado.
        string url = await storage
            .GerarUrlLeituraAsync(confirmado.ObjectKeyConfirmado!, TtlLeitura, cancellationToken)
            .ConfigureAwait(false);

        return Result<AcessoDocumentoAPublicarDto>.Success(
            new AcessoDocumentoAPublicarDto(new Uri(url, UriKind.Absolute), expiraEm));
    }
}
