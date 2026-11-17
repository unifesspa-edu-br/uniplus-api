namespace Unifesspa.UniPlus.Publicacoes.Application.Queries.TiposAtoPublicado;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Application.DTOs;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;

public static class ObterTipoAtoPublicadoVigenteQueryHandler
{
    public static async Task<Result<TipoAtoPublicadoDto>> Handle(
        ObterTipoAtoPublicadoVigenteQuery query,
        ITipoAtoPublicadoRepository repository,
        IResolvedorFusoInstitucional fuso,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(fuso);
        ArgumentNullException.ThrowIfNull(timeProvider);

        Result<DateOnly> dia = DiaDaConsulta(query, fuso, timeProvider);

        return dia.IsFailure
            ? Result<TipoAtoPublicadoDto>.Failure(dia.Error!)
            : await LerVigenteAsync(query.Codigo, dia.Value, repository, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// A data informada vale como veio, sem consultar o fuso; sem ela, o dia de hoje no fuso
    /// institucional.
    /// </summary>
    private static Result<DateOnly> DiaDaConsulta(
        ObterTipoAtoPublicadoVigenteQuery query, IResolvedorFusoInstitucional fuso, TimeProvider timeProvider) =>
        query.Data is { } informada
            ? Result<DateOnly>.Success(informada)
            : fuso.DiaCivilDe(timeProvider.GetUtcNow());

    private static async Task<Result<TipoAtoPublicadoDto>> LerVigenteAsync(
        string codigo, DateOnly dia, ITipoAtoPublicadoRepository repository, CancellationToken cancellationToken)
    {
        TipoAtoPublicado? tipo = await repository
            .ObterVigenteAsync(codigo, dia, cancellationToken)
            .ConfigureAwait(false);

        return LeituraDoTipoAtoPublicado.DtoOuNaoEncontrado(tipo);
    }
}
