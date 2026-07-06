namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Kernel.Results;

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
    Guid? ReferenciaReservaDemograficaId,
    IReadOnlyList<Guid> ModalidadeIds);

/// <summary>
/// Substitui integralmente a distribuição de vagas do processo (Story #773,
/// modelagem P-A): uma <c>ConfiguracaoDistribuicaoVagas</c> por oferta de
/// curso. O <c>QuadroDeVagas</c> (quantidade calculada por modalidade) não é
/// definido aqui — é output derivado de um motor futuro sobre estes inputs.
/// </summary>
public sealed record DefinirDistribuicaoVagasCommand(
    Guid ProcessoSeletivoId,
    IReadOnlyList<ConfiguracaoDistribuicaoVagasInput> DistribuicaoVagas) : ICommand<Result>;
