namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;

using DTOs;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;

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
        IRegraCatalogoReader regraCatalogoReader,
        IOfertaCursoReader ofertaCursoReader,
        IModalidadeReader modalidadeReader,
        IReferenciaReservaDemograficaReader referenciaReservaDemograficaReader,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(regraCatalogoReader);
        ArgumentNullException.ThrowIfNull(ofertaCursoReader);
        ArgumentNullException.ThrowIfNull(modalidadeReader);
        ArgumentNullException.ThrowIfNull(referenciaReservaDemograficaReader);

        bool existe = await processoSeletivoRepository
            .ExisteAsync(query.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (!existe)
        {
            return Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>>.Failure(new DomainError(
                "ProcessoSeletivo.NaoEncontrado",
                $"Processo Seletivo {query.ProcessoSeletivoId} não encontrado."));
        }

        // Mesma ordem do comando de escrita (ADR-0125): a forma de todas as ofertas antes de qualquer I/O.
        IReadOnlyList<FieldError> formaErros = ConfiguracaoDistribuicaoVagasResolver.ConferirForma(query.DistribuicaoVagas);
        if (formaErros.Count > 0)
        {
            return Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>>.ValidationFailure(formaErros);
        }

        Result<IReadOnlyList<ConfiguracaoDistribuicaoVagas>> resolvidas = await ConfiguracaoDistribuicaoVagasResolver.ResolverTodasAsync(
            query.DistribuicaoVagas,
            regraCatalogoReader,
            ofertaCursoReader,
            modalidadeReader,
            referenciaReservaDemograficaReader,
            cancellationToken).ConfigureAwait(false);
        if (resolvidas.IsFailure)
        {
            return Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>>.ValidationFailure(resolvidas.Errors);
        }

        // Nunca tocadas por SalvarAlteracoesAsync: os Guids da configuração e de cada VagaOfertada são
        // efêmeros, gerados em memória, e nunca persistidos.
        IReadOnlyList<ConfiguracaoDistribuicaoVagasDto> simulacao =
            [.. resolvidas.Value!.Select(ProjecaoDasVagas.ProjetarDistribuicao)];

        return Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>>.Success(simulacao);
    }
}
