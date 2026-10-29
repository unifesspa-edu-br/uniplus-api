namespace Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// Mapeamento entre <see cref="ClassificacaoDeProtecao"/> (PascalCase) e o token textual de contrato/banco
/// (UPPER_SNAKE), com parsing de domínio fechado por allowlist explícita. Fonte única do token para
/// todo módulo que guarda ou congela a classificação: o CHECK de domínio em
/// <c>rol_de_fatos_candidato.classificacao_protecao</c>, os conversores de persistência e o envelope
/// congelado da publicação.
/// </summary>
public static class ClassificacoesDeProtecao
{
    private static readonly Dictionary<ClassificacaoDeProtecao, string> ParaToken = new()
    {
        [ClassificacaoDeProtecao.Publico] = "PUBLICO",
        [ClassificacaoDeProtecao.Interno] = "INTERNO",
        [ClassificacaoDeProtecao.Pessoal] = "PESSOAL",
        [ClassificacaoDeProtecao.Identificador] = "IDENTIFICADOR",
        [ClassificacaoDeProtecao.Sensivel] = "SENSIVEL",
    };

    private static readonly Dictionary<string, ClassificacaoDeProtecao> DeToken =
        ParaToken.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.Ordinal);

    /// <summary>Os tokens canônicos (UPPER_SNAKE), para o CHECK de domínio e mensagens.</summary>
    public static readonly IReadOnlyList<string> TokensCanonicos = [.. ParaToken.Values];

    /// <summary>Token textual de contrato/banco (UPPER_SNAKE) de um valor válido.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Se o valor é o sentinela ou está fora do roster.</exception>
    public static string ParaTokenCanonico(ClassificacaoDeProtecao valor) =>
        ParaToken.TryGetValue(valor, out string? token)
            ? token
            : throw new ArgumentOutOfRangeException(nameof(valor), valor, "Classificação de proteção de dados fora do domínio fechado.");

    /// <summary>Resolve um token textual (UPPER_SNAKE); <see langword="false"/> quando inválido.</summary>
    public static bool TryAnalisar(string? token, out ClassificacaoDeProtecao valor)
    {
        if (!string.IsNullOrWhiteSpace(token) && DeToken.TryGetValue(token.Trim(), out ClassificacaoDeProtecao resolvido))
        {
            valor = resolvido;
            return true;
        }

        valor = ClassificacaoDeProtecao.Nenhuma;
        return false;
    }

    /// <summary>Resolve um token à sua enum (reidratação fail-fast do value converter).</summary>
    /// <exception cref="ArgumentOutOfRangeException">Se <paramref name="token"/> não é canônico.</exception>
    public static ClassificacaoDeProtecao Analisar(string? token) =>
        TryAnalisar(token, out ClassificacaoDeProtecao valor)
            ? valor
            : throw new ArgumentOutOfRangeException(nameof(token), token, "Token de classificação de proteção de dados fora do domínio fechado.");
}
