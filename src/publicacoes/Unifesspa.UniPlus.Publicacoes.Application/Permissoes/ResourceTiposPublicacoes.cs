namespace Unifesspa.UniPlus.Publicacoes.Application.Permissoes;

/// <summary>
/// Os tipos de resource que a decisão de acesso enxerga em Publicações, no formato de
/// <c>ResourceTipos</c>. A restrição de tipo de uma concessão é comparada por igualdade exata, então cada valor é o
/// contrato com quem restringe uma concessão por tipo.
/// </summary>
public static class ResourceTiposPublicacoes
{
    /// <summary>Tipo <c>ato-normativo</c>.</summary>
    public const string AtoNormativo = "ato-normativo";

    /// <summary>Tipo <c>tipo-ato</c>.</summary>
    public const string TipoAto = "tipo-ato";
}
