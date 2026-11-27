namespace Unifesspa.UniPlus.Configuracao.Application.Queries.TiposProcesso;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public static class ObterTipoProcessoPorIdQueryHandler
{
    private static readonly DomainError NaoEncontrado = new(
        TipoProcessoErrorCodes.NaoEncontrado,
        "Tipo de processo seletivo não encontrado.");

    public static async Task<Result<TipoProcessoDto>> Handle(
        ObterTipoProcessoPorIdQuery query,
        ITipoProcessoRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);
        TipoProcesso? tipo = await repository.ObterPorIdParaLeituraAsync(query.Id, cancellationToken).ConfigureAwait(false);
        return tipo is null || (query.ApenasAtivos && !tipo.Ativo)
            ? Result<TipoProcessoDto>.Failure(NaoEncontrado)
            : Result<TipoProcessoDto>.Success(tipo.ToDto());
    }
}
