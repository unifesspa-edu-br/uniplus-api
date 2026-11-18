namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Leitura, no envelope, dos três campos da modalidade que têm um token canônico único em toda a plataforma
/// (o mesmo do cadastro de modalidades): a natureza legal, a composição de vagas e a regra de remanejamento.
/// O envelope grava o token, e não o nome do enum; um token fora do vocabulário é recusado.
/// </summary>
internal static class LeituraDosTokensDeVagas
{
    public static NaturezaLegalModalidade NaturezaLegal(this LeitorEnvelope leitor, JsonObject pai, string chave, string path) =>
        Reconhecer(leitor, pai, chave, path, NaturezaLegalModalidadeCodigo.FromCodigo, NaturezaLegalModalidade.Nenhuma);

    public static ComposicaoVagasModalidade ComposicaoDeVagas(this LeitorEnvelope leitor, JsonObject pai, string chave, string path) =>
        Reconhecer(leitor, pai, chave, path, ComposicaoVagasModalidadeCodigo.FromCodigo, ComposicaoVagasModalidade.Nenhuma);

    /// <summary>A modalidade que não remaneja não tem token: o campo é nulo, como no cadastro.</summary>
    public static RegraRemanejamentoModalidade RegraDeRemanejamento(this LeitorEnvelope leitor, JsonObject pai, string chave, string path) =>
        !leitor.Falhou && pai[chave] is null
            ? RegraRemanejamentoModalidade.Nenhuma
            : Reconhecer(leitor, pai, chave, path, RegraRemanejamentoModalidadeCodigo.FromCodigo, RegraRemanejamentoModalidade.Nenhuma);

    private static TEnum Reconhecer<TEnum>(
        LeitorEnvelope leitor, JsonObject pai, string chave, string path, Func<string?, TEnum> deCodigo, TEnum sentinela)
        where TEnum : struct, Enum
    {
        if (leitor.Falhou)
        {
            return default;
        }

        string texto = leitor.Texto(pai, chave, path);
        TEnum valor = deCodigo(texto);
        return leitor.Falhou || !EqualityComparer<TEnum>.Default.Equals(valor, sentinela)
            ? valor
            : leitor.Malformado<TEnum>($"{path}.{chave}", $"valor '{texto}' não é um {typeof(TEnum).Name} conhecido.");
    }
}
