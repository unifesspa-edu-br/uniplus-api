namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json.Nodes;

/// <summary>
/// Leitura do grupo de área do ENEM congelado: na distribuição de vagas, onde pode faltar, e no quadro de
/// pesos por área da classificação, onde é obrigatório.
/// </summary>
internal static class LeituraDoGrupoDeAreaEnem
{
    /// <summary>
    /// O grupo congelado na distribuição: <see langword="null"/> quando o curso da oferta não o declara,
    /// senão o par que <see cref="LerObjeto"/> lê. As invariantes do grupo são da factory da distribuição,
    /// que recebe o par cru.
    /// </summary>
    internal static (string? Codigo, string? Rotulo)? LerOpcional(LeitorEnvelope leitor, JsonObject distribuicao, string pathPai)
    {
        JsonObject? item = leitor.ObjetoOpcional(distribuicao, "grupoAreaEnem", pathPai);
        return leitor.Falhou || item is null ? null : LerObjeto(leitor, item, $"{pathPai}.grupoAreaEnem");
    }

    /// <summary>O grupo de área do ENEM obrigatório, como no quadro de pesos por área da classificação.</summary>
    internal static (string Codigo, string Rotulo)? LerObrigatorio(LeitorEnvelope leitor, JsonObject pai, string pathPai)
    {
        JsonObject item = leitor.Objeto(pai, "grupoAreaEnem", pathPai);
        return leitor.Falhou ? null : LerObjeto(leitor, item, $"{pathPai}.grupoAreaEnem");
    }

    /// <summary>
    /// O objeto <c>grupoAreaEnem</c> já obtido — <c>{codigo, rotulo}</c>, os dois obrigatórios e
    /// dentro do limite das colunas —, a mesma forma na distribuição de vagas e no quadro de
    /// pesos por área da classificação.
    /// </summary>
    internal static (string Codigo, string Rotulo)? LerObjeto(LeitorEnvelope leitor, JsonObject item, string path)
    {
        leitor.ExigirChaves(item, path, "codigo", "rotulo");
        string codigo = leitor.TextoNaoVazio(item, "codigo", path, LimitesDoEnvelope.GrupoAreaEnemCodigo);
        string rotulo = leitor.TextoNaoVazio(item, "rotulo", path, LimitesDoEnvelope.GrupoAreaEnemRotulo);
        return leitor.Falhou ? null : (codigo, rotulo);
    }
}
