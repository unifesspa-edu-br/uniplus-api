namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Concessoes;

using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;

/// <summary>Handler da listagem de concessões. A mecânica de keyset vive no repositório.</summary>
public static class ListarConcessoesQueryHandler
{
    public static async Task<ListarConcessoesResult> Handle(
        ListarConcessoesQuery query,
        IConcessaoRepository repository,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(timeProvider);

        DateTimeOffset agora = timeProvider.GetUtcNow();
        FiltroDeConcessoes filtro = new(
            query.PessoaEmissor, query.PessoaSub, query.UnidadeId, query.PerfilId, query.Situacao, agora);

        (IReadOnlyList<ConcessaoComPerfil> itens, Guid? anterior, Guid? proximo) = await repository
            .ListarPaginadoAsync(filtro, query.AfterId, query.Limit, query.Direction, cancellationToken)
            .ConfigureAwait(false);

        ConcessaoDto[] dtos = [.. itens.Select(i => ConcessaoMapping.ToDto(i, agora))];

        return new ListarConcessoesResult(dtos, anterior, proximo);
    }
}
