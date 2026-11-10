namespace Unifesspa.UniPlus.Autorizacao.Application.Permissoes;

/// <summary>
/// Os tipos de resource que a decisão de acesso enxerga em Autorização, no formato de
/// <c>ResourceTipos</c>. A restrição de tipo de uma concessão é comparada por igualdade exata, então cada valor é o
/// contrato com quem restringe uma concessão por tipo.
/// </summary>
public static class ResourceTiposAutorizacao
{
    /// <summary>Tipo <c>concessao</c>.</summary>
    public const string Concessao = "concessao";

    /// <summary>Tipo <c>instalacao</c>.</summary>
    public const string Instalacao = "instalacao";

    /// <summary>Tipo <c>pedido-de-concessao</c>.</summary>
    public const string Pedido = "pedido-de-concessao";

    /// <summary>Tipo <c>revisao-de-acessos</c>.</summary>
    public const string Revisao = "revisao-de-acessos";

    /// <summary>Tipo <c>perfil</c>.</summary>
    public const string Perfil = "perfil";

    /// <summary>Tipo <c>reclassificacao-de-permissao</c>.</summary>
    public const string Reclassificacao = "reclassificacao-de-permissao";
}
