namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Pedidos;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Handlers do detalhe e da trilha do pedido. O pedido que a pessoa não vê não se distingue do inexistente.</summary>
public static class ObterPedidoDeConcessaoQueryHandler
{
    public static async Task<Result<PedidoDeConcessaoDto>> Handle(
        ObterPedidoDeConcessaoQuery query,
        IPedidoDeConcessaoRepository repository,
        IPerfilRepository perfis,
        IVerificadorDeAcesso verificador,
        IUserContext userContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(perfis);
        ArgumentNullException.ThrowIfNull(verificador);
        ArgumentNullException.ThrowIfNull(userContext);

        Result<PedidoDeConcessao> pedido = await ObterVisivelAsync(query.Id, repository, verificador, userContext, cancellationToken)
            .ConfigureAwait(false);
        if (pedido.IsFailure)
        {
            return Result<PedidoDeConcessaoDto>.Failure(pedido.Error!);
        }

        Perfil? perfil = await perfis.ObterPorIdAsync(pedido.Value!.PerfilId, cancellationToken).ConfigureAwait(false);

        return Result<PedidoDeConcessaoDto>.Success(PedidoMapping.ToDto(pedido.Value, perfil?.Nome ?? string.Empty));
    }

    public static async Task<Result<IReadOnlyList<EventoDaTrilhaDto>>> Handle(
        ObterTrilhaDoPedidoQuery query,
        IPedidoDeConcessaoRepository repository,
        IVerificadorDeAcesso verificador,
        IUserContext userContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(verificador);
        ArgumentNullException.ThrowIfNull(userContext);

        Result<PedidoDeConcessao> pedido = await ObterVisivelAsync(query.Id, repository, verificador, userContext, cancellationToken)
            .ConfigureAwait(false);
        if (pedido.IsFailure)
        {
            return Result<IReadOnlyList<EventoDaTrilhaDto>>.Failure(pedido.Error!);
        }

        IReadOnlyList<LeituraDoAnexoDoPedido> leituras = await repository
            .ListarLeiturasAsync(query.Id, cancellationToken).ConfigureAwait(false);

        return Result<IReadOnlyList<EventoDaTrilhaDto>>.Success(EventosDoPedido.Montar(pedido.Value!, leituras));
    }

    private static async Task<Result<PedidoDeConcessao>> ObterVisivelAsync(
        Guid id,
        IPedidoDeConcessaoRepository repository,
        IVerificadorDeAcesso verificador,
        IUserContext userContext,
        CancellationToken cancellationToken)
    {
        if (IdentidadeDoToken.PessoaDe(userContext) is not { } pessoa)
        {
            return Result<PedidoDeConcessao>.Failure(PedidoHandlerErros.IdentidadeIncompleta());
        }

        PedidoDeConcessao? pedido = await repository.ObterParaLeituraAsync(id, cancellationToken).ConfigureAwait(false);
        if (pedido is null)
        {
            return Result<PedidoDeConcessao>.Failure(PedidoHandlerErros.NaoEncontrado());
        }

        DomainError? recusa = await VisibilidadeDoPedido.ConferirAsync(pedido, pessoa, verificador, cancellationToken).ConfigureAwait(false);

        return recusa is null ? Result<PedidoDeConcessao>.Success(pedido) : Result<PedidoDeConcessao>.Failure(recusa);
    }
}
