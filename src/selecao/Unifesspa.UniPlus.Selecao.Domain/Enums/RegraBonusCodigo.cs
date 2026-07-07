namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Código canônico da regra de <c>tipo=regra_bonus</c> do <c>rol_de_regras</c>
/// (Story #772) reconhecida pela configuração de bônus regional (Story #774,
/// modelagem P-B §2.5).
/// </summary>
public static class RegraBonusCodigo
{
    /// <summary>Multiplica a nota final pelo fator, após os pesos (ex.: ×1,20, sem teto — RN05).</summary>
    public const string Multiplicativo = "BONUS-MULTIPLICATIVO";
}
