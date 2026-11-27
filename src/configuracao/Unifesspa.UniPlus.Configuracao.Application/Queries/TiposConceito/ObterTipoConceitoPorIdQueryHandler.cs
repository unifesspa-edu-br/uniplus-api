namespace Unifesspa.UniPlus.Configuracao.Application.Queries.TiposConceito;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public static class ObterTipoConceitoPorIdQueryHandler
{
    private static readonly DomainError NaoEncontrado = new(
        TipoConceitoErrorCodes.NaoEncontrado,
        "Tipo de conceito não encontrado.");

    public static async Task<Result<TipoConceitoDto>> Handle(
        ObterTipoConceitoPorIdQuery query,
        ITipoConceitoRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);
        TipoConceito? tipo = await repository.ObterPorIdParaLeituraAsync(query.Id, cancellationToken).ConfigureAwait(false);
        return tipo is { Ativo: true }
            ? Result<TipoConceitoDto>.Success(tipo.ToDto())
            : Result<TipoConceitoDto>.Failure(NaoEncontrado);
    }
}
