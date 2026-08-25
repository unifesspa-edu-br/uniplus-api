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

        // Mesma ordem do comando de escrita (ADR-0125): forma de VoBase/PR de
        // TODAS as ofertas antes de qualquer I/O cross-módulo.
        List<FieldError> formaErros = [];
        for (int indice = 0; indice < query.DistribuicaoVagas.Count; indice++)
        {
            ConfiguracaoDistribuicaoVagasInput itemForma = query.DistribuicaoVagas[indice];
            formaErros.AddRange(ConfiguracaoDistribuicaoVagas
                .ValidarFormaBasica(itemForma.VoBase, itemForma.Pr, itemForma.ModalidadeIds.Count)
                .Select(erro => erro.Field is null ? erro : erro with { Field = $"distribuicaoVagas[{indice}].{ConfiguracaoDistribuicaoVagasResolver.TraduzirFieldDoDominio(erro.Field)}" }));
        }

        if (formaErros.Count > 0)
        {
            return Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>>.ValidationFailure(formaErros);
        }

        List<ConfiguracaoDistribuicaoVagasDto> simulacao = [];
        for (int indice = 0; indice < query.DistribuicaoVagas.Count; indice++)
        {
            Result<ConfiguracaoDistribuicaoVagas> resultado = await ConfiguracaoDistribuicaoVagasResolver.ResolverDistribuicaoAsync(
                query.DistribuicaoVagas[indice],
                regraCatalogoReader,
                ofertaCursoReader,
                modalidadeReader,
                referenciaReservaDemograficaReader,
                cancellationToken).ConfigureAwait(false);

            if (resultado.IsFailure)
            {
                IReadOnlyList<FieldError> errosComIndice = [.. resultado.Errors
                    .Select(erro => erro.Field is null ? erro : erro with { Field = $"distribuicaoVagas[{indice}].{ConfiguracaoDistribuicaoVagasResolver.TraduzirFieldDoDominio(erro.Field)}" })];
                return Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>>.ValidationFailure(errosComIndice);
            }

            // Nunca tocado por SalvarAlteracoesAsync — os Guids em
            // resultado.Value!.Id (e de cada VagaOfertada) são efêmeros,
            // gerados em memória por ConfiguracaoDistribuicaoVagas.Criar/
            // VagaOfertada.Criar (EntityBase.Id = Guid.CreateVersion7()),
            // nunca persistidos.
            simulacao.Add(ObterProcessoSeletivoQueryHandler.ProjectDistribuicaoVagas(resultado.Value!));
        }

        return Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>>.Success(simulacao);
    }
}
