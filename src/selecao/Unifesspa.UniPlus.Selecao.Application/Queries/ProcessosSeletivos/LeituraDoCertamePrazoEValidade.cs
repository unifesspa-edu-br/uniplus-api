namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;

using Domain.ValueObjects;

using DTOs;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// Leitura, no envelope congelado, do prazo de emissão e da validade de uma exigência: a coerência entre os campos
/// vem dos próprios tipos do domínio, e a data de referência, do cronograma congelado da mesma versão.
/// </summary>
internal static class LeituraDoCertamePrazoEValidade
{
    /// <summary>
    /// As duas chaves são obrigatórias, nulas quando a escolha não foi feita. O resultado é nulo quando o documento
    /// não tem prazo de emissão ou não exige validade: o candidato só vê o que o documento lhe pede.
    /// </summary>
    internal static bool TentarPrazoEValidade(
        JsonObject exigencia,
        CronogramaCongelado cronograma,
        out PrazoDeEmissaoCertameDto? prazo,
        out ValidadeDoDocumentoCertameDto? validade)
    {
        prazo = null;
        validade = null;
        return TentarBloco(exigencia, "prazoDeEmissao", out JsonObject? blocoDoPrazo)
            && TentarBloco(exigencia, "validade", out JsonObject? blocoDaValidade)
            && TentarPrazo(blocoDoPrazo, cronograma, out prazo)
            && TentarValidade(blocoDaValidade, cronograma, out validade);
    }

    private static bool TentarBloco(JsonObject exigencia, string chave, out JsonObject? bloco)
    {
        bloco = null;
        if (!exigencia.TryGetPropertyValue(chave, out JsonNode? node))
        {
            return false;
        }

        bloco = node as JsonObject;
        return node is null || bloco is not null;
    }

    private static bool TentarPrazo(JsonObject? bloco, CronogramaCongelado cronograma, out PrazoDeEmissaoCertameDto? prazo)
    {
        prazo = null;
        if (bloco is null)
        {
            return true;
        }

        if (!TentarPrazoDoDominio(bloco, out PrazoDeEmissao? doDominio))
        {
            return false;
        }

        prazo = Projetar(doDominio, cronograma);
        return true;
    }

    private static bool TentarPrazoDoDominio(JsonObject bloco, [NotNullWhen(true)] out PrazoDeEmissao? prazo)
    {
        prazo = null;
        if (!PrimitivasDaProjecao.TentarCodigo(bloco, "escolha", out EscolhaDoPrazoDeEmissao escolha)
            || !PrimitivasDaProjecao.TentarInteiroOpcional(bloco, "valor", out int? valor)
            || !PrimitivasDaProjecao.TentarCodigoOpcional(bloco, "unidade", out UnidadeDoPrazoDeEmissao? unidade)
            || !TentarReferencia(bloco, out DataDeReferenciaDoDocumento? referencia)
            || !PrimitivasDaProjecao.TentarCodigoOpcional(bloco, "efeitoForaDoPrazo", out EfeitoNaAnaliseDoDocumento? efeito))
        {
            return false;
        }

        Result<PrazoDeEmissao> criado = PrazoDeEmissao.Criar(escolha, valor, unidade, referencia, efeito);
        prazo = criado.IsSuccess ? criado.Value : null;
        return prazo is not null;
    }

    private static bool TentarValidade(JsonObject? bloco, CronogramaCongelado cronograma, out ValidadeDoDocumentoCertameDto? validade)
    {
        validade = null;
        if (bloco is null)
        {
            return true;
        }

        if (!TentarValidadeDoDominio(bloco, out ValidadeDoDocumento? doDominio))
        {
            return false;
        }

        validade = Projetar(doDominio, cronograma);
        return true;
    }

    private static bool TentarValidadeDoDominio(JsonObject bloco, [NotNullWhen(true)] out ValidadeDoDocumento? validade)
    {
        validade = null;
        if (!PrimitivasDaProjecao.TentarCodigo(bloco, "escolha", out EscolhaDaValidade escolha)
            || !TentarReferencia(bloco, out DataDeReferenciaDoDocumento? referencia)
            || !PrimitivasDaProjecao.TentarCodigoOpcional(bloco, "efeitoDoVencimento", out EfeitoNaAnaliseDoDocumento? efeito))
        {
            return false;
        }

        Result<ValidadeDoDocumento> criada = ValidadeDoDocumento.Criar(escolha, referencia, efeito);
        validade = criada.IsSuccess ? criada.Value : null;
        return validade is not null;
    }

    private static bool TentarReferencia(JsonObject pai, out DataDeReferenciaDoDocumento? referencia)
    {
        referencia = null;
        return pai.TryGetPropertyValue("referencia", out JsonNode? node)
            && (node is null || (node is JsonObject bloco && TentarReferenciaDoBloco(bloco, out referencia)));
    }

    private static bool TentarReferenciaDoBloco(JsonObject bloco, [NotNullWhen(true)] out DataDeReferenciaDoDocumento? referencia)
    {
        referencia = null;
        if (!PrimitivasDaProjecao.TentarCodigo(bloco, "tipo", out TipoDaDataDeReferencia tipo)
            || !PrimitivasDaProjecao.TentarDataOpcional(bloco, "data", out DateOnly? data)
            || !PrimitivasDaProjecao.TentarIdentificadorOpcional(bloco, "faseId", out Guid? faseId))
        {
            return false;
        }

        Result<DataDeReferenciaDoDocumento> criada = DataDeReferenciaDoDocumento.Criar(tipo, data, faseId);
        referencia = criada.IsSuccess ? criada.Value : null;
        return referencia is not null;
    }

    private static PrazoDeEmissaoCertameDto? Projetar(PrazoDeEmissao prazo, CronogramaCongelado cronograma) => prazo is
    {
        Escolha: EscolhaDoPrazoDeEmissao.ComPrazo,
        Valor: { } valor,
        Unidade: { } unidade,
        Referencia: { } referencia,
        EfeitoForaDoPrazo: { } efeito,
    }
        ? new PrazoDeEmissaoCertameDto(
            CodigoComRotulo<EscolhaDoPrazoDeEmissao>.De(prazo.Escolha),
            valor,
            CodigoComRotulo<UnidadeDoPrazoDeEmissao>.De(unidade),
            Projetar(referencia, cronograma),
            CodigoComRotulo<EfeitoNaAnaliseDoDocumento>.De(efeito))
        : null;

    private static ValidadeDoDocumentoCertameDto? Projetar(ValidadeDoDocumento validade, CronogramaCongelado cronograma) => validade is
    {
        Escolha: EscolhaDaValidade.ExigeDocumentoValido,
        Referencia: { } referencia,
        EfeitoDoVencimento: { } efeito,
    }
        ? new ValidadeDoDocumentoCertameDto(
            CodigoComRotulo<EscolhaDaValidade>.De(validade.Escolha),
            Projetar(referencia, cronograma),
            CodigoComRotulo<EfeitoNaAnaliseDoDocumento>.De(efeito))
        : null;

    private static ReferenciaDoDocumentoCertameDto Projetar(DataDeReferenciaDoDocumento referencia, CronogramaCongelado cronograma) => new(
        CodigoComRotulo<TipoDaDataDeReferencia>.De(referencia.Tipo),
        cronograma.DataDe(referencia),
        referencia.FaseId);
}
