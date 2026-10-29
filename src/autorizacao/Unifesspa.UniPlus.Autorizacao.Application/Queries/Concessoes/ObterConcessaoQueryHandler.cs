namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Concessoes;

using Unifesspa.UniPlus.Autorizacao.Application.Commands.Concessoes;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Handler da leitura de uma concessão pelo Id.</summary>
public static class ObterConcessaoQueryHandler
{
    public static async Task<Result<ConcessaoDto>> Handle(
        ObterConcessaoQuery query,
        IConcessaoRepository repository,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(timeProvider);

        ConcessaoComPerfil? item = await repository.ObterComPerfilAsync(query.Id, cancellationToken).ConfigureAwait(false);

        return item is null
            ? Result<ConcessaoDto>.Failure(ConcessaoHandlerErros.NaoEncontrada(query.Id))
            : Result<ConcessaoDto>.Success(ConcessaoMapping.ToDto(item, timeProvider.GetUtcNow()));
    }
}
