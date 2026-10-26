namespace Unifesspa.UniPlus.Selecao.Application.Queries.NotasDoEnem;

using Abstractions;

using Commands.NotasDoEnem;

using Domain.Entities;
using Domain.Errors;
using Domain.Interfaces;

using DTOs.NotasDoEnem;

using Kernel.Results;

using Services;

/// <summary>O detalhe de um lote de notas do ENEM, com as ocorrências.</summary>
public static class ObterLoteDeNotasQueryHandler
{
    public static async Task<Result<LoteDeNotasDoEnemDto>> Handle(
        ObterLoteDeNotasQuery query,
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
            return Result<LoteDeNotasDoEnemDto>.Failure(escopo.Error!);
        }

        LoteDeNotasDoEnem? lote = await lotes
            .ObterComOcorrenciasAsync(query.ProcessoSeletivoId, query.EtapaProcessoId, query.Edicao, query.LoteId, cancellationToken)
            .ConfigureAwait(false);
        if (lote is null)
        {
            return Result<LoteDeNotasDoEnemDto>.Failure(new DomainError(
                NotasDoEnemErrorCodes.LoteNaoEncontrado,
                "O lote informado não pertence a esta edição do processo."));
        }

        LoteDeNotasDoEnem? vigente = await lotes
            .ObterVigenteAsync(query.ProcessoSeletivoId, query.EtapaProcessoId, query.Edicao, cancellationToken)
            .ConfigureAwait(false);

        return Result<LoteDeNotasDoEnemDto>.Success(await DetalheDoLoteDeNotasMontador
            .MontarAsync(lotes, lote, vigente?.Id == lote.Id, processoSeletivoRepository, registroCodecs, cancellationToken)
            .ConfigureAwait(false));
    }
}
