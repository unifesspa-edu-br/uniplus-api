namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Instalacoes;

using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;

/// <summary>Handler da listagem das tentativas sem pendência. A mecânica de keyset vive no repositório.</summary>
public static class ListarTentativasSemPendenciaQueryHandler
{
    public static async Task<ListarTentativasSemPendenciaResult> Handle(
        ListarTentativasSemPendenciaQuery query,
        IInstalacaoRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        (IReadOnlyList<TentativaDeInstalacao> itens, Guid? anterior, Guid? proximo) = await repository
            .ListarTentativasSemPendenciaAsync(query.AfterId, query.Limit, query.Direction, cancellationToken)
            .ConfigureAwait(false);

        TentativaDeInstalacaoDto[] dtos = [.. itens.Select(InstalacaoMapping.ToDto)];

        return new ListarTentativasSemPendenciaResult(dtos, anterior, proximo);
    }
}
