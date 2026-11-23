namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Confere as fases que o prazo de emissão e a validade de cada exigência usam como referência contra o
/// cronograma do processo.
/// </summary>
internal static class DatasDeReferenciaNoCronograma
{
    private const string CampoDoPrazo = "do prazo de emissão";
    private const string CampoDaValidade = "da validade";

    /// <summary>Uma recusa por exigência e campo cuja fase de referência não pertence ao cronograma.</summary>
    internal static IReadOnlyList<FieldError> ConferirConjunto(IReadOnlyList<FaseCronograma> fases, IEnumerable<DocumentoExigido> documentos) =>
        [.. documentos.SelectMany(documento => ReferenciasDoDocumento(documento)
            .Where(referenciada => referenciada.Referencia.FaseId is not null && !fases.Any(fase => referenciada.Referencia.UsaAFase(fase.Id)))
            .Select(referenciada => new FieldError(null, new DomainError(
                DataDeReferenciaDoDocumentoErrorCodes.FaseNaoPertenceAoProcesso,
                $"A fase de referência {referenciada.Campo} do documento '{documento.TipoDocumentoNome}' não pertence ao cronograma deste processo."))))];

    internal static IReadOnlyList<FieldError> ConferirCompletude(IReadOnlyList<FaseCronograma> fases, IEnumerable<DocumentoExigido> documentos) =>
        [.. documentos.SelectMany(documento => CompletudeDoDocumento(fases, documento))];

    private static IEnumerable<FieldError> CompletudeDoDocumento(IReadOnlyList<FaseCronograma> fases, DocumentoExigido documento)
    {
        if (documento.PrazoDeEmissao is null)
            yield return Recusa("PrazoDeEmissao.EscolhaNaoDeclarada", documento, CampoDoPrazo, "Faça a escolha");
        else if (documento.PrazoDeEmissao.Referencia is { } prazo && RecusaDaReferencia(fases, documento, CampoDoPrazo, prazo) is { } recusaDoPrazo)
            yield return recusaDoPrazo;
        if (documento.Validade is null)
            yield return Recusa("ValidadeDoDocumento.EscolhaNaoDeclarada", documento, CampoDaValidade, "Faça a escolha");
        else if (documento.Validade.Referencia is { } validade && RecusaDaReferencia(fases, documento, CampoDaValidade, validade) is { } recusaDaValidade)
            yield return recusaDaValidade;
    }

    private static FieldError? RecusaDaReferencia(IReadOnlyList<FaseCronograma> fases, DocumentoExigido documento, string campo, DataDeReferenciaDoDocumento referencia)
    {
        if (referencia.Tipo == TipoDaDataDeReferencia.FimDasInscricoes && !fases.Any(static fase => fase.ColetaInscricao && fase.Fim.HasValue))
            return Recusa("DataDeReferenciaDoDocumento.FimDasInscricoesIndefinido", documento, campo, "Defina o término de uma fase que coleta inscrição");
        FaseCronograma? fase = fases.FirstOrDefault(fase => referencia.UsaAFase(fase.Id));
        if (fase is not null && ((referencia.Tipo == TipoDaDataDeReferencia.InicioDaFase && fase.Inicio is null)
            || (referencia.Tipo == TipoDaDataDeReferencia.FimDaFase && fase.Fim is null)))
            return Recusa("DataDeReferenciaDoDocumento.DataDaFaseIndefinida", documento, campo, "Defina a data da fase de referência");
        return null;
    }

    private static FieldError Recusa(string codigo, DocumentoExigido documento, string campo, string orientacao) =>
        new(null, new DomainError(codigo, $"{orientacao} {campo} do documento '{documento.TipoDocumentoNome}'."));

    /// <summary>O campo da exigência (prazo de emissão ou validade) que usa a fase como referência; nulo quando nenhum usa.</summary>
    internal static string? CampoQueUsaAFase(DocumentoExigido documento, Guid faseId) =>
        ReferenciasDoDocumento(documento).FirstOrDefault(referenciada => referenciada.Referencia.UsaAFase(faseId)).Campo;

    private static IEnumerable<(string Campo, DataDeReferenciaDoDocumento Referencia)> ReferenciasDoDocumento(DocumentoExigido documento)
    {
        if (documento.PrazoDeEmissao?.Referencia is { } doPrazo)
        {
            yield return (CampoDoPrazo, doPrazo);
        }

        if (documento.Validade?.Referencia is { } daValidade)
        {
            yield return (CampoDaValidade, daValidade);
        }
    }
}
