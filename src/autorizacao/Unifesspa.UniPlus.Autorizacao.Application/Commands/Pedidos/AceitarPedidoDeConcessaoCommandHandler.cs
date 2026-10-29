namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Handler do aceite. O e-mail verificado do token tem de ser o do pedido; senão, o aceite é recusado.</summary>
public static class AceitarPedidoDeConcessaoCommandHandler
{
    public static async Task<Result<PedidoDeConcessaoDto>> Handle(
        AceitarPedidoDeConcessaoCommand command,
        IPedidoDeConcessaoRepository repository,
        IPerfilRepository perfis,
        IAutorizacaoUnitOfWork unitOfWork,
        IUserContext userContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(perfis);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (IdentidadeDoToken.PessoaDe(userContext) is not { } pessoa)
        {
            return Result<PedidoDeConcessaoDto>.Failure(PedidoHandlerErros.AutorNaoIdentificado());
        }

        PedidoDeConcessao? pedido = await repository.ObterParaAlterarAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (pedido is null)
        {
            return Result<PedidoDeConcessaoDto>.Failure(PedidoHandlerErros.NaoEncontrado());
        }

        Result aceite = pedido.Aceitar(pessoa, timeProvider.GetUtcNow());
        if (aceite.IsFailure)
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result<PedidoDeConcessaoDto>.Failure(aceite.Error!);
        }

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ViolacoesDeGravacao.EhConflitoDeConcorrencia(ex))
        {
            // Com Idempotency-Key o handler captura e descarta o rastreamento (ADR-0119).
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result<PedidoDeConcessaoDto>.Failure(PedidoHandlerErros.ConflitoDeConcorrencia());
        }

        return Result<PedidoDeConcessaoDto>.Success(await RespostaDoPedido.ParaDtoAsync(pedido, perfis, cancellationToken).ConfigureAwait(false));
    }
}
