namespace Unifesspa.UniPlus.Selecao.Application.Permissoes;

/// <summary>
/// Os tipos de resource que a decisão de acesso enxerga em Seleção, no formato de
/// <c>ResourceTipos</c>. A restrição de tipo de uma concessão é comparada por igualdade exata, então cada valor é o
/// contrato com quem restringe uma concessão por tipo.
/// </summary>
public static class ResourceTiposSelecao
{
    /// <summary>Tipo <c>motivo-de-decisao</c>.</summary>
    public const string MotivoDeDecisao = "motivo-de-decisao";

    /// <summary>Tipo <c>regra-catalogo</c>.</summary>
    public const string RegraCatalogo = "regra-catalogo";

    /// <summary>Tipo <c>obrigatoriedade-legal</c>.</summary>
    public const string ObrigatoriedadeLegal = "obrigatoriedade-legal";

    /// <summary>Tipo <c>leiaute-inep</c>.</summary>
    public const string LeiauteInep = "leiaute-inep";

    /// <summary>Tipo <c>processo-seletivo</c>.</summary>
    public const string ProcessoSeletivo = "processo-seletivo";

    /// <summary>Tipo <c>inscricao</c>.</summary>
    public const string Inscricao = "inscricao";

    /// <summary>Tipo <c>documento-da-inscricao</c>.</summary>
    public const string DocumentoDaInscricao = "documento-da-inscricao";

    /// <summary>Tipo <c>fase-do-processo</c>.</summary>
    public const string FaseDoProcesso = "fase-do-processo";
}
