namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Código canônico da regra de <c>tipo=regra_bonus</c> do <c>rol_de_regras</c>
/// (Story #772) reconhecida pela configuração de bônus regional (Story #774).
/// </summary>
public static class RegraBonusCodigo
{
    /// <summary>Multiplica a nota final pelo fator, após os pesos (ex.: ×1,20, sem teto — Fator de Atenuação das Desigualdades de Acesso, UNI-REQ-0015).</summary>
    public const string Multiplicativo = "BONUS-MULTIPLICATIVO";
}
