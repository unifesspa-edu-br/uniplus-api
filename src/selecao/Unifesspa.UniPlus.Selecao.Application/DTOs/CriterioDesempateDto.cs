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
