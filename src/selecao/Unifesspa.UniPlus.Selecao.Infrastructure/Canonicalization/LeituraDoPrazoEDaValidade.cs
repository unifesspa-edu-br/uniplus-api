namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Leitura do prazo de emissão e da validade de uma exigência no envelope: cada código pelo valor exato que a
/// escrita grava, e a coerência entre os campos pelos próprios tipos do domínio.
/// </summary>
internal static class LeituraDoPrazoEDaValidade
{
    internal static PrazoDeEmissao? LerPrazo(LeitorEnvelope leitor, JsonObject item, string pathPai)
    {
        JsonObject? bloco = leitor.ObjetoOpcional(item, "prazoDeEmissao", pathPai);
        if (leitor.Falhou || bloco is null)
        {
            return null;
        }

        string path = $"{pathPai}.prazoDeEmissao";
        leitor.ExigirChaves(bloco, path, "escolha", "valor", "unidade", "referencia", "efeitoForaDoPrazo");
        EscolhaDoPrazoDeEmissao escolha = leitor.CodigoDeContrato<EscolhaDoPrazoDeEmissao>(bloco, "escolha", path);
        int? valor = leitor.InteiroOpcional(bloco, "valor", path);
        UnidadeDoPrazoDeEmissao? unidade = CodigoOpcional<UnidadeDoPrazoDeEmissao>(leitor, bloco, "unidade", path);
        DataDeReferenciaDoDocumento? referencia = LerReferencia(leitor, bloco, path);
        EfeitoNaAnaliseDoDocumento? efeito = CodigoOpcional<EfeitoNaAnaliseDoDocumento>(leitor, bloco, "efeitoForaDoPrazo", path);
        return leitor.Falhou ? null : Declarado(leitor, PrazoDeEmissao.Criar(escolha, valor, unidade, referencia, efeito), path);
    }

    internal static ValidadeDoDocumento? LerValidade(LeitorEnvelope leitor, JsonObject item, string pathPai)
    {
        JsonObject? bloco = leitor.ObjetoOpcional(item, "validade", pathPai);
        if (leitor.Falhou || bloco is null)
        {
            return null;
        }

        string path = $"{pathPai}.validade";
        leitor.ExigirChaves(bloco, path, "escolha", "referencia", "efeitoDoVencimento");
        EscolhaDaValidade escolha = leitor.CodigoDeContrato<EscolhaDaValidade>(bloco, "escolha", path);
        DataDeReferenciaDoDocumento? referencia = LerReferencia(leitor, bloco, path);
        EfeitoNaAnaliseDoDocumento? efeito = CodigoOpcional<EfeitoNaAnaliseDoDocumento>(leitor, bloco, "efeitoDoVencimento", path);
        return leitor.Falhou ? null : Declarado(leitor, ValidadeDoDocumento.Criar(escolha, referencia, efeito), path);
    }

    private static DataDeReferenciaDoDocumento? LerReferencia(LeitorEnvelope leitor, JsonObject pai, string pathPai)
    {
        JsonObject? bloco = leitor.ObjetoOpcional(pai, "referencia", pathPai);
        if (leitor.Falhou || bloco is null)
        {
            return null;
        }

        string path = $"{pathPai}.referencia";
        leitor.ExigirChaves(bloco, path, "tipo", "data", "faseId");
        TipoDaDataDeReferencia tipo = leitor.CodigoDeContrato<TipoDaDataDeReferencia>(bloco, "tipo", path);
        DateOnly? data = leitor.DataOpcional(bloco, "data", path);
        Guid? faseId = leitor.IdentificadorOpcional(bloco, "faseId", path);
        return leitor.Falhou ? null : Declarado(leitor, DataDeReferenciaDoDocumento.Criar(tipo, data, faseId), path);
    }

    private static TEnum? CodigoOpcional<TEnum>(LeitorEnvelope leitor, JsonObject pai, string chave, string path)
        where TEnum : struct, Enum =>
        leitor.Falhou || pai[chave] is null ? null : leitor.CodigoDeContrato<TEnum>(pai, chave, path);

    private static T? Declarado<T>(LeitorEnvelope leitor, Result<T> criado, string caminhoDoBloco)
        where T : class =>
        criado.IsFailure ? leitor.Propagar<T?>(criado.Errors, caminhoDoBloco) : criado.Value;
}
