namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler da leitura do anexo: só o anexo verificado, ou seja, o objeto promovido, tem URL de leitura, e cada acesso
/// fica registrado com quem o pediu.
/// </summary>
public static class LerAnexoDoPedidoCommandHandler
{
    /// <summary>Validade da URL de leitura; o navegador a segue logo depois de recebê-la.</summary>
    public static readonly TimeSpan PrazoDaLeitura = TimeSpan.FromMinutes(2);

    private const string NomeDoArquivo = "anexo-do-pedido.pdf";

    public static async Task<Result<LeituraDoAnexoDto>> Handle(
        LerAnexoDoPedidoCommand command,
        IPedidoDeConcessaoRepository repository,
        IArmazenamentoDosDocumentosDaAutorizacao armazenamento,
        IVerificadorDeAcesso verificador,
        IAutorizacaoUnitOfWork unitOfWork,
        IUserContext userContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(armazenamento);
        ArgumentNullException.ThrowIfNull(verificador);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (IdentidadeDoToken.PessoaDe(userContext) is not { } pessoa)
        {
            return Result<LeituraDoAnexoDto>.Failure(PedidoHandlerErros.IdentidadeIncompleta());
        }

        PedidoDeConcessao? pedido = await repository.ObterParaLeituraAsync(command.PedidoId, cancellationToken).ConfigureAwait(false);
        if (pedido is null)
        {
            return Result<LeituraDoAnexoDto>.Failure(PedidoHandlerErros.NaoEncontrado());
        }

        DomainError? recusa = await VisibilidadeDoPedido.ConferirAsync(pedido, pessoa, verificador, cancellationToken).ConfigureAwait(false);
        if (recusa is not null)
        {
            return Result<LeituraDoAnexoDto>.Failure(recusa);
        }

        AnexoDoPedido? anexo = pedido.Anexos.FirstOrDefault(a => a.Id == command.AnexoId);
        if (anexo is null)
        {
            return Result<LeituraDoAnexoDto>.Failure(PedidoHandlerErros.NaoEncontrado());
        }

        if (anexo.Situacao != SituacaoDoAnexo.Verificado)
        {
            return Result<LeituraDoAnexoDto>.Failure(PedidoHandlerErros.AnexoNaoVerificado());
        }

        DateTimeOffset agora = timeProvider.GetUtcNow();
        string url = await armazenamento
            .GerarUrlDeLeituraAsync(anexo.ChaveDefinitiva, NomeDoArquivo, PrazoDaLeitura, cancellationToken)
            .ConfigureAwait(false);

        // O acesso se registra antes de a URL sair: a URL só vai a quem ficou na trilha.
        await repository.AdicionarLeituraAsync(
            LeituraDoAnexoDoPedido.Registrar(pedido.Id, anexo.Id, pessoa.Identidade, agora), cancellationToken).ConfigureAwait(false);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result<LeituraDoAnexoDto>.Success(new LeituraDoAnexoDto(new Uri(url, UriKind.Absolute), agora + PrazoDaLeitura));
    }
}
