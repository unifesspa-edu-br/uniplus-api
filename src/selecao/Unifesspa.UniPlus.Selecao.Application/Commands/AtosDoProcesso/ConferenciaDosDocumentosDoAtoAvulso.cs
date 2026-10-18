namespace Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;

using Domain.Entities;
using Domain.Enums;
using Domain.Errors;
using Domain.Interfaces;
using Domain.Services;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Confere os documentos que o ato avulso referencia: cada um é do processo, está confirmado, foi
/// enviado para um ato e não foi usado em outro; o principal é PDF e os anexos têm nomes distintos.
/// </summary>
/// <remarks>
/// Antes de qualquer escrita, porque o ato é registrado depois, por mensagem durável (ADR-0108), e a
/// recusa tardia não teria a quem responder. O documento usado em outro ato é recusado para que o
/// arquivo publicado tenha um único ato por trás; a finalidade já separa o documento do ato do
/// documento e dos anexos do edital.
/// </remarks>
internal static class ConferenciaDosDocumentosDoAtoAvulso
{
    /// <summary>Quantos anexos um ato avulso pode levar.</summary>
    public const int QuantidadeMaximaDeAnexos = 20;

    public static async Task<Result<DocumentosDoAtoConferidos>> ConferirAsync(
        IDocumentoAPublicarRepository documentoRepository,
        IAtoDoProcessoRepository atoDoProcessoRepository,
        Guid processoSeletivoId,
        Guid documentoPrincipalId,
        IReadOnlyList<AnexoSolicitadoDoAto> anexosSolicitados,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(documentoRepository);
        ArgumentNullException.ThrowIfNull(atoDoProcessoRepository);
        ArgumentNullException.ThrowIfNull(anexosSolicitados);

        if (anexosSolicitados.Count > QuantidadeMaximaDeAnexos)
        {
            return Falha(AtoDoProcessoErrorCodes.QuantidadeDeAnexosExcedida,
                $"O ato aceita no máximo {QuantidadeMaximaDeAnexos} anexos.");
        }

        Guid[] ids = [documentoPrincipalId, .. anexosSolicitados.Select(static a => a.DocumentoId)];
        if (ids.Distinct().Count() != ids.Length)
        {
            return Falha(AtoDoProcessoErrorCodes.DocumentoRepetido,
                "O mesmo documento não pode constar duas vezes no ato, nem como principal e anexo.");
        }

        IReadOnlyList<DocumentoAPublicar> encontrados = await documentoRepository
            .ListarDoProcessoAsync(processoSeletivoId, ids, cancellationToken)
            .ConfigureAwait(false);
        Dictionary<Guid, DocumentoAPublicar> porId = encontrados.ToDictionary(static d => d.Id);

        foreach (Guid id in ids)
        {
            if (!porId.TryGetValue(id, out DocumentoAPublicar? documento))
            {
                return Falha(AtoDoProcessoErrorCodes.DocumentoNaoEncontrado,
                    $"Documento {id} não encontrado ou não pertence a este processo.");
            }

            if (documento.Status != StatusArquivoEnviado.Confirmado)
            {
                return Falha(AtoDoProcessoErrorCodes.DocumentoNaoConfirmado,
                    "Somente um documento confirmado pode ser referenciado no ato.");
            }

            if (documento.Finalidade != FinalidadeDoDocumento.DocumentoDeAto)
            {
                return Falha(AtoDoProcessoErrorCodes.DocumentoNaoEnviadoComoDocumentoDeAto,
                    "Somente um documento enviado para um ato pode ser o principal ou o anexo de um ato.");
            }
        }

        DocumentoAPublicar principal = porId[documentoPrincipalId];
        if (principal.Formato != FormatoDeDocumento.Pdf)
        {
            return Falha(AtoDoProcessoErrorCodes.DocumentoPrincipalNaoEhPdf, "O documento principal do ato deve ser um PDF.");
        }

        List<AnexoConferido> anexos = [];
        HashSet<string> nomesUsados = new(NomeDoAnexoPublicado.Comparador);
        foreach (AnexoSolicitadoDoAto pedido in anexosSolicitados)
        {
            DocumentoAPublicar documento = porId[pedido.DocumentoId];
            string? nome = NomeDeArquivoEnviado.Normalizar(pedido.Nome, documento.Extensao);
            if (nome is null)
            {
                return Falha(AtoDoProcessoErrorCodes.AnexoNomeInvalido,
                    $"O nome do anexo é obrigatório, tem até {NomeDeArquivoEnviado.TamanhoMaximo} caracteres com a extensão e não tem separador de caminho, caractere de controle nem {NomeDeArquivoEnviado.CaracteresProibidos}.");
            }

            if (!nomesUsados.Add(NomeDoAnexoPublicado.Normalizar(nome)))
            {
                return Falha(AtoDoProcessoErrorCodes.AnexoNomeRepetido,
                    $"Há mais de um anexo chamado '{nome}' no ato. O nome do download leva a extensão do arquivo, então 'quadro' e 'quadro.pdf' são o mesmo nome.");
            }

            anexos.Add(new AnexoConferido(documento, nome));
        }

        IReadOnlySet<Guid> jaUsados = await atoDoProcessoRepository
            .ListarDocumentosJaUsadosAsync(processoSeletivoId, ids, cancellationToken)
            .ConfigureAwait(false);
        if (jaUsados.Count > 0)
        {
            return Falha(AtoDoProcessoErrorCodes.DocumentoJaUsadoEmOutroAto,
                $"O documento {jaUsados.First()} já foi usado em outro ato do processo. Envie o arquivo de novo para publicá-lo neste ato.");
        }

        return Result<DocumentosDoAtoConferidos>.Success(new DocumentosDoAtoConferidos(principal, anexos));
    }

    private static Result<DocumentosDoAtoConferidos> Falha(string codigo, string mensagem) =>
        Result<DocumentosDoAtoConferidos>.Failure(new DomainError(codigo, mensagem));
}

/// <summary>Os documentos do ato depois da conferência: o principal e os anexos com o nome do download.</summary>
internal sealed record DocumentosDoAtoConferidos(DocumentoAPublicar Principal, IReadOnlyList<AnexoConferido> Anexos);

/// <summary>Um anexo conferido: o documento e o nome, já com a extensão do formato.</summary>
internal sealed record AnexoConferido(DocumentoAPublicar Documento, string Nome);
