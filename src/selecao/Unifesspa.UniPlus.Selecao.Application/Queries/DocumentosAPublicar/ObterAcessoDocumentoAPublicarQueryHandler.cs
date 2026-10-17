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
/// Assina só depois de estabelecer que o documento existe, é deste processo e
/// está confirmado — a assinatura é o último passo, nunca um que se descarta
/// depois.
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
        IProcessoSeletivoRepository processoSeletivoRepository,
        IDocumentoAPublicarRepository documentoAPublicarRepository,
        IArquivoArmazenadoStorage storage,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(documentoAPublicarRepository);
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(clock);

        // A visibilidade do processo é decidida antes do documento, e não
        // deduzida dele: o processo é excluído logicamente e a exclusão
        // preserva a linha, então um processo fora de alcance continua tendo
        // documentos que apontam para ele.
        bool processoExiste = await processoSeletivoRepository
            .ExisteAsync(query.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);

        if (!processoExiste)
        {
            return Result<AcessoDocumentoAPublicarDto>.Failure(new DomainError(
                "ProcessoSeletivo.NaoEncontrado",
                $"Processo Seletivo {query.ProcessoSeletivoId} não encontrado."));
        }

        DocumentoAPublicar? documento = await documentoAPublicarRepository
            .ObterPorIdAsync(query.DocumentoAPublicarId, cancellationToken)
            .ConfigureAwait(false);

        // Documento de outro processo recebe a mesma recusa de documento
        // inexistente. Separar as duas confirmaria, a quem tenta um id colhido
        // em outro lugar, que ele existe — e a rota deste processo não é o
        // lugar de contar isso.
        if (documento is null || documento.ProcessoSeletivoId != query.ProcessoSeletivoId)
        {
            return Result<AcessoDocumentoAPublicarDto>.Failure(new DomainError(
                DocumentoAPublicarErrorCodes.NaoEncontrado,
                "Documento não encontrado neste Processo Seletivo."));
        }

        // O pendente não passou pela validação de conteúdo: o que está na
        // chave de upload pode não ser do formato declarado, pode exceder o limite, e pode ter
        // sido sobrescrito enquanto a URL de PUT ainda valia.
        if (documento.Status != StatusArquivoEnviado.Confirmado || documento.ObjectKeyConfirmado is null)
        {
            return Result<AcessoDocumentoAPublicarDto>.Failure(new DomainError(
                DocumentoAPublicarErrorCodes.NaoConfirmado,
                "Documento ainda não confirmado — não há conteúdo validado para conferir."));
        }

        DateTimeOffset expiraEm = clock.GetUtcNow().Add(TtlLeitura);

        // A cópia selada, nunca a chave de upload: é ela que o hash do
        // registro atesta, e a de upload segue alcançável por uma URL de PUT
        // que pode não ter expirado.
        string url = await storage
            .GerarUrlLeituraAsync(documento.ObjectKeyConfirmado, TtlLeitura, cancellationToken)
            .ConfigureAwait(false);

        return Result<AcessoDocumentoAPublicarDto>.Success(
            new AcessoDocumentoAPublicarDto(new Uri(url, UriKind.Absolute), expiraEm));
    }
}
