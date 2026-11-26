namespace Unifesspa.UniPlus.Selecao.Application.Services.Comprovantes;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
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
    public static IReadOnlyList<TermoAceitoDoComprovanteDto> Termos(InsumosDoComprovante insumos)
    {
        Dictionary<TermoNoFormulario, string> nomes = NomesDosTermos(insumos.Envelope.Configuracao.Coleta);

        return [.. insumos.Comprovante.Aceites.Select(a => new TermoAceitoDoComprovanteDto(
            a.FormularioCodigo, a.Codigo, nomes.GetValueOrDefault(new TermoNoFormulario(a.FormularioCodigo, a.Codigo), a.Codigo), a.VersaoId, a.HashVersao))];
    }

    private static Dictionary<TermoNoFormulario, string> NomesDosTermos(ColetaDeclarada coleta)
    {
        Dictionary<TermoNoFormulario, string> nomes = [];
        foreach (FormularioProcesso formulario in coleta.Formularios)
        {
            foreach (TermoExigidoFormulario termo in coleta.TermosExigidos.Where(t => t.FormularioId == formulario.Id))
            {
                nomes.TryAdd(new TermoNoFormulario(formulario.Codigo.Valor, termo.Codigo), termo.Nome);
            }
        }

        return nomes;
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
            RotuloDaSituacao(documento.Situacao),
            documento.ConsequenciaDoDescumprimento,
            [.. documento.Arquivos.Select(static a => new ArquivoDoComprovanteDto(a.HashSha256, a.TamanhoBytes, a.Formato))]);
    private static string RotuloDaSituacao(string codigo) => codigo == DocumentosDoComprovante.SituacaoDispensada
        ? "Dispensado"
        : CatalogoDe<SituacaoDoDocumento>.TentarObter(codigo, out SituacaoDoDocumento situacao) ? situacao switch
        {
            SituacaoDoDocumento.Satisfeita => "Atendido",
            SituacaoDoDocumento.Pendente => "Pendente",
            SituacaoDoDocumento.PodeSerExigido => "Pode ser exigido, conforme as respostas",
            SituacaoDoDocumento.DependeDeFasePosterior => "Pode ser exigido, conforme uma fase posterior do processo",
            _ => throw new InvalidOperationException("Situação do documento inválida no comprovante."),
        } : "A confirmar";
}
