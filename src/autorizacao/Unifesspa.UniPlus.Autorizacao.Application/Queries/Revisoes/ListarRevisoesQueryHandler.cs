namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Revisoes;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Revisoes;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Application.Permissoes;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler da listagem. Lê quem conduz a revisão, quem revê os acessos da proteção de dados ou quem consulta as revisões.
/// Cada revisão exibida entra na trilha de acesso pela decisão permitida que deu a leitura, antes de a página sair
/// (ADR-0153). A mecânica de keyset vive no repositório.
/// </summary>
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
        ArgumentNullException.ThrowIfNull(timeProvider);

        AcessoDaRevisao? acesso = await AcessoDaRevisao.ConsultarAsync(verificador, null, cancellationToken).ConfigureAwait(false);
        if (acesso is null)
        {
            return Result<ListarRevisoesResult>.Failure(RevisaoHandlerErros.IdentidadeIncompleta());
        }

        await acesso.RegistrarLeituraAsync(verificador, cancellationToken).ConfigureAwait(false);
        return acesso.Leitura is { } leitura
            ? Result<ListarRevisoesResult>.Success(await ListarAsync(query, repository, verificador, leitura, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false))
            : Result<ListarRevisoesResult>.Failure(RevisaoHandlerErros.SemPermissao());
    }

    private static async Task<ListarRevisoesResult> ListarAsync(
        ListarRevisoesQuery query,
        IRevisaoDeAcessosRepository repository,
        IVerificadorDeAcesso verificador,
        DecisaoPermitida leitura,
        DateTimeOffset agora,
        CancellationToken cancellationToken)
    {
        (IReadOnlyList<ResumoDaRevisao> itens, Guid? anterior, Guid? proximo) = await repository
            .ListarPaginadoAsync(new FiltroDeRevisoes(agora, query.Situacao), query.AfterId, query.Limit, query.Direction, cancellationToken)
            .ConfigureAwait(false);

        if (itens.Count > 0)
        {
            await verificador.RegistrarParaCadaAsync(
                leitura, ResourceTiposAutorizacao.Revisao, [.. itens.Select(static i => i.Id)], cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        return new ListarRevisoesResult([.. itens.Select(RevisaoMapping.ToResumo)], anterior, proximo);
    }
}
