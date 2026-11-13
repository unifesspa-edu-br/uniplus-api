namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Pedidos;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler da listagem. A fila de uma etapa exige a permissão dela, e a fila de todos, a de uma das duas etapas ou a de
/// consultar os pedidos; a lista de quem pergunta exige só a identidade. Cada pedido exibido entra na trilha de acesso pela
/// decisão permitida que governou a fila, antes de a página sair (ADR-0153). Na lista de quem pergunta, essa decisão é a
/// da consulta dos pedidos, como a do Encarregado de dados (UNI-REQ-0372); sem ela, cada pedido exibido é registrado por
/// titularidade (ADR-0166). A página vazia não grava nada. A mecânica de keyset vive no repositório.
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
    /// A decisão permitida que governa a fila pedida. A lista de quem pergunta não exige permissão: é governada pela
    /// leitura de quem é parte, nula sem a consulta dos pedidos, quando a leitura se registra por titularidade. A fila
    /// recusada responde 403 e a identidade incompleta, 401, também na lista de quem pergunta.
    /// </summary>
    private static async Task<Result<DecisaoPermitida?>> AutorizarFilaAsync(
        FilaDePedidos fila, IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        if (fila == FilaDePedidos.Meus)
        {
            return await AcessoDoAprovador.GovernanteDaLeituraDaParteAsync(verificador, pedidoId: null, cancellationToken).ConfigureAwait(false);
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
        DomainError? recusa = await AcessoDoAprovador
            .RegistrarLeituraAsync(verificador, governante, [.. pedidos.Select(static p => p.Id)], cancellationToken).ConfigureAwait(false);

        return recusa is null
            ? Result<ListarPedidosDeConcessaoResult>.Success(new ListarPedidosDeConcessaoResult(pedidos, anterior, proximo))
            : Result<ListarPedidosDeConcessaoResult>.Failure(recusa);
    }
}
