namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Perfis;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler da fila de revisões de uma etapa: lê a etapa pedida, recusa quem não tem a permissão dela antes de qualquer
/// leitura e pede a fila.
/// </summary>
public static class ListarFilaDeRevisoesDasConcessoesQueryHandler
{
    public static async Task<Result<IReadOnlyList<ItemDaListaDeRevisoesDto>>> Handle(
        ListarFilaDeRevisoesDasConcessoesQuery query,
        IVerificadorDeAcesso verificador,
        FilaDeRevisoesDasConcessoesDaEtapa fila,
        IUserContext userContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(verificador);
        ArgumentNullException.ThrowIfNull(fila);
        ArgumentNullException.ThrowIfNull(userContext);

        Result<EtapaDaVersao> etapa = await AcessoAFila.AutorizarAsync(query.Etapa, verificador, cancellationToken).ConfigureAwait(false);

        return etapa.IsFailure
            ? Recusa(etapa.Error!)
            : await ListarAsync(fila, etapa.Value, IdentidadeDoToken.De(userContext), cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<IReadOnlyList<ItemDaListaDeRevisoesDto>>> ListarAsync(
        FilaDeRevisoesDasConcessoesDaEtapa fila, EtapaDaVersao etapa, Identidade? leitor, CancellationToken cancellationToken) =>
        leitor is null
            ? Recusa(PerfilHandlerErros.AutorNaoIdentificado())
            : Result<IReadOnlyList<ItemDaListaDeRevisoesDto>>.Success(
                await fila.MontarAsync(etapa, leitor, cancellationToken).ConfigureAwait(false));

    private static Result<IReadOnlyList<ItemDaListaDeRevisoesDto>> Recusa(DomainError erro) =>
        Result<IReadOnlyList<ItemDaListaDeRevisoesDto>>.Failure(erro);
}
