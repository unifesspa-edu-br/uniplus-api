namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Mapeamento entre <see cref="ComposicaoVagas"/> (domínio, PascalCase) e o código
/// textual de contrato/banco (UPPER_SNAKE), com parsing de domínio fechado.
/// </summary>
/// <remarks>
/// <para>O parsing é por <b>lista textual explícita de códigos aceitos</b> (<see cref="TryAnalisar"/>):
/// só os quatro códigos canônicos são aceitos. Deliberadamente <b>não</b> usa
/// <c>Enum.TryParse</c>, que aceitaria códigos numéricos e nomes PascalCase do enum
/// — ambos fora do contrato textual da #589.</para>
/// <para>É o vocabulário fonte do CHECK de domínio em <c>modalidade.composicao_vagas</c>
/// (<see cref="TokensCanonicos"/>) e do value converter de persistência.</para>
/// </remarks>
public static class ComposicoesVagas
{
    private static readonly Dictionary<ComposicaoVagas, string> ParaToken = new()
    {
        [ComposicaoVagas.RestanteDasVagasOfertadas] = "RESTANTE_DAS_VAGAS_OFERTADAS",
        [ComposicaoVagas.DentroDaReservaLegal] = "DENTRO_DA_RESERVA_LEGAL",
        [ComposicaoVagas.RetiradaDeOutraModalidade] = "RETIRADA_DE_OUTRA_MODALIDADE",
        [ComposicaoVagas.SuplementarAoTotal] = "SUPLEMENTAR_AO_TOTAL",
    };

    private static readonly Dictionary<string, ComposicaoVagas> DeToken =
        ParaToken.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.Ordinal);

    /// <summary>Os quatro códigos canônicos (UPPER_SNAKE), para o CHECK de domínio e mensagens.</summary>
    public static readonly IReadOnlyList<string> TokensCanonicos = [.. ParaToken.Values];

    /// <summary>Código textual de contrato/banco (UPPER_SNAKE) de uma composição válida.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Se <paramref name="composicao"/> é <see cref="ComposicaoVagas.Nenhuma"/> ou fora dos valores declarados.</exception>
    public static string ParaTokenCanonico(ComposicaoVagas composicao) =>
        ParaToken.TryGetValue(composicao, out string? codigo)
            ? codigo
            : throw new ArgumentOutOfRangeException(
                nameof(composicao), composicao, "Composição de vagas fora do domínio fechado.");

    /// <summary>
    /// Resolve um código textual (UPPER_SNAKE) à composição correspondente. Aceita
    /// <c>Trim</c>, mas é case-sensitive e rejeita códigos numéricos ou fora do
    /// domínio (lista de códigos aceitos). Retorna <see langword="false"/> quando inválido.
    /// </summary>
    public static bool TryAnalisar(string? codigo, out ComposicaoVagas composicao)
    {
        if (!string.IsNullOrWhiteSpace(codigo)
            && DeToken.TryGetValue(codigo.Trim(), out ComposicaoVagas resolvida))
        {
            composicao = resolvida;
            return true;
        }

        composicao = ComposicaoVagas.Nenhuma;
        return false;
    }

    /// <summary>Indica se <paramref name="codigo"/> é um dos quatro códigos canônicos, sem alocar resultado.</summary>
    public static bool EhValido(string? codigo) => TryAnalisar(codigo, out _);
}
