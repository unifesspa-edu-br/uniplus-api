namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Perfis;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Handler da fila de aprovação de uma etapa: lê a etapa pedida, recusa quem não tem a permissão dela e pede a fila.</summary>
public static class ListarFilaDeVersoesQueryHandler
{
    public static async Task<Result<IReadOnlyList<ItemDaFilaDeVersoesDto>>> Handle(
        ListarFilaDeVersoesQuery query,
        IVerificadorDeAcesso verificador,
        FilaDeVersoesDaEtapa fila,
        IUserContext userContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(verificador);
        ArgumentNullException.ThrowIfNull(fila);
        ArgumentNullException.ThrowIfNull(userContext);

        if (EtapaDaVersaoCodigo.TentarLer(query.Fila) is not { } etapa)
        {
            return Recusa(PerfilErros.EtapaInvalida());
        }

        DomainError? negado = await AcessoAFila.NegadoAsync(verificador, etapa, cancellationToken).ConfigureAwait(false);

        return negado is not null
            ? Recusa(negado)
            : Result<IReadOnlyList<ItemDaFilaDeVersoesDto>>.Success(
                await fila.MontarAsync(etapa, IdentidadeDoToken.De(userContext), cancellationToken).ConfigureAwait(false));
    }

    private static Result<IReadOnlyList<ItemDaFilaDeVersoesDto>> Recusa(DomainError erro) =>
        Result<IReadOnlyList<ItemDaFilaDeVersoesDto>>.Failure(erro);
}
