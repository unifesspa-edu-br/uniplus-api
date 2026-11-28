namespace Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;

using Domain.Entities;
using Domain.Enums;
using Domain.Errors;
using Domain.Interfaces;
using Domain.Services;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Confere os documentos que o ato referencia: cada um é do processo, está confirmado, foi
/// enviado para um ato e não foi usado em outro; o principal é PDF e os anexos têm nomes distintos.
/// </summary>
/// <remarks>
/// Antes de qualquer escrita, porque o ato é gravado e registrado numa transação só e a recusa tardia
/// desfaria o que já foi rastreado. O documento usado em outro ato é recusado para que o
/// arquivo publicado tenha um único ato por trás; a finalidade já separa o documento do ato do
/// documento e dos anexos do edital.
/// </remarks>
public sealed class ConferenciaDosDocumentosDoAto(
    IDocumentoAPublicarRepository documentoRepository,
    IAtoDoProcessoRepository atoDoProcessoRepository)
{
    public const int QuantidadeMaximaDeAnexos = 20;

    internal async Task<Result<DocumentosDoAtoConferidos>> ConferirAsync(
        Guid processoSeletivoId, Guid documentoPrincipalId,
        IReadOnlyList<AnexoSolicitadoDoAto> anexosSolicitados, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(anexosSolicitados);
        if (anexosSolicitados.Count > QuantidadeMaximaDeAnexos)
        {
            return Falha(AtoDoProcessoErrorCodes.QuantidadeDeAnexosExcedida,
                $"O ato aceita no máximo {QuantidadeMaximaDeAnexos} anexos.");
        }
        Guid[] ids = [documentoPrincipalId, .. anexosSolicitados.Select(static a => a.DocumentoId)];
        DomainError? recusa = ConferirRepeticao(ids);
        return recusa is not null
            ? Result<DocumentosDoAtoConferidos>.Failure(recusa)
            : await ConferirDocumentosEnviadosAsync(processoSeletivoId, ids, anexosSolicitados, cancellationToken).ConfigureAwait(false);
    }

    private static DomainError? ConferirRepeticao(Guid[] ids) =>
        ids.Distinct().Count() != ids.Length
            ? new(AtoDoProcessoErrorCodes.DocumentoRepetido,
                "O mesmo documento não pode constar duas vezes no ato, nem como principal e anexo.")
            : null;

    private async Task<Result<DocumentosDoAtoConferidos>> ConferirDocumentosEnviadosAsync(
        Guid processoId, Guid[] ids, IReadOnlyList<AnexoSolicitadoDoAto> anexos, CancellationToken cancellationToken)
    {
        IReadOnlyList<DocumentoAPublicar> encontrados = await documentoRepository
            .ListarDoProcessoAsync(processoId, ids, cancellationToken).ConfigureAwait(false);
        Dictionary<Guid, DocumentoAPublicar> porId = encontrados.ToDictionary(static d => d.Id);
        FieldError? recusa = ConferirEnvios(ids, porId);
        return recusa is not null
            ? Result<DocumentosDoAtoConferidos>.ValidationFailure([recusa])
            : await ConferirPrincipalEAnexosAsync(processoId, ids, porId, anexos, cancellationToken).ConfigureAwait(false);
    }

    private static FieldError? ConferirEnvios(Guid[] ids, Dictionary<Guid, DocumentoAPublicar> documentos)
    {
        for (int posicao = 0; posicao < ids.Length; posicao++)
        {
            FieldError? recusa = ConferirEnvio(documentos.GetValueOrDefault(ids[posicao]), posicao);
            if (recusa is not null)
            {
                return recusa;
            }
        }
        return null;
    }

    // A posição 0 é o documento principal; as seguintes são os anexos, na ordem do pedido.
    private static FieldError? ConferirEnvio(DocumentoAPublicar? documento, int posicao) => documento switch
    {
        null => DocumentoNaoEncontrado(posicao),
        { Situacao: not StatusArquivoEnviado.Confirmado } => new(null, new(AtoDoProcessoErrorCodes.DocumentoNaoConfirmado,
            "Somente um documento confirmado pode ser referenciado no ato.")),
        { Finalidade: not FinalidadeDoDocumento.DocumentoDeAto } => new(null, new(AtoDoProcessoErrorCodes.DocumentoNaoEnviadoComoDocumentoDeAto,
            "Somente um documento enviado para um ato pode ser o principal ou o anexo de um ato.")),
        _ => null,
    };

    private static FieldError DocumentoNaoEncontrado(int posicao) => posicao == 0
        ? new("documentoPrincipalId", new(AtoDoProcessoErrorCodes.DocumentoNaoEncontrado,
            "O documento principal do ato deve ser um dos documentos deste processo."))
        : new($"anexos[{posicao - 1}].documentoId", new(AtoDoProcessoErrorCodes.DocumentoNaoEncontrado,
            "O documento do anexo deve ser um dos documentos deste processo."));

    private async Task<Result<DocumentosDoAtoConferidos>> ConferirPrincipalEAnexosAsync(
        Guid processoId, Guid[] ids, Dictionary<Guid, DocumentoAPublicar> documentos,
        IReadOnlyList<AnexoSolicitadoDoAto> pedidos, CancellationToken cancellationToken)
    {
        DocumentoAPublicar principal = documentos[ids[0]];
        if (principal.Formato != FormatoDeDocumento.Pdf)
        {
            return Falha(AtoDoProcessoErrorCodes.DocumentoPrincipalNaoEhPdf, "O documento principal do ato deve ser um PDF.");
        }
        Result<IReadOnlyList<AnexoConferido>> anexos = ConferirNomesDosAnexos(pedidos, documentos);
        return anexos.IsFailure
            ? Result<DocumentosDoAtoConferidos>.Failure(anexos.Error!)
            : await ConferirUsoAsync(processoId, ids, new(principal, anexos.Value!), cancellationToken).ConfigureAwait(false);
    }

    private static Result<IReadOnlyList<AnexoConferido>> ConferirNomesDosAnexos(
        IReadOnlyList<AnexoSolicitadoDoAto> pedidos, Dictionary<Guid, DocumentoAPublicar> documentos)
    {
        List<AnexoConferido> anexos = [];
        HashSet<string> nomesUsados = new(NomeDoAnexoPublicado.Comparador);
        foreach (AnexoSolicitadoDoAto pedido in pedidos)
        {
            Result<AnexoConferido> anexo = ConferirNomeDoAnexo(pedido, documentos[pedido.DocumentoId], nomesUsados);
            if (anexo.IsFailure)
            {
                return Result<IReadOnlyList<AnexoConferido>>.Failure(anexo.Error!);
            }
            anexos.Add(anexo.Value!);
        }
        return Result<IReadOnlyList<AnexoConferido>>.Success(anexos);
    }

    private static Result<AnexoConferido> ConferirNomeDoAnexo(
        AnexoSolicitadoDoAto pedido, DocumentoAPublicar documento, HashSet<string> nomesUsados)
    {
        string? nome = NomeDeArquivoEnviado.Normalizar(pedido.Nome, documento.Extensao);
        if (nome is null)
        {
            return Result<AnexoConferido>.Failure(new(AtoDoProcessoErrorCodes.AnexoNomeInvalido,
                $"O nome do anexo é obrigatório, tem até {NomeDeArquivoEnviado.TamanhoMaximo} caracteres com a extensão e não tem separador de caminho, caractere de controle nem {NomeDeArquivoEnviado.CaracteresProibidos}."));
        }
        return !nomesUsados.Add(NomeDoAnexoPublicado.Normalizar(nome))
            ? Result<AnexoConferido>.Failure(new(AtoDoProcessoErrorCodes.AnexoNomeRepetido,
                "Há mais de um anexo com o mesmo nome no ato. O nome do download leva a extensão do arquivo, então 'quadro' e 'quadro.pdf' são o mesmo nome."))
            : Result<AnexoConferido>.Success(new(documento, nome));
    }

    private async Task<Result<DocumentosDoAtoConferidos>> ConferirUsoAsync(
        Guid processoId, Guid[] ids, DocumentosDoAtoConferidos documentos, CancellationToken cancellationToken)
    {
        IReadOnlySet<Guid> jaUsados = await atoDoProcessoRepository
            .ListarDocumentosJaUsadosAsync(processoId, ids, cancellationToken).ConfigureAwait(false);
        return jaUsados.Count > 0
            ? Falha(AtoDoProcessoErrorCodes.DocumentoJaUsadoEmOutroAto,
                "O documento já foi usado em outro ato do processo. Envie o arquivo de novo para publicá-lo neste ato.")
            : Result<DocumentosDoAtoConferidos>.Success(documentos);
    }

    private static Result<DocumentosDoAtoConferidos> Falha(string codigo, string mensagem) =>
        Result<DocumentosDoAtoConferidos>.Failure(new DomainError(codigo, mensagem));
}

/// <summary>Os documentos do ato depois da conferência: o principal e os anexos com o nome do download.</summary>
internal sealed record DocumentosDoAtoConferidos(DocumentoAPublicar Principal, IReadOnlyList<AnexoConferido> Anexos);

/// <summary>Um anexo conferido: o documento e o nome, já com a extensão do formato.</summary>
internal sealed record AnexoConferido(DocumentoAPublicar Documento, string Nome);
