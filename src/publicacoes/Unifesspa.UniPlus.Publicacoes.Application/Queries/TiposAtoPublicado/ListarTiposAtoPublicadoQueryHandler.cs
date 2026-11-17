namespace Unifesspa.UniPlus.Publicacoes.Application.Queries.TiposAtoPublicado;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Application.DTOs;
using Unifesspa.UniPlus.Publicacoes.Application.Mappings;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;

public static class ListarTiposAtoPublicadoQueryHandler
{
    public static async Task<Result<ListarTiposAtoPublicadoResult>> Handle(
        ListarTiposAtoPublicadoQuery query,
        ITipoAtoPublicadoRepository repository,
        IResolvedorFusoInstitucional fuso,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(fuso);
        ArgumentNullException.ThrowIfNull(timeProvider);

        Result<DateOnly?> vigentesEm = DiaDaVigencia(query, fuso, timeProvider);

        return vigentesEm.IsFailure
            ? Result<ListarTiposAtoPublicadoResult>.Failure(vigentesEm.Error!)
            : await ListarAsync(query, vigentesEm.Value, repository, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<ListarTiposAtoPublicadoResult>> ListarAsync(
        ListarTiposAtoPublicadoQuery query,
        DateOnly? vigentesEm,
        ITipoAtoPublicadoRepository repository,
        CancellationToken cancellationToken)
    {
        (IReadOnlyList<TipoAtoPublicado> itens, Guid? anteriorAfterId, Guid? proximoAfterId) = await repository
            .ListarPaginadoAsync(query.AfterId, query.Limit, query.Direction, vigentesEm, cancellationToken)
            .ConfigureAwait(false);

        TipoAtoPublicadoDto[] items = [.. itens.Select(tipo => tipo.ToDto())];
        return Result<ListarTiposAtoPublicadoResult>.Success(
            new ListarTiposAtoPublicadoResult(items, anteriorAfterId, proximoAfterId));
    }

    /// <summary>
    /// O dia que filtra a listagem: hoje no fuso institucional, ou nenhum quando a consulta pede a
    /// série histórica inteira — caso em que o fuso nem é consultado.
    /// </summary>
    private static Result<DateOnly?> DiaDaVigencia(
        ListarTiposAtoPublicadoQuery query, IResolvedorFusoInstitucional fuso, TimeProvider timeProvider)
    {
        if (!query.Vigentes)
        {
            return Result<DateOnly?>.Success(null);
        }

        Result<DateOnly> hoje = fuso.DiaCivilDe(timeProvider.GetUtcNow());
        return hoje.IsFailure
            ? Result<DateOnly?>.Failure(hoje.Error!)
            : Result<DateOnly?>.Success(hoje.Value);
    }
}
