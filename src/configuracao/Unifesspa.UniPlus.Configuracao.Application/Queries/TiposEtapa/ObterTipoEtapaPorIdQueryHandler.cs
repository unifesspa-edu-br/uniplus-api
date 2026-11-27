namespace Unifesspa.UniPlus.Configuracao.Application.Queries.TiposEtapa;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public static class ObterTipoEtapaPorIdQueryHandler
{
    private static readonly DomainError NaoEncontrado = new(
        TipoEtapaErrorCodes.NaoEncontrado,
        "Tipo de etapa não encontrado.");

    public static async Task<Result<TipoEtapaDto>> Handle(
        ObterTipoEtapaPorIdQuery query,
        ITipoEtapaRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);
        TipoEtapa? tipo = await repository.ObterPorIdParaLeituraAsync(query.Id, cancellationToken).ConfigureAwait(false);
        return tipo is { Ativo: true }
            ? Result<TipoEtapaDto>.Success(tipo.ToDto())
            : Result<TipoEtapaDto>.Failure(NaoEncontrado);
    }
}
