namespace Unifesspa.UniPlus.Regras.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// De onde vêm os valores de um dado de opção do catálogo (ADR-0136). Quem consome o dado decide pela fonte, e nunca
/// pelo código do dado. Dado que não é de opção não tem fonte: a ausência é o valor nulo, sem membro sentinela. É o
/// mesmo enum na Configuração, que cadastra o dado, e na Seleção, que decide a coleta pelo catálogo (ADR-0176).
/// </summary>
[DescricaoDoContrato("De onde vêm os valores de um dado de opção do catálogo.")]
public enum FonteValoresFato
{
    /// <summary>Os valores vivem no próprio catálogo (ex.: COR_RACA, SEXO, NACIONALIDADE).</summary>
    [CodigoDeContrato("CATALOGO", "Catálogo", Descricao = "Os valores vivem no próprio catálogo, como a cor ou raça, o sexo e a nacionalidade.")]
    Catalogo = 1,

    /// <summary>
    /// Os valores são as opções que cada processo declara, congeladas no edital (ex.: CONDICAO_ATENDIMENTO e
    /// RECURSO_ACESSIBILIDADE, a partir do atendimento especializado).
    /// </summary>
    [CodigoDeContrato("OPCOES_DO_PROCESSO", "Opções do processo", Descricao = "Os valores são as opções que cada processo declara, congeladas no edital.")]
    OpcoesDoProcesso = 2,

    /// <summary>Os valores são as modalidades ofertadas pelo processo (MODALIDADE).</summary>
    [CodigoDeContrato("MODALIDADE", "Modalidades do processo", Descricao = "Os valores são as modalidades de concorrência ofertadas pelo processo.")]
    Modalidade = 3,

    /// <summary>Os valores são os municípios da área do Fator de Atenuação das Desigualdades de Acesso (FADA) que o processo configurou, pelo código IBGE.</summary>
    [CodigoDeContrato("MUNICIPIOS_DO_FADA", "Municípios do Fator de Atenuação das Desigualdades de Acesso (FADA)", Descricao = "Os valores são os municípios da área do Fator de Atenuação das Desigualdades de Acesso (FADA) que o processo configurou, pelo código do IBGE.")]
    MunicipiosDoFada = 4,

    /// <summary>As 27 unidades federativas, pela sigla: as do derivado de residência e as que o candidato declara.</summary>
    [CodigoDeContrato("UNIDADES_FEDERATIVAS", "Unidades federativas", Descricao = "Os valores são as 27 unidades federativas, pela sigla: as do dado calculado de residência e as que o candidato declara.")]
    UnidadesFederativas = 5,

    /// <summary>
    /// Os municípios do módulo Geo, pelo código IBGE: os do derivado de residência e os que o candidato declara entre os
    /// da unidade federativa respondida antes.
    /// </summary>
    [CodigoDeContrato("MUNICIPIOS", "Municípios", Descricao = "Os valores são os municípios do módulo de geografia, pelo código do IBGE, entre os da unidade federativa respondida antes.")]
    Municipios = 6,

    /// <summary>
    /// Os tipos do cadastro de tipos de deficiência, pelo código: todos os vivos, em qualquer processo,
    /// independentemente do atendimento especializado (UNI-REQ-0012).
    /// </summary>
    [CodigoDeContrato("TIPOS_DE_DEFICIENCIA", "Tipos de deficiência", Descricao = "Os valores são os tipos do cadastro de tipos de deficiência, todos os vivos, em qualquer processo.")]
    TiposDeDeficiencia = 7,

    /// <summary>Os municípios declarados para a prova presencial, pelo código IBGE.</summary>
    [CodigoDeContrato("MUNICIPIOS_PROVA_PRESENCIAL", "Municípios da prova presencial", Descricao = "Os valores são os municípios declarados para a prova presencial, pelo código do IBGE.")]
    MunicipiosProvaPresencial = 8,
}
