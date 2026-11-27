namespace Unifesspa.UniPlus.Configuracao.Application.Queries.LocaisOferta;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public static class ObterLocalOfertaPorIdQueryHandler
{
    public static async Task<Result<LocalOfertaDto>> Handle(
        ObterLocalOfertaPorIdQuery query,
        ILocalOfertaRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        LocalOferta? local = await repository
            .ObterPorIdParaLeituraAsync(query.Id, cancellationToken)
            .ConfigureAwait(false);

        return local is null
            ? Result<LocalOfertaDto>.Failure(new DomainError(LocalOfertaErrorCodes.NaoEncontrado, "Local de oferta não encontrado."))
            : Result<LocalOfertaDto>.Success(local.ToDto());
    }
}
