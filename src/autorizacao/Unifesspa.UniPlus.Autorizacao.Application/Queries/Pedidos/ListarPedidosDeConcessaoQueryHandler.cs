namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Pedidos;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Application.Permissoes;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler da listagem. A fila de uma etapa exige a permissão dela, e a fila de todos, a de uma das duas etapas ou a de
/// consultar os pedidos; a lista de quem pergunta exige só a identidade. Nas filas, cada pedido exibido entra na trilha de
/// acesso pela decisão permitida que governou a fila, antes de a página sair (ADR-0153). A mecânica de keyset vive no
/// repositório.
/// </summary>
public static class ListarPedidosDeConcessaoQueryHandler
{
    public static async Task<Result<ListarPedidosDeConcessaoResult>> Handle(
        ListarPedidosDeConcessaoQuery query,
        IPedidoDeConcessaoRepository repository,
        IVerificadorDeAcesso verificador,
        IUserContext userContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(verificador);
        ArgumentNullException.ThrowIfNull(userContext);

        if (IdentidadeDoToken.PessoaDe(userContext) is not { } pessoa)
        {
            return Result<ListarPedidosDeConcessaoResult>.Failure(PedidoHandlerErros.IdentidadeIncompleta());
        }

        Result<DecisaoPermitida?> autorizada = await AutorizarFilaAsync(query.Fila, verificador, cancellationToken).ConfigureAwait(false);
        return autorizada.IsFailure
            ? Result<ListarPedidosDeConcessaoResult>.Failure(autorizada.Error!)
            : await ListarERegistrarAsync(query, pessoa, repository, verificador, autorizada.Value, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// A decisão permitida que governa a fila pedida; nula na lista de quem pergunta, que não exige permissão. A fila
    /// recusada responde 403 e a identidade incompleta, 401.
    /// </summary>
    private static async Task<Result<DecisaoPermitida?>> AutorizarFilaAsync(
        FilaDePedidos fila, IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        if (fila == FilaDePedidos.Meus)
        {
            return Result<DecisaoPermitida?>.Success(null);
        }

        AcessoDoAprovador? acesso = await AcessoDoAprovador.ConsultarAsync(verificador, FinalidadeDoAcesso.Fila, pedidoId: null, cancellationToken).ConfigureAwait(false);
        DecisaoPermitida? governante = acesso is null ? null : await acesso.AutorizarFilaAsync(verificador, fila, cancellationToken).ConfigureAwait(false);

        return governante is not null ? Result<DecisaoPermitida?>.Success(governante)
            : Result<DecisaoPermitida?>.Failure(acesso is null ? PedidoHandlerErros.IdentidadeIncompleta() : PedidoHandlerErros.SemPermissao());
    }

    private static async Task<Result<ListarPedidosDeConcessaoResult>> ListarERegistrarAsync(
        ListarPedidosDeConcessaoQuery query,
        PessoaNoToken pessoa,
        IPedidoDeConcessaoRepository repository,
        IVerificadorDeAcesso verificador,
        DecisaoPermitida? governante,
        CancellationToken cancellationToken)
    {
        FiltroDePedidos filtro = new(
            query.Fila, query.Situacao, pessoa.Identidade, pessoa.EmailVerificado ? PedidoDeConcessao.EmailNormalizado(pessoa.Email) : null);

        (IReadOnlyList<PedidoComPerfil> itens, (string SortKey, Guid Id)? anterior, (string SortKey, Guid Id)? proximo) = await repository
            .ListarPaginadoAsync(filtro, query.AfterSortKey, query.AfterId, query.Limit, query.Direction, cancellationToken)
            .ConfigureAwait(false);
        PedidoResumoDto[] pedidos = [.. itens.Select(PedidoMapping.ToResumo)];
        await RegistrarCadaPedidoAsync(verificador, governante, pedidos, cancellationToken).ConfigureAwait(false);

        return Result<ListarPedidosDeConcessaoResult>.Success(new ListarPedidosDeConcessaoResult(pedidos, anterior, proximo));
    }

    private static Task RegistrarCadaPedidoAsync(
        IVerificadorDeAcesso verificador, DecisaoPermitida? governante, PedidoResumoDto[] pedidos, CancellationToken cancellationToken) =>
        governante is null || pedidos.Length == 0
            ? Task.CompletedTask
            : verificador.RegistrarParaCadaAsync(
                governante, ResourceTiposAutorizacao.Pedido, [.. pedidos.Select(static p => p.Id)], cancellationToken: cancellationToken);
}
