namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

internal static class LeituraDosTokensDeVagas
{
    public static NaturezaLegalModalidade NaturezaLegal(this LeitorEnvelope leitor, JsonObject pai, string chave, string path) =>
        Reconhecer<NaturezaLegalModalidade>(leitor, pai, chave, path);

    public static ComposicaoVagasModalidade ComposicaoDeVagas(this LeitorEnvelope leitor, JsonObject pai, string chave, string path) =>
        Reconhecer<ComposicaoVagasModalidade>(leitor, pai, chave, path);

    public static RegraRemanejamentoModalidade? RegraDeRemanejamento(this LeitorEnvelope leitor, JsonObject pai, string chave, string path) =>
        !leitor.Falhou && pai[chave] is null ? null : Reconhecer<RegraRemanejamentoModalidade>(leitor, pai, chave, path);

    private static TEnum Reconhecer<TEnum>(LeitorEnvelope leitor, JsonObject pai, string chave, string path)
        where TEnum : struct, Enum
    {
        if (leitor.Falhou)
        {
            return default;
        }

        string texto = leitor.Texto(pai, chave, path);
        if (CatalogoDe<TEnum>.TentarObter(texto, out TEnum valor))
        {
            return valor;
        }

        return leitor.Falhou ? default : leitor.Malformado<TEnum>($"{path}.{chave}", $"valor não é um {typeof(TEnum).Name} conhecido.");
    }
}
