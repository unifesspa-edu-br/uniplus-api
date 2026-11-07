namespace Unifesspa.UniPlus.Selecao.Application.Services.Comprovantes;

using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Comprovantes;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Os termos aceitos e as exigências documentais do comprovante, com o texto pronto: o nome do termo é o da versão da
/// inscrição, e a situação do documento sai por extenso.
/// </summary>
internal static class TermosEDocumentosDoComprovante
{
    private const string SituacaoDesconhecida = "A confirmar";

    private static readonly Dictionary<string, string> RotuloDaSituacao = new(StringComparer.Ordinal)
    {
        [SituacaoDoDocumentoCodigo.Satisfeita] = "Atendido",
        [SituacaoDoDocumentoCodigo.Pendente] = "Pendente",
        [DocumentosDoComprovante.SituacaoDispensada] = "Dispensado",
        [SituacaoDoDocumentoCodigo.PodeSerExigido] = "Pode ser exigido, conforme as respostas",
        [SituacaoDoDocumentoCodigo.DependeDeFasePosterior] = "Pode ser exigido, conforme uma fase posterior do processo",
    };

    public static IReadOnlyList<TermoAceitoDoComprovanteDto> Termos(InsumosDoComprovante insumos)
    {
        Dictionary<string, string> nomes = [];
        foreach (TermoExigidoFormulario termo in insumos.Envelope.Grafo.TermosExigidos.Where(static t => t.Finalidade == FinalidadeFormulario.Inscricao))
        {
            nomes.TryAdd(termo.Codigo, termo.Nome);
        }

        return [.. insumos.Comprovante.Aceites.Select(a => new TermoAceitoDoComprovanteDto(a.Codigo, nomes.GetValueOrDefault(a.Codigo, a.Codigo), a.VersaoId, a.HashVersao))];
    }

    public static IReadOnlyList<DocumentoDoComprovanteDto> Documentos(InsumosDoComprovante insumos, FatosDaVersao fatos)
    {
        Dictionary<string, string> integrantes = TitulosDasOcorrencias.Dos(fatos.Grupos, insumos.Comprovante.Grupos);

        return [.. insumos.Comprovante.Documentos.Select(d => Documento(d, integrantes))];
    }

    private static DocumentoDoComprovanteDto Documento(DocumentoDoComprovante documento, Dictionary<string, string> integrantes) =>
        new(
            documento.Nome,
            documento.OcorrenciaId is { } ocorrencia ? integrantes.GetValueOrDefault(ocorrencia) : null,
            documento.Obrigatorio,
            RotuloDaSituacao.GetValueOrDefault(documento.Situacao, SituacaoDesconhecida),
            documento.Consequencia,
            [.. documento.Arquivos.Select(static a => new ArquivoDoComprovanteDto(a.HashSha256, a.TamanhoBytes, a.Formato))]);
}
