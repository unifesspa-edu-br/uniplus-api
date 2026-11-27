namespace Unifesspa.UniPlus.Configuracao.Application.Queries.TiposDeficiencia;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public static class ObterTipoDeficienciaPorIdQueryHandler
{
    private static readonly DomainError NaoEncontrado = new(
        TipoDeficienciaErrorCodes.NaoEncontrado,
        "Tipo de deficiência não encontrado.");

    public static async Task<Result<TipoDeficienciaDto>> Handle(
        ObterTipoDeficienciaPorIdQuery query,
        ITipoDeficienciaRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        TipoDeficiencia? tipo = await repository
            .ObterPorIdParaLeituraAsync(query.Id, cancellationToken)
            .ConfigureAwait(false);

        return tipo is null
            ? Result<TipoDeficienciaDto>.Failure(NaoEncontrado)
            : Result<TipoDeficienciaDto>.Success(tipo.ToDto());
    }
}
