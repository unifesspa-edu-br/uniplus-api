namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Código canônico da(s) regra(s) de <c>tipo=regra_prazo_recurso</c> do
/// <c>rol_de_regras</c> (Story #854) reconhecidas por
/// <see cref="Entities.RegraRecursoFase"/> (Story #851 §3.0/CA-01).
/// </summary>
/// <remarks>
/// Símbolo, não literal solto: o gate referencia esta constante — nunca o texto
/// <c>"RECURSO-PRAZO-ANCORADO-EM-ATO"</c> espalhado pelo código (CA-01).
/// </remarks>
public static class RegraPrazoRecursoCodigo
{
    /// <summary>Prazo de interposição ancorado no instante de publicação de um ato + suspensividade por instância (#854).</summary>
    public const string AncoradoEmAto = "RECURSO-PRAZO-ANCORADO-EM-ATO";
}
