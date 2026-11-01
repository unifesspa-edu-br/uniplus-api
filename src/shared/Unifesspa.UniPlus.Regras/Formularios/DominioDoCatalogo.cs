namespace Unifesspa.UniPlus.Regras.Formularios;

/// <summary>Os tokens de domínio e de cardinalidade de um fato no catálogo, como a Configuração os grava.</summary>
public static class DominioDoCatalogo
{
    public const string Booleano = "BOOLEANO";
    public const string Numerico = "NUMERICO";
    public const string Categorico = "CATEGORICO";
    public const string Texto = "TEXTO";
    public const string Data = "DATA";
    public const string Endereco = "ENDERECO";

    public static readonly IReadOnlyList<string> Dominios = [Booleano, Numerico, Categorico, Texto, Data, Endereco];

    public const string Escalar = "ESCALAR";
    public const string Multivalorado = "MULTIVALORADO";
    public static readonly IReadOnlyList<string> Cardinalidades = [Escalar, Multivalorado];

    /// <summary>A origem do fato cujo valor o sistema ou uma regra calcula.</summary>
    public const string OrigemDerivada = "DERIVADO";

    /// <summary>
    /// As hipóteses legais de tratamento no vocabulário fechado da Configuração (LGPD, arts. 7º e 11), para
    /// quem congela o token sem alcançar o enum do catálogo.
    /// </summary>
    public static readonly IReadOnlyList<string> HipotesesLegais =
    [
        "CONSENTIMENTO", "CUMPRIMENTO_OBRIGACAO_LEGAL", "EXECUCAO_POLITICAS_PUBLICAS", "ESTUDOS_POR_ORGAO_DE_PESQUISA",
        "EXECUCAO_CONTRATO", "EXERCICIO_REGULAR_DE_DIREITOS", "PROTECAO_DA_VIDA", "TUTELA_DA_SAUDE", "INTERESSE_LEGITIMO",
        "PROTECAO_DO_CREDITO", "PREVENCAO_A_FRAUDE",
    ];

    public const string FonteGeoUf = "GEO_UF";
    public const string FonteGeoMunicipio = "GEO_MUNICIPIO";
    public const string FonteTiposDeficiencia = "TIPOS_DEFICIENCIA";
}
