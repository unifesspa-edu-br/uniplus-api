namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Revisoes;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Revisoes;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Handler da listagem. Lê quem conduz a revisão ou quem revê os acessos da proteção de dados. A mecânica de keyset vive no repositório.</summary>
public static class ListarRevisoesQueryHandler
{
    public static async Task<Result<ListarRevisoesResult>> Handle(
        ListarRevisoesQuery query,
        IRevisaoDeAcessosRepository repository,
        IVerificadorDeAcesso verificador,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(verificador);
        ArgumentNullException.ThrowIfNull(timeProvider);

        AcessoDaRevisao? acesso = await AcessoDaRevisao.ConsultarAsync(verificador, cancellationToken).ConfigureAwait(false);
        if (acesso is null)
        {
            return Result<ListarRevisoesResult>.Failure(RevisaoHandlerErros.IdentidadeIncompleta());
        }

        if (!acesso.Le)
        {
            return Result<ListarRevisoesResult>.Failure(RevisaoHandlerErros.SemPermissao());
        }

        (IReadOnlyList<ResumoDaRevisao> itens, Guid? anterior, Guid? proximo) = await repository
            .ListarPaginadoAsync(timeProvider.GetUtcNow(), query.AfterId, query.Limit, query.Direction, cancellationToken)
            .ConfigureAwait(false);

        RevisaoResumoDto[] dtos = [.. itens.Select(RevisaoMapping.ToResumo)];

        return Result<ListarRevisoesResult>.Success(new ListarRevisoesResult(dtos, anterior, proximo));
    }
}
