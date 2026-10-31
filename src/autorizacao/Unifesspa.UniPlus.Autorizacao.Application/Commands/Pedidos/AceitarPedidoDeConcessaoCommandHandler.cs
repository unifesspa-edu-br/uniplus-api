namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do aceite. O e-mail verificado do token tem de ser o do pedido; senão, o aceite é recusado. O aviso à fila do
/// gestor sai como mensagem de saída, no outbox da mesma transação.
/// </summary>
public static class AceitarPedidoDeConcessaoCommandHandler
{
    public static async Task<(Result<PedidoDeConcessaoDto> Resposta, IEnumerable<object> Mensagens)> Handle(
        AceitarPedidoDeConcessaoCommand command,
        IPedidoDeConcessaoRepository repository,
        IPerfilRepository perfis,
        AvisosDoPedido avisos,
        IAutorizacaoUnitOfWork unitOfWork,
        IUserContext userContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(perfis);
        ArgumentNullException.ThrowIfNull(avisos);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (IdentidadeDoToken.PessoaDe(userContext) is not { } pessoa)
        {
            return Recusa(PedidoHandlerErros.AutorNaoIdentificado());
        }

        PedidoDeConcessao? pedido = await repository.ObterParaAlterarAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (pedido is null)
        {
            return Recusa(PedidoHandlerErros.NaoEncontrado());
        }

        Result aceite = pedido.Aceitar(pessoa, timeProvider.GetUtcNow());
        if (aceite.IsFailure)
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Recusa(aceite.Error!);
        }

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ViolacoesDeGravacao.EhConflitoDeConcorrencia(ex))
        {
            // Com Idempotency-Key o handler captura e descarta o rastreamento (ADR-0119).
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Recusa(PedidoHandlerErros.ConflitoDeConcorrencia());
        }

        PedidoDeConcessaoDto dto = await RespostaDoPedido.ParaDtoAsync(pedido, perfis, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<EnviarEmail> mensagens = await avisos
            .AvisarAsync(pedido, dto.Perfil.Nome, SituacaoDoPedido.AguardandoBeneficiario, cancellationToken)
            .ConfigureAwait(false);

        return (Result<PedidoDeConcessaoDto>.Success(dto), mensagens);
    }

    private static (Result<PedidoDeConcessaoDto> Resposta, IEnumerable<object> Mensagens) Recusa(DomainError erro) =>
        (Result<PedidoDeConcessaoDto>.Failure(erro), []);
}
