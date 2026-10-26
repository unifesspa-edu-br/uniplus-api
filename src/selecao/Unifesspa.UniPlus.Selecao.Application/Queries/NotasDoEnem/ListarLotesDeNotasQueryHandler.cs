namespace Unifesspa.UniPlus.Selecao.Application.Queries.NotasDoEnem;

using Abstractions;

using Commands.NotasDoEnem;

using Domain.Entities;
using Domain.Errors;
using Domain.Interfaces;

using DTOs.NotasDoEnem;

using Kernel.Results;

/// <summary>Histórico dos lotes de notas do ENEM da edição, sem as ocorrências.</summary>
public static class ListarLotesDeNotasQueryHandler
{
    public static async Task<Result<IReadOnlyList<LoteDeNotasDoEnemDto>>> Handle(
        ListarLotesDeNotasQuery query,
        ICertameDivulgadoRepository certameDivulgadoRepository,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        ILoteDeNotasDoEnemRepository lotes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(lotes);

        Result<EscopoDeNotasDoEnem> escopo = await ResolvedorDoEscopoDeNotasDoEnem
            .ResolverAsync(
                query.ProcessoSeletivoId, query.EtapaProcessoId, query.Edicao, exigeNotaDoEnem: false,
                certameDivulgadoRepository, processoSeletivoRepository, registroCodecs, cancellationToken)
            .ConfigureAwait(false);
        if (escopo.IsFailure)
        {
            return Result<IReadOnlyList<LoteDeNotasDoEnemDto>>.Failure(escopo.Error!);
        }

        IReadOnlyList<LoteDeNotasDoEnem> historico = await lotes
            .ListarAsync(query.ProcessoSeletivoId, query.EtapaProcessoId, query.Edicao, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyDictionary<Guid, int> ausentes = await lotes
            .ContarAusentesPorLoteAsync([.. historico.Select(static l => l.Id)], cancellationToken)
            .ConfigureAwait(false);

        // O repositório devolve do mais recente para o mais antigo: o primeiro é o vigente.
        return Result<IReadOnlyList<LoteDeNotasDoEnemDto>>.Success(
            [.. historico.Select((lote, posicao) => LoteDeNotasDoEnemDto.De(lote, vigente: posicao == 0, detalhe: null, ausentes.GetValueOrDefault(lote.Id)))]);
    }
}
