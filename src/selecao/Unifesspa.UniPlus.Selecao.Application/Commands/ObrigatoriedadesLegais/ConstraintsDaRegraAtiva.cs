namespace Unifesspa.UniPlus.Selecao.Application.Commands.ObrigatoriedadesLegais;

/// <summary>
/// Reconhece as constraints de unicidade parcial da regra ativa (código e hash canônico) no nome que a gravação violou.
/// A corrida entre a consulta prévia e a gravação só o banco enxerga, e o handler a devolve como recusa nomeada.
/// </summary>
internal static class ConstraintsDaRegraAtiva
{
    private const string CodigoDaRegra = "ux_obrigatoriedades_legais_regra_codigo_ativos";

    private const string HashCanonico = "ux_obrigatoriedades_legais_hash_ativos";

    /// <summary><see langword="true"/> quando a constraint violada é a do código da regra ativa.</summary>
    public static bool EhConflitoDoCodigoDaRegra(string? constraint) =>
        string.Equals(constraint, CodigoDaRegra, StringComparison.Ordinal);

    /// <summary><see langword="true"/> quando a constraint violada é a do hash canônico da regra ativa.</summary>
    public static bool EhConflitoDoHashDaRegra(string? constraint) =>
        string.Equals(constraint, HashCanonico, StringComparison.Ordinal);
}
