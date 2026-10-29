namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler da criação do anexo. A URL de PUT sai antes de gravar: se o armazenamento falhar, nada é gravado. O pedido
/// de outra pessoa não se distingue do inexistente.
/// </summary>
public static class CriarAnexoDoPedidoCommandHandler
{
    public const string TipoDeMidiaDoPdf = "application/pdf";

    /// <summary>Validade da URL de PUT; o PDF de até 10 MB sobe dentro dela.</summary>
    public static readonly TimeSpan PrazoDoEnvio = TimeSpan.FromMinutes(15);

    public static async Task<Result<AnexoParaEnvioDto>> Handle(
        CriarAnexoDoPedidoCommand command,
        IPedidoDeConcessaoRepository repository,
        IArmazenamentoDosDocumentosDaAutorizacao armazenamento,
        IAutorizacaoUnitOfWork unitOfWork,
        IUserContext userContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(armazenamento);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (IdentidadeDoToken.De(userContext) is not { } solicitante)
        {
            return Result<AnexoParaEnvioDto>.Failure(PedidoHandlerErros.AutorNaoIdentificado());
        }

        PedidoDeConcessao? pedido = await repository.ObterParaAlterarAsync(command.PedidoId, cancellationToken).ConfigureAwait(false);
        if (pedido is null || pedido.Solicitante != solicitante)
        {
            return Result<AnexoParaEnvioDto>.Failure(PedidoHandlerErros.NaoEncontrado());
        }

        Result<AnexoDoPedido> criado = pedido.NovoAnexo(command.TamanhoDoPdf);
        if (criado.IsFailure)
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result<AnexoParaEnvioDto>.ValidationFailure(criado.Errors);
        }

        AnexoDoPedido anexo = criado.Value!;
        string url = await armazenamento
            .GerarUrlDeEnvioAsync(anexo.ChaveDeEnvio, TipoDeMidiaDoPdf, PrazoDoEnvio, cancellationToken)
            .ConfigureAwait(false);

        // Marca a linha do pedido: criar o anexo e decidir o pedido não correm juntos.
        repository.Atualizar(pedido);

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ViolacoesDeGravacao.EhConflitoDeConcorrencia(ex))
        {
            // Com Idempotency-Key o handler captura e descarta o rastreamento (ADR-0119).
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result<AnexoParaEnvioDto>.Failure(PedidoHandlerErros.ConflitoDeConcorrencia());
        }

        return Result<AnexoParaEnvioDto>.Success(new AnexoParaEnvioDto(
            anexo.Id,
            SituacaoDoAnexoCodigo.AguardandoEnvio,
            new Uri(url, UriKind.Absolute),
            TipoDeMidiaDoPdf,
            timeProvider.GetUtcNow() + PrazoDoEnvio));
    }
}
