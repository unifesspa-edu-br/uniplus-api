namespace Unifesspa.UniPlus.Configuracao.Application.Queries.TiposBanca;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public static class ObterTipoBancaPorIdQueryHandler
{
    private static readonly DomainError NaoEncontrado = new(
        TipoBancaErrorCodes.NaoEncontrado,
        "Tipo de banca não encontrado.");

    public static async Task<Result<TipoBancaDto>> Handle(
        ObterTipoBancaPorIdQuery query,
        ITipoBancaRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        TipoBanca? banca = await repository
            .ObterPorIdParaLeituraAsync(query.Id, cancellationToken)
            .ConfigureAwait(false);

        return banca is null
            ? Result<TipoBancaDto>.Failure(NaoEncontrado)
            : Result<TipoBancaDto>.Success(banca.ToDto());
    }
}
