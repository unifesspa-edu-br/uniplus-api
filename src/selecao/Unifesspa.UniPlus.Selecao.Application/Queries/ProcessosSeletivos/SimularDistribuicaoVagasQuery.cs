namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using Commands.ProcessosSeletivos;

using DTOs;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Calcula, sem persistir, o quadro de vagas por modalidade que
/// <see cref="DefinirDistribuicaoVagasCommand"/> produziria para o mesmo
/// payload (issue #1282) — preview para o admin conferir/ajustar VoBase, PR
/// ou modalidades antes de confirmar a gravação real.
/// </summary>
/// <remarks>
/// É uma Query, não um Command: despachada via <c>IQueryBus</c>, nunca toca
/// <c>ISelecaoUnitOfWork</c>. Reaproveita a mesma resolução cross-módulo
/// (<see cref="ConfiguracaoDistribuicaoVagasResolver"/>) e a mesma projeção
/// de leitura (<see cref="ProcessoSeletivoDto"/>) usadas pelo comando de
/// escrita e pela leitura persistida, para nunca divergir do que o PUT real
/// calcularia e gravaria.
/// </remarks>
public sealed record SimularDistribuicaoVagasQuery(
    Guid ProcessoSeletivoId,
    IReadOnlyList<ConfiguracaoDistribuicaoVagasInput> DistribuicaoVagas)
    : IQuery<Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>>>;
