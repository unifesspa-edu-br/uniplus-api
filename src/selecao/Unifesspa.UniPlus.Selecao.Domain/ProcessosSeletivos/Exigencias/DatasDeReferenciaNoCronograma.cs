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
    private const string NomeDoPrazo = "prazoDeEmissao";
    private const string NomeDaValidade = "validade";
    private const string DaReferencia = "referencia";

    /// <summary>
    /// Uma recusa por exigência e campo cuja fase de referência não pertence ao cronograma. Os campos apontam o
    /// caminho de <paramref name="caminhosDosRequisitos"/>, o de onde ficam o prazo de emissão e a validade de cada exigência.
    /// </summary>
    internal static IReadOnlyList<FieldError> ConferirConjunto(
        IReadOnlyList<FaseCronograma> fases, IReadOnlyCollection<DocumentoExigido> documentos, IReadOnlyList<string> caminhosDosRequisitos)
    {
        CaminhosDasExigencias.ExigirUmParaCada(caminhosDosRequisitos, documentos.Count, nameof(caminhosDosRequisitos));
        return [.. documentos.SelectMany((documento, indice) => ReferenciasDoDocumento(documento)
            .Where(referenciada => referenciada.Referencia.FaseId is not null && !fases.Any(fase => referenciada.Referencia.UsaAFase(fase.Id)))
            .Select(referenciada => new FieldError(
                PonteiroDaReferencia(caminhosDosRequisitos[indice], referenciada.Nome),
                new DomainError(
                    DataDeReferenciaDoDocumentoErrorCodes.FaseNaoPertenceAoProcesso,
                    $"A fase de referência {referenciada.Campo} do documento '{documento.TipoDocumentoNome}' não pertence ao cronograma deste processo."))))];
    }

    /// <summary>As escolhas e as datas de referência que faltam, apontando cada exigência pelo caminho de <paramref name="caminhosDasExigencias"/>.</summary>
    internal static IReadOnlyList<FieldError> ConferirCompletude(
        IReadOnlyList<FaseCronograma> fases, IReadOnlyCollection<DocumentoExigido> documentos, IReadOnlyList<string> caminhosDasExigencias)
    {
        CaminhosDasExigencias.ExigirUmParaCada(caminhosDasExigencias, documentos.Count, nameof(caminhosDasExigencias));
        return [.. documentos.SelectMany((documento, indice) => CompletudeDoDocumento(fases, documento, caminhosDasExigencias[indice]))];
    }

    private static IEnumerable<FieldError> CompletudeDoDocumento(IReadOnlyList<FaseCronograma> fases, DocumentoExigido documento, string prefixo)
    {
        if (documento.PrazoDeEmissao is null)
            yield return RecusaDaEscolha("PrazoDeEmissao.EscolhaNaoDeclarada", documento, prefixo, (CampoDoPrazo, NomeDoPrazo));
        else if (documento.PrazoDeEmissao.Referencia is { } prazo && RecusaDaReferencia(fases, documento, prefixo, (CampoDoPrazo, NomeDoPrazo, prazo)) is { } recusaDoPrazo)
            yield return recusaDoPrazo;
        if (documento.Validade is null)
            yield return RecusaDaEscolha("ValidadeDoDocumento.EscolhaNaoDeclarada", documento, prefixo, (CampoDaValidade, NomeDaValidade));
        else if (documento.Validade.Referencia is { } validade && RecusaDaReferencia(fases, documento, prefixo, (CampoDaValidade, NomeDaValidade, validade)) is { } recusaDaValidade)
            yield return recusaDaValidade;
    }

    private static FieldError? RecusaDaReferencia(
        IReadOnlyList<FaseCronograma> fases, DocumentoExigido documento, string prefixo, (string Campo, string Nome, DataDeReferenciaDoDocumento Referencia) referenciada)
    {
        DataDeReferenciaDoDocumento referencia = referenciada.Referencia;
        string ponteiro = PonteiroDaReferencia(prefixo, referenciada.Nome);
        if (referencia.Tipo == TipoDaDataDeReferencia.FimDasInscricoes && !fases.Any(static fase => fase.ColetaInscricao && fase.Fim.HasValue))
            return Recusa(ponteiro, "DataDeReferenciaDoDocumento.FimDasInscricoesIndefinido", documento, referenciada.Campo, "Defina o término de uma fase que coleta inscrição");
        FaseCronograma? fase = fases.FirstOrDefault(fase => referencia.UsaAFase(fase.Id));
        if (fase is not null && ((referencia.Tipo == TipoDaDataDeReferencia.InicioDaFase && fase.Inicio is null)
            || (referencia.Tipo == TipoDaDataDeReferencia.FimDaFase && fase.Fim is null)))
            return Recusa(ponteiro, "DataDeReferenciaDoDocumento.DataDaFaseIndefinida", documento, referenciada.Campo, "Defina a data da fase de referência");
        return null;
    }

    private static FieldError RecusaDaEscolha(string codigo, DocumentoExigido documento, string prefixo, (string Campo, string Nome) campo) =>
        Recusa($"{prefixo}.{campo.Nome}", codigo, documento, campo.Campo, "Faça a escolha");

    private static FieldError Recusa(string ponteiro, string codigo, DocumentoExigido documento, string campo, string orientacao) =>
        new(ponteiro, new DomainError(codigo, $"{orientacao} {campo} do documento '{documento.TipoDocumentoNome}'."));

    private static string PonteiroDaReferencia(string prefixo, string nome) => $"{prefixo}.{nome}.{DaReferencia}";

    /// <summary>O campo da exigência (prazo de emissão ou validade) que usa a fase como referência; nulo quando nenhum usa.</summary>
    internal static string? CampoQueUsaAFase(DocumentoExigido documento, Guid faseId) =>
        ReferenciasDoDocumento(documento).FirstOrDefault(referenciada => referenciada.Referencia.UsaAFase(faseId)).Campo;

    private static IEnumerable<(string Campo, string Nome, DataDeReferenciaDoDocumento Referencia)> ReferenciasDoDocumento(DocumentoExigido documento)
    {
        if (documento.PrazoDeEmissao?.Referencia is { } doPrazo)
        {
            yield return (CampoDoPrazo, NomeDoPrazo, doPrazo);
        }

        if (documento.Validade?.Referencia is { } daValidade)
        {
            yield return (CampoDaValidade, NomeDaValidade, daValidade);
        }
    }
}
