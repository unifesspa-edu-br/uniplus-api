namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.ValueObjects;

using DTOs;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Item de entrada de uma distribuição de vagas por oferta, usado por
/// <see cref="DefinirDistribuicaoVagasCommand"/>. O admin referencia a oferta,
/// declara <c>VoBase</c>/<c>PR</c>, escolhe a versão da regra de distribuição
/// (<c>rol_de_regras</c>) e seleciona as modalidades por <c>Id</c> — os demais
/// atributos de cada modalidade (natureza legal, composição, remanejamento)
/// são lidos do cadastro vivo pelo handler (snapshot-copy, ADR-0061), não
/// informados aqui.
/// </summary>
public sealed record ConfiguracaoDistribuicaoVagasInput(
    Guid OfertaCursoId,
    int VoBase,
    decimal Pr,
    string RegraDistribuicaoCodigo,
    string RegraDistribuicaoVersao,
    string? RegraAjusteCodigo,
    string? RegraAjusteVersao,
    Guid? ReferenciaReservaDemograficaId,
    IReadOnlyList<Guid> ModalidadeIds,
    IReadOnlyList<QuantidadeVagaInput> Quadro,
    ArgsRegraAjusteDistribuicao? ArgsAjuste = null);

/// <summary>
/// Quantidade que o edital fixa para uma modalidade — obrigatória para as
/// modalidades de retirada/suplemento no ramo federal, e para todas no ramo
/// institucional; recusada para as modalidades calculadas do ramo federal
/// (issue #848/ADR-0115).
/// </summary>
public sealed record QuantidadeVagaInput(Guid ModalidadeId, int Quantidade);

/// <summary>
/// Substitui integralmente a distribuição de vagas do processo (Story #773,
/// issue #848/ADR-0115): uma <c>ConfiguracaoDistribuicaoVagas</c>
/// por oferta de curso, com o quadro de vagas (calculado no ramo federal,
/// fixado no institucional) materializado na mesma operação.
/// </summary>
public sealed record DefinirDistribuicaoVagasCommand(
    Guid ProcessoSeletivoId,
    IReadOnlyList<ConfiguracaoDistribuicaoVagasInput> DistribuicaoVagas,
    PrecondicaoIfMatch Precondicao) : ICommand<Result<MutacaoComDistribuicaoVagasDto>>;

/// <summary>
/// O que <see cref="DefinirDistribuicaoVagasCommand"/> devolve ao persistir
/// (issue #1283): o mesmo <c>ETag</c> de <see cref="MutacaoAceita"/>, mais a
/// distribuição recém-persistida — inclusive o quadro de vagas calculado —
/// no mesmo shape que a simulação (issue #1282) e o <c>GET</c> do processo
/// devolvem, para o cliente confirmar o que foi gravado sem uma segunda
/// chamada.
/// </summary>
public sealed record MutacaoComDistribuicaoVagasDto(
    string? ETag,
    IReadOnlyList<ConfiguracaoDistribuicaoVagasDto> DistribuicaoVagas);
