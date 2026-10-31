namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Permissoes;

/// <summary>
/// Os tipos de resource que a decisão de acesso enxerga em Organização Institucional, no formato de
/// <c>ResourceTipos</c>. A restrição de tipo de uma concessão é comparada por igualdade exata, então cada valor é o
/// contrato com quem restringe uma concessão por tipo.
/// </summary>
public static class ResourceTiposOrganizacao
{
    /// <summary>Tipo <c>instituicao</c>.</summary>
    public const string Instituicao = "instituicao";

    /// <summary>Tipo <c>unidade</c>.</summary>
    public const string Unidade = "unidade";
}
