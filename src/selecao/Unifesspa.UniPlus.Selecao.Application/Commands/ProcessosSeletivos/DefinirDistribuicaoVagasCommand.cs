namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using System.ComponentModel;

using Domain.ValueObjects;

using DTOs;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Item de entrada de uma distribuição de vagas por oferta, usado por
/// <see cref="DefinirDistribuicaoVagasCommand"/>. O admin referencia a oferta,
/// declara as vagas ofertadas base e o percentual da reserva legal (este só na Lei de Cotas), escolhe a versão da regra de distribuição
/// (<c>regras_do_catalogo</c>) e seleciona as modalidades por <c>Id</c> — os demais
/// atributos de cada modalidade (natureza legal, composição, remanejamento)
/// são lidos do cadastro vivo pelo handler (snapshot-copy, ADR-0061), não
/// informados aqui. As modalidades e o quadro são obrigatórios: ausentes ou nulos são recusados (422) na conferência da
/// forma do pedido.
/// </summary>
public sealed record ConfiguracaoDistribuicaoVagasInput(
    Guid OfertaCursoId,
    int VagasOfertadasBase,
    decimal? PercentualDaReservaLegal,
    [property: ObrigatorioNoContrato]
    [property: Description("Obrigatório; ausente ou nulo é recusado (422).")]
    string? RegraDistribuicaoCodigo,
    [property: ObrigatorioNoContrato]
    [property: Description("Obrigatório; ausente ou nulo é recusado (422).")]
    string? RegraDistribuicaoVersao,
    string? RegraAjusteCodigo,
    string? RegraAjusteVersao,
    Guid? ReferenciaReservaDemograficaId,
    [property: ObrigatorioNoContrato]
    [property: Description("Obrigatório; ausente ou nulo é recusado (422).")]
    IReadOnlyList<Guid>? ModalidadeIds,
    [property: ObrigatorioNoContrato]
    [property: Description("Obrigatório; ausente ou nulo é recusado (422); envie [] quando não houver quantidade a fixar.")]
    IReadOnlyList<QuantidadeVagaInput>? Quadro,
    ParametrosRegraAjusteDistribuicao? AjusteDoQuadro = null);

/// <summary>
/// Quantidade que o edital fixa para uma modalidade — obrigatória para as
/// modalidades de retirada/suplemento no ramo federal, e para todas no ramo
/// institucional; recusada para as modalidades calculadas do ramo federal
/// (issue #848/ADR-0115).
/// </summary>
public sealed record QuantidadeVagaInput(
    Guid ModalidadeId,
    [property: ObrigatorioNoContrato]
    [property: Description("Obrigatório; ausente ou nulo é recusado (422).")]
    int? Quantidade);

/// <summary>
/// Substitui integralmente a distribuição de vagas do processo (Story #773,
/// issue #848/ADR-0115): uma <c>ConfiguracaoDistribuicaoVagas</c>
/// por oferta de curso, com o quadro de vagas (calculado no ramo federal,
/// fixado no institucional) materializado na mesma operação. Devolve a distribuição recém-persistida,
/// inclusive o quadro de vagas calculado, no mesmo formato que a simulação e o <c>GET</c> do processo.
/// </summary>
public sealed record DefinirDistribuicaoVagasCommand(
    Guid ProcessoSeletivoId,
    IReadOnlyList<ConfiguracaoDistribuicaoVagasInput> DistribuicaoVagas) : ICommand<Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>>>;
