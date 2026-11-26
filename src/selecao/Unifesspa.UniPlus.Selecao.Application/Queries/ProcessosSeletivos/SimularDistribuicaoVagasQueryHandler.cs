namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;

using DTOs;

using Kernel.Results;

using Services;

/// <summary>
/// Handler do <see cref="SimularDistribuicaoVagasQuery"/> (issue #1282):
/// resolve cada oferta contra os mesmos cadastros vivos e o mesmo catálogo
/// que <c>DefinirDistribuicaoVagasCommandHandler</c> usa, mas nunca carrega o
/// <see cref="ProcessoSeletivo"/> para mutação nem chama
/// <c>SalvarAlteracoesAsync</c> — só confirma que o processo existe
/// (<see cref="IProcessoSeletivoRepository.ExisteAsync"/>) e devolve o
/// resultado calculado em memória.
/// </summary>
public static class SimularDistribuicaoVagasQueryHandler
{
    public static async Task<Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>>> Handle(
        SimularDistribuicaoVagasQuery query,
        IProcessoSeletivoRepository processoSeletivoRepository,
        ConfiguracaoDistribuicaoVagasResolver resolvedor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        Result<IReadOnlyList<ConfiguracaoDistribuicaoVagas>> simulada =
            await SimularAsync(query, processoSeletivoRepository, resolvedor, cancellationToken).ConfigureAwait(false);

        // Nunca tocadas por SalvarAlteracoesAsync: os Guids da configuração e de cada VagaOfertada são
        // efêmeros, gerados em memória, e nunca persistidos.
        return simulada.IsFailure
            ? Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>>.ValidationFailure(simulada.Errors)
            : Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>>.Success(
                [.. simulada.Value!.Select(ProjecaoDasVagas.ProjetarDistribuicao)]);
    }

    private static async Task<Result<IReadOnlyList<ConfiguracaoDistribuicaoVagas>>> SimularAsync(
        SimularDistribuicaoVagasQuery query,
        IProcessoSeletivoRepository processoSeletivoRepository,
        ConfiguracaoDistribuicaoVagasResolver resolvedor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(resolvedor);

        if (!await processoSeletivoRepository.ExisteAsync(query.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false))
        {
            return Result<IReadOnlyList<ConfiguracaoDistribuicaoVagas>>.Failure(AcessoAoProcesso.NaoEncontrado());
        }

        return await resolvedor.ResolverTodasAsync(query.DistribuicaoVagas, cancellationToken).ConfigureAwait(false);
    }
}
