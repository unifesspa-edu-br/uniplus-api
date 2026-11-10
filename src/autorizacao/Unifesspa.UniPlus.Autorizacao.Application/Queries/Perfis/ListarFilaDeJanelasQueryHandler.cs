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
/// Handler da fila de janelas de uma etapa: lê a etapa pedida, recusa quem não tem a permissão dela antes de qualquer
/// leitura e pede a fila.
/// </summary>
public static class ListarFilaDeJanelasQueryHandler
{
    public static async Task<Result<IReadOnlyList<ItemDaFilaDeJanelasDto>>> Handle(
        ListarFilaDeJanelasQuery query,
        IVerificadorDeAcesso verificador,
        FilaDeJanelasDaEtapa fila,
        IUserContext userContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(verificador);
        ArgumentNullException.ThrowIfNull(fila);
        ArgumentNullException.ThrowIfNull(userContext);

        if (EtapaDaVersaoCodigo.TentarLer(query.Etapa) is not { } etapa)
        {
            return Recusa(PerfilErros.EtapaInvalida());
        }

        DomainError? negado = await AcessoAFila.NegadoAsync(verificador, etapa, cancellationToken).ConfigureAwait(false);

        return negado is not null
            ? Recusa(negado)
            : await ListarAsync(fila, etapa, IdentidadeDoToken.De(userContext), cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<IReadOnlyList<ItemDaFilaDeJanelasDto>>> ListarAsync(
        FilaDeJanelasDaEtapa fila, EtapaDaVersao etapa, Identidade? leitor, CancellationToken cancellationToken) =>
        leitor is null
            ? Recusa(PerfilHandlerErros.AutorNaoIdentificado())
            : Result<IReadOnlyList<ItemDaFilaDeJanelasDto>>.Success(
                await fila.MontarAsync(etapa, leitor, cancellationToken).ConfigureAwait(false));

    private static Result<IReadOnlyList<ItemDaFilaDeJanelasDto>> Recusa(DomainError erro) =>
        Result<IReadOnlyList<ItemDaFilaDeJanelasDto>>.Failure(erro);
}
