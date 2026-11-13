namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Concessoes;

using Unifesspa.UniPlus.Autorizacao.Application.Commands.Concessoes;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Handler da trilha: lê a concessão e deixa a montagem dos eventos com <see cref="TrilhaDaConcessao"/>.</summary>
public static class ObterTrilhaDaConcessaoQueryHandler
{
    public static async Task<Result<IReadOnlyList<EventoDaTrilhaDto>>> Handle(
        ObterTrilhaDaConcessaoQuery query,
        IConcessaoRepository repository,
        TrilhaDaConcessao trilha,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(trilha);

        return await repository.ObterComPerfilAsync(query.Id, cancellationToken).ConfigureAwait(false) is { } item
            ? Result<IReadOnlyList<EventoDaTrilhaDto>>.Success(await trilha.MontarAsync(item.Concessao, cancellationToken).ConfigureAwait(false))
            : Result<IReadOnlyList<EventoDaTrilhaDto>>.Failure(ConcessaoHandlerErros.NaoEncontrada(query.Id));
    }
}
