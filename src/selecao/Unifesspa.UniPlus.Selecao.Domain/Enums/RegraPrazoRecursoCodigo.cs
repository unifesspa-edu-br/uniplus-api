namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Código canônico da regra de <c>tipo=regra_prazo_recurso</c> do <c>regras_do_catalogo</c> que
/// <see cref="Entities.RegraRecursoFase"/> reconhece.
/// </summary>
/// <remarks>
/// Símbolo, não literal solto: a conferência da regra referencia esta constante, nunca o texto
/// <c>"RECURSO-PRAZO-ANCORADO-EM-ATO"</c> espalhado pelo código.
/// </remarks>
public static class RegraPrazoRecursoCodigo
{
    /// <summary>Prazo de interposição e prazo da 2ª instância contados da publicação de um ato.</summary>
    public const string AncoradoEmAto = "RECURSO-PRAZO-ANCORADO-EM-ATO";
}
