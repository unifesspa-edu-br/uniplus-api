namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Mapeamento entre <see cref="HipoteseLegalTratamento"/> (PascalCase) e o token textual de contrato/banco
/// (UPPER_SNAKE), com parsing de domínio fechado por allowlist explícita (molde de
/// <see cref="CardinalidadesFato"/>). Fonte do CHECK de domínio em
/// <c>rol_de_fatos_candidato.hipotese_legal</c> e do value converter de persistência.
/// </summary>
public static class HipotesesLegaisTratamento
{
    private static readonly Dictionary<HipoteseLegalTratamento, string> ParaToken = new()
    {
        [HipoteseLegalTratamento.Consentimento] = "CONSENTIMENTO",
        [HipoteseLegalTratamento.CumprimentoObrigacaoLegal] = "CUMPRIMENTO_OBRIGACAO_LEGAL",
        [HipoteseLegalTratamento.ExecucaoPoliticasPublicas] = "EXECUCAO_POLITICAS_PUBLICAS",
        [HipoteseLegalTratamento.EstudosPorOrgaoDePesquisa] = "ESTUDOS_POR_ORGAO_DE_PESQUISA",
        [HipoteseLegalTratamento.ExecucaoContrato] = "EXECUCAO_CONTRATO",
        [HipoteseLegalTratamento.ExercicioRegularDeDireitos] = "EXERCICIO_REGULAR_DE_DIREITOS",
        [HipoteseLegalTratamento.ProtecaoDaVida] = "PROTECAO_DA_VIDA",
        [HipoteseLegalTratamento.TutelaDaSaude] = "TUTELA_DA_SAUDE",
        [HipoteseLegalTratamento.InteresseLegitimo] = "INTERESSE_LEGITIMO",
        [HipoteseLegalTratamento.ProtecaoDoCredito] = "PROTECAO_DO_CREDITO",
        [HipoteseLegalTratamento.PrevencaoAFraude] = "PREVENCAO_A_FRAUDE",
    };

    private static readonly Dictionary<string, HipoteseLegalTratamento> DeToken =
        ParaToken.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.Ordinal);

    /// <summary>Os tokens canônicos (UPPER_SNAKE), para o CHECK de domínio e mensagens.</summary>
    public static readonly IReadOnlyList<string> TokensCanonicos = [.. ParaToken.Values];

    /// <summary>Token textual de contrato/banco (UPPER_SNAKE) de um valor válido.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Se o valor é o sentinela ou está fora do roster.</exception>
    public static string ParaTokenCanonico(HipoteseLegalTratamento valor) =>
        ParaToken.TryGetValue(valor, out string? token)
            ? token
            : throw new ArgumentOutOfRangeException(nameof(valor), valor, "Hipótese legal de tratamento fora do domínio fechado.");

    /// <summary>Resolve um token textual (UPPER_SNAKE); <see langword="false"/> quando inválido.</summary>
    public static bool TryAnalisar(string? token, out HipoteseLegalTratamento valor)
    {
        if (!string.IsNullOrWhiteSpace(token) && DeToken.TryGetValue(token.Trim(), out HipoteseLegalTratamento resolvido))
        {
            valor = resolvido;
            return true;
        }

        valor = HipoteseLegalTratamento.Nenhuma;
        return false;
    }

    /// <summary>Resolve um token à sua enum (reidratação fail-fast do value converter).</summary>
    /// <exception cref="ArgumentOutOfRangeException">Se <paramref name="token"/> não é canônico.</exception>
    public static HipoteseLegalTratamento Analisar(string? token) =>
        TryAnalisar(token, out HipoteseLegalTratamento valor)
            ? valor
            : throw new ArgumentOutOfRangeException(nameof(token), token, "Token de hipótese legal de tratamento fora do domínio fechado.");

    // Só o art. 7º prevê execução de contrato, interesse legítimo e proteção do crédito; só o
    // art. 11 prevê a prevenção à fraude. As demais hipóteses existem nos dois artigos.
    private static readonly HashSet<HipoteseLegalTratamento> SoDoArtigo7 =
    [
        HipoteseLegalTratamento.ExecucaoContrato,
        HipoteseLegalTratamento.InteresseLegitimo,
        HipoteseLegalTratamento.ProtecaoDoCredito,
    ];

    private static readonly HashSet<HipoteseLegalTratamento> SoDoArtigo11 = [HipoteseLegalTratamento.PrevencaoAFraude];

    /// <summary>
    /// Indica se a hipótese cabe na classificação: as do art. 11 da LGPD para o dado sensível e
    /// as do art. 7º para os demais.
    /// </summary>
    public static bool AdmiteClassificacao(HipoteseLegalTratamento hipotese, ClassificacaoProtecaoDado classificacao) =>
        classificacao == ClassificacaoProtecaoDado.Sensivel
            ? !SoDoArtigo7.Contains(hipotese)
            : !SoDoArtigo11.Contains(hipotese);
}
