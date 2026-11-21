namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// Projeção de leitura de um critério de desempate (Story #774): a regra referenciada e os parâmetros, na mesma forma
/// que <c>CriterioDesempateInput</c> recebe.
/// </summary>
public sealed record CriterioDesempateDto(
    Guid Id,
    int Ordem,
    ReferenciaRegraDto Regra,
    ParametrosDoCriterioDesempateDto Parametros);

/// <summary>
/// Os parâmetros de um critério de desempate — apenas o(s) relevante(s) para o código da regra referenciada vem(êm)
/// preenchido(s).
/// </summary>
/// <remarks>
/// <see cref="Valor"/> é o texto JSON canônico do valor da condição — <c>"PRETA"</c> com
/// aspas, <c>18</c> sem —, o mesmo formato de <c>CondicaoGatilhoDto.Valor</c>: as duas
/// leituras carregam a mesma tripla (fato, operador, valor), e uma segunda convenção para o
/// mesmo campo obrigaria o cliente a saber de qual das duas ela veio. Round-tripável direto de
/// volta pelo mesmo PUT.
/// <para>
/// <see cref="Areas"/> traz só os códigos das áreas do ENEM, na ordem de desempate, como o
/// PUT os recebe. O rótulo de cada área está no quadro de pesos por área da classificação
/// (<see cref="ConfiguracaoClassificacaoDto.QuadroPesoAreaEnem"/>), a única fonte de área no
/// processo.
/// </para>
/// </remarks>
public sealed record ParametrosDoCriterioDesempateDto(
    Guid? EtapaRef,
    int? IdadeMinima,
    string? Fato,
    [property: AceitaCodigosDe<Operador>] string? Operador,
    string? Valor,
    IReadOnlyList<string>? Areas);

/// <summary>Projeção de leitura do bônus regional (Fator de Atenuação das Desigualdades de Acesso, UNI-REQ-0015, Story #774), com o snapshot congelado da Base Legal (Story #1466).</summary>
public sealed record ConfiguracaoBonusRegionalDto(
    Guid Id,
    ReferenciaRegraDto Regra,
    decimal Fator,
    decimal? Teto,
    string? FatoElegibilidadeCodigo,
    BaseLegalCongeladaDto BaseLegal);

/// <summary>O snapshot congelado da Base Legal do bônus regional: o instrumento normativo e os municípios que ele beneficia.</summary>
/// <param name="TipoInstrumento">O código do tipo do instrumento normativo, do vocabulário <c>tipos-instrumento-normativo</c> da Configuração.</param>
public sealed record BaseLegalCongeladaDto(
    Guid Id,
    string TipoInstrumento,
    string Identificacao,
    string Descricao,
    IReadOnlyList<ConfiguracaoBonusRegionalMunicipioDto> Municipios);

/// <summary>Um município do snapshot congelado do bônus regional.</summary>
public sealed record ConfiguracaoBonusRegionalMunicipioDto(
    string CodigoIbge,
    string Nome,
    string Uf);
